using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Interfaces;
using RailwayInterlock.Data;

namespace RailwayInterlock.Components
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class Train : MonoBehaviour, ITrain
    {
        [Header("Configuration")]
        public string trainId;
        public string displayName;
        public float maxSpeedKmh = 60f;
        public float acceleration = 2f;
        public float brakeDeceleration = 5f;
        public float emergencyBrakeDeceleration = 10f;
        public float trainLength = 20f;
        public Direction travelDirection = Direction.Up;

        [Header("Signal Detection")]
        public Transform signalDetectionPoint;
        public float signalDetectionRange = 30f;
        public LayerMask signalLayer = ~0;
        public float stopDistanceFromSignal = 5f;

        [Header("References")]
        public List<Transform> bogies = new List<Transform>();
        public ParticleSystem brakeSmoke;
        public AudioSource brakeSound;
        public AudioSource hornSound;

        [Header("Debug")]
        [SerializeField] private TrainState _state = TrainState.Stopped;
        [SerializeField] private float _currentSpeedMs;
        [SerializeField] private bool _isBrakeApplied;
        [SerializeField] private bool _isEmergencyBrake;
        [SerializeField] private Signal _currentSignalAhead;
        [SerializeField] private string _currentTrackId;

        private Rigidbody _rb;
        private bool _automaticMode = true;

        public string Id => trainId;
        public TrainState State => _state;
        public float Speed => _currentSpeedMs;
        public float SpeedKmh => _currentSpeedMs * 3.6f;
        public bool IsAutomaticMode => _automaticMode;
        public Signal CurrentSignalAhead => _currentSignalAhead;
        public string CurrentTrackId => _currentTrackId;

        public event Action<string, TrainState> OnStateChanged;
        public event Action<string, float> OnSpeedChanged;
        public event Action<string> OnEmergencyBrakeTriggered;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = false;
            _rb.useGravity = true;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

        private void Update()
        {
            DetectSignalsAhead();

            if (_automaticMode)
            {
                HandleAutomaticDriving();
            }

            UpdateState();
        }

        private void FixedUpdate()
        {
            HandlePhysicsMovement();
        }

        private void DetectSignalsAhead()
        {
            Vector3 detectionOrigin = signalDetectionPoint != null ? signalDetectionPoint.position : transform.position + Vector3.up * 2f;
            Vector3 detectionDir = travelDirection == Direction.Up ? transform.forward : -transform.forward;

            _currentSignalAhead = null;
            RaycastHit[] hits = Physics.RaycastAll(detectionOrigin, detectionDir, signalDetectionRange, signalLayer);

            float closestDistance = float.MaxValue;
            foreach (var hit in hits)
            {
                var signal = hit.collider.GetComponentInParent<Signal>();
                if (signal != null)
                {
                    if (signal.direction != travelDirection)
                        continue;

                    float dist = Vector3.Distance(detectionOrigin, hit.point);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        _currentSignalAhead = signal;
                    }
                }
            }
        }

        private void HandleAutomaticDriving()
        {
            if (_currentSignalAhead != null)
            {
                float distanceToSignal = Vector3.Distance(
                    signalDetectionPoint != null ? signalDetectionPoint.position : transform.position,
                    _currentSignalAhead.transform.position);

                if (_currentSignalAhead.ShouldStopTrain())
                {
                    float requiredStopDistance = CalculateRequiredStopDistance();

                    if (distanceToSignal <= requiredStopDistance + stopDistanceFromSignal)
                    {
                        ApplyBrake();
                    }

                    if (distanceToSignal <= stopDistanceFromSignal && _currentSpeedMs > 0.5f)
                    {
                        TriggerEmergencyBrake();
                    }
                }
                else
                {
                    ReleaseBrake();
                }
            }
            else
            {
                ReleaseBrake();
            }
        }

        private float CalculateRequiredStopDistance()
        {
            float decel = _isEmergencyBrake ? emergencyBrakeDeceleration : brakeDeceleration;
            return (_currentSpeedMs * _currentSpeedMs) / (2f * decel);
        }

        private void HandlePhysicsMovement()
        {
            float targetSpeedMs = maxSpeedKmh / 3.6f;
            Vector3 moveDir = travelDirection == Direction.Up ? transform.forward : -transform.forward;

            if (_isBrakeApplied)
            {
                float decel = _isEmergencyBrake ? emergencyBrakeDeceleration : brakeDeceleration;
                _currentSpeedMs = Mathf.Max(0f, _currentSpeedMs - decel * Time.fixedDeltaTime);

                if (_currentSpeedMs <= 0.01f)
                {
                    _currentSpeedMs = 0f;
                    _rb.velocity = Vector3.zero;
                    PlayBrakeEffects(false);
                    return;
                }
            }
            else
            {
                if (_currentSpeedMs < targetSpeedMs)
                {
                    _currentSpeedMs = Mathf.Min(targetSpeedMs, _currentSpeedMs + acceleration * Time.fixedDeltaTime);
                }
            }

            Vector3 newVelocity = moveDir * _currentSpeedMs;
            newVelocity.y = _rb.velocity.y;
            _rb.velocity = Vector3.Lerp(_rb.velocity, newVelocity, 0.5f);
        }

        private void UpdateState()
        {
            TrainState newState;
            if (_currentSpeedMs <= 0.1f)
                newState = TrainState.Stopped;
            else if (_isBrakeApplied)
                newState = TrainState.Braking;
            else
                newState = TrainState.Moving;

            if (_state != newState)
            {
                _state = newState;
                OnStateChanged?.Invoke(trainId, _state);
                Debug.Log($"[Train] {trainId} 状态: {_state}, 速度: {SpeedKmh:F1} km/h");
            }
        }

        public void ApplyBrake()
        {
            if (!_isBrakeApplied)
            {
                _isBrakeApplied = true;
                _isEmergencyBrake = false;
                PlayBrakeEffects(true);
            }
        }

        public void ReleaseBrake()
        {
            if (_isBrakeApplied || _isEmergencyBrake)
            {
                _isBrakeApplied = false;
                _isEmergencyBrake = false;
                PlayBrakeEffects(false);
            }
        }

        public void TriggerEmergencyBrake()
        {
            if (!_isEmergencyBrake)
            {
                _isBrakeApplied = true;
                _isEmergencyBrake = true;
                PlayBrakeEffects(true);
                PlayHorn();
                OnEmergencyBrakeTriggered?.Invoke(trainId);
                Debug.LogWarning($"[Train] {trainId} 触发紧急制动！");
            }
        }

        private void PlayBrakeEffects(bool active)
        {
            if (brakeSmoke != null)
            {
                if (active && _currentSpeedMs > 1f)
                    brakeSmoke.Play();
                else
                    brakeSmoke.Stop();
            }

            if (brakeSound != null)
            {
                if (active && _currentSpeedMs > 1f)
                {
                    if (!brakeSound.isPlaying)
                        brakeSound.Play();
                }
                else
                {
                    brakeSound.Stop();
                }
            }
        }

        public void PlayHorn()
        {
            if (hornSound != null)
            {
                hornSound.Play();
            }
        }

        public void SetManualMode()
        {
            _automaticMode = false;
        }

        public void SetAutomaticMode()
        {
            _automaticMode = true;
        }

        public void ToggleAutomaticMode()
        {
            _automaticMode = !_automaticMode;
        }

        public void ManualThrottle(float amount)
        {
            if (_automaticMode) return;

            if (amount > 0)
            {
                ReleaseBrake();
                float target = amount * maxSpeedKmh / 3.6f;
                _currentSpeedMs = Mathf.Min(target, _currentSpeedMs + acceleration * Time.deltaTime);
            }
            else if (amount < 0)
            {
                ApplyBrake();
            }
        }

        public void SetCurrentTrack(string trackId)
        {
            _currentTrackId = trackId;
        }

        public TrainData ToData()
        {
            return new TrainData
            {
                Id = trainId,
                Name = displayName,
                StartTrackId = _currentTrackId,
                MaxSpeedKmh = maxSpeedKmh,
                Acceleration = acceleration,
                BrakeDeceleration = brakeDeceleration,
                Length = trainLength
            };
        }

        public void ResetTrain(Vector3 position, Quaternion rotation)
        {
            transform.SetPositionAndRotation(position, rotation);
            _rb.velocity = Vector3.zero;
            _currentSpeedMs = 0f;
            _isBrakeApplied = false;
            _isEmergencyBrake = false;
            _state = TrainState.Stopped;
        }

        private void OnTriggerEnter(Collider other)
        {
            var track = other.GetComponent<TrackCircuit>();
            if (track != null)
            {
                SetCurrentTrack(track.trackId);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Vector3 origin = signalDetectionPoint != null ? signalDetectionPoint.position : transform.position + Vector3.up * 2f;
            Vector3 dir = travelDirection == Direction.Up ? transform.forward : -transform.forward;

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(origin, dir * signalDetectionRange);

            if (_currentSignalAhead != null)
            {
                Gizmos.color = _currentSignalAhead.ShouldStopTrain() ? Color.red : Color.green;
                Gizmos.DrawLine(origin, _currentSignalAhead.transform.position);

                float stopDist = CalculateRequiredStopDistance() + stopDistanceFromSignal;
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
                Gizmos.DrawSphere(origin + dir * stopDist, 1f);
            }
        }
    }
}
