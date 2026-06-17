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
    [RequireComponent(typeof(TrackScanner))]
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
        public float signalDetectionRange = 60f;
        public LayerMask signalLayer = ~0;
        public float stopDistanceFromSignal = 5f;

        [Header("High-Speed Protection")]
        public float maxDisplacementPerFrame = 8f;
        public float safetyBrakeDistanceFactor = 1.5f;
        public float minTrackBoundaryRespect = 3f;
        public bool enableSpeedClamping = true;

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
        private TrackScanner _scanner;
        private bool _automaticMode = true;
        private bool _forceEmergencyBrakePending;
        private Vector3 _positionBeforePhysicsStep;

        public string Id => trainId;
        public TrainState State => _state;
        public float Speed => _currentSpeedMs;
        public float SpeedKmh => _currentSpeedMs * 3.6f;
        public bool IsAutomaticMode => _automaticMode;
        public Signal CurrentSignalAhead => _currentSignalAhead;
        public string CurrentTrackId => _currentTrackId;
        public TrackScanner Scanner => _scanner;

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
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _scanner = GetComponent<TrackScanner>();
            if (_scanner == null)
                _scanner = gameObject.AddComponent<TrackScanner>();
        }

        private void Start()
        {
            _positionBeforePhysicsStep = transform.position;

            if (_scanner != null)
            {
                _scanner.OnTrackEntered += HandleTrackEntered;
                _scanner.OnTrackLeft += HandleTrackLeft;
                _scanner.OnRedSignalCrossed += HandleRedSignalCrossed;
            }
        }

        private void FixedUpdate()
        {
            _positionBeforePhysicsStep = transform.position;

            if (_forceEmergencyBrakePending)
            {
                _forceEmergencyBrakePending = false;
                ForceEmergencyBrakeImmediate();
            }

            EvaluateDrivingLogic();

            ClampSpeedForSafeDisplacement();

            HandlePhysicsMovement();
        }

        private void Update()
        {
            UpdateCurrentTrackFromScanner();
            UpdateState();
            UpdateBrakeEffects();
        }

        private void EvaluateDrivingLogic()
        {
            if (!_automaticMode) return;

            if (_scanner == null) return;

            bool needBrake = false;
            bool needEmergency = false;

            float distanceToStopPoint;
            if (_scanner.IsApproachingRedSignal(out distanceToStopPoint))
            {
                float effectiveDistance = distanceToStopPoint - stopDistanceFromSignal;
                float requiredStopDist = CalculateRequiredStopDistance() * safetyBrakeDistanceFactor;

                if (effectiveDistance <= requiredStopDist)
                {
                    needBrake = true;
                }

                if (effectiveDistance <= stopDistanceFromSignal && _currentSpeedMs > 0.5f)
                {
                    needEmergency = true;
                }

                float nextFrameMaxDisp = _scanner.CalculateMaxDisplacement();
                if (effectiveDistance <= nextFrameMaxDisp + safetyBrakeDistanceFactor)
                {
                    needEmergency = true;
                }
            }

            float brakingDistanceForTrain = CalculateRequiredStopDistance() * safetyBrakeDistanceFactor;
            if (_scanner.IsTrainAheadInBrakingDistance(brakingDistanceForTrain))
            {
                needBrake = true;

                float trainAheadDist = _scanner.DistanceToClosestTrain;
                float minSafeGap = trainLength + stopDistanceFromSignal;
                if (trainAheadDist <= minSafeGap)
                {
                    needEmergency = true;
                }
            }

            if (_scanner.ClosestYellowSignal != null)
            {
                float yellowDist = _scanner.DistanceToClosestYellowSignal;
                float targetSpeedMs = maxSpeedKmh / 3.6f * 0.5f;
                if (_currentSpeedMs > targetSpeedMs && yellowDist < CalculateRequiredStopDistance() * 1.5f)
                {
                    needBrake = true;
                }
            }

            if (needEmergency)
            {
                if (!_isEmergencyBrake)
                    TriggerEmergencyBrake();
            }
            else if (needBrake)
            {
                ApplyBrake();
            }
            else
            {
                ReleaseBrake();
            }

            _currentSignalAhead = _scanner.ClosestSignal;
        }

        private void ClampSpeedForSafeDisplacement()
        {
            if (!enableSpeedClamping || _currentSpeedMs <= 0.01f) return;

            float scaledFixedDt = Time.fixedDeltaTime * Mathf.Max(Time.timeScale, 1f);
            float maxSafeSpeed = maxDisplacementPerFrame / scaledFixedDt;

            if (_currentSpeedMs > maxSafeSpeed)
            {
                _currentSpeedMs = maxSafeSpeed;
                Debug.Log($"[Train] {trainId} 速度被钳制为 {SpeedKmh:F1} km/h（防止帧位移越界）");
            }
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

            ValidatePositionAfterPhysics();
        }

        private void ValidatePositionAfterPhysics()
        {
            if (_scanner == null || _currentSpeedMs < 0.1f) return;

            float displacement = Vector3.Distance(_positionBeforePhysicsStep, transform.position);

            if (displacement > maxDisplacementPerFrame)
            {
                Vector3 moveDir = (transform.position - _positionBeforePhysicsStep).normalized;
                Vector3 clampedPos = _positionBeforePhysicsStep + moveDir * maxDisplacementPerFrame;
                clampedPos.y = transform.position.y;
                _rb.position = clampedPos;
                _rb.velocity = moveDir * _currentSpeedMs;

                Debug.LogWarning($"[Train] {trainId} 帧位移 {displacement:F2}m 超过安全阈值 {maxDisplacementPerFrame}m，已钳制位置");
            }

            if (_scanner.ClosestRedSignal != null && _currentSpeedMs > 0.1f)
            {
                float distToRed = _scanner.DistanceToClosestRedSignal;
                if (distToRed < stopDistanceFromSignal * 0.5f)
                {
                    float overshoot = stopDistanceFromSignal * 0.5f - distToRed;
                    Vector3 pullbackDir = travelDirection == Direction.Up ? -transform.forward : transform.forward;
                    _rb.position += pullbackDir * overshoot;
                    _currentSpeedMs = 0f;
                    _rb.velocity = Vector3.zero;
                    ApplyBrake();

                    Debug.LogWarning($"[Train] {trainId} 越过红灯信号机，已拉回 {overshoot:F2}m");
                }
            }
        }

        private void UpdateCurrentTrackFromScanner()
        {
            if (_scanner != null && _scanner.CurrentTracks != null)
            {
                foreach (var tc in _scanner.CurrentTracks)
                {
                    if (tc != null)
                    {
                        _currentTrackId = tc.trackId;
                        break;
                    }
                }
            }
        }

        private float CalculateRequiredStopDistance()
        {
            float decel = _isEmergencyBrake ? emergencyBrakeDeceleration : brakeDeceleration;
            return (_currentSpeedMs * _currentSpeedMs) / (2f * Mathf.Max(decel, 0.1f));
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
            }
        }

        private void UpdateBrakeEffects()
        {
            PlayBrakeEffects(_isBrakeApplied && _currentSpeedMs > 1f);
        }

        private void HandleTrackEntered(Train train, TrackCircuit track)
        {
            if (train == this && track != null)
            {
                _currentTrackId = track.trackId;
            }
        }

        private void HandleTrackLeft(Train train, TrackCircuit track)
        {
        }

        private void HandleRedSignalCrossed(Train train, Signal signal)
        {
            if (train == this)
            {
                _forceEmergencyBrakePending = true;
                Debug.LogError($"[Train] {trainId} 穿越红灯信号机 {signal.signalId}！触发保护性紧急制动！");
            }
        }

        public void ApplyBrake()
        {
            if (!_isBrakeApplied)
            {
                _isBrakeApplied = true;
                _isEmergencyBrake = false;
            }
        }

        public void ReleaseBrake()
        {
            if (_isBrakeApplied || _isEmergencyBrake)
            {
                _isBrakeApplied = false;
                _isEmergencyBrake = false;
            }
        }

        public void TriggerEmergencyBrake()
        {
            if (!_isEmergencyBrake)
            {
                _isBrakeApplied = true;
                _isEmergencyBrake = true;
                PlayHorn();
                OnEmergencyBrakeTriggered?.Invoke(trainId);
                Debug.LogWarning($"[Train] {trainId} 触发紧急制动！");
            }
        }

        public void ForceEmergencyBrake()
        {
            _forceEmergencyBrakePending = true;
        }

        private void ForceEmergencyBrakeImmediate()
        {
            _isBrakeApplied = true;
            _isEmergencyBrake = true;
            _currentSpeedMs = 0f;
            _rb.velocity = Vector3.zero;
            PlayHorn();
            OnEmergencyBrakeTriggered?.Invoke(trainId);
            Debug.LogError($"[Train] {trainId} 强制紧急制动！（穿越红灯保护）");
        }

        private void PlayBrakeEffects(bool active)
        {
            if (brakeSmoke != null)
            {
                if (active)
                    brakeSmoke.Play();
                else
                    brakeSmoke.Stop();
            }

            if (brakeSound != null)
            {
                if (active)
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
            _forceEmergencyBrakePending = false;
            _state = TrainState.Stopped;

            if (_scanner != null)
            {
                _scanner.ClearAllTrackOccupancy();
            }
        }

        private void OnDestroy()
        {
            if (_scanner != null)
            {
                _scanner.OnTrackEntered -= HandleTrackEntered;
                _scanner.OnTrackLeft -= HandleTrackLeft;
                _scanner.OnRedSignalCrossed -= HandleRedSignalCrossed;
                _scanner.ClearAllTrackOccupancy();
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
            }

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.2f);
            float stopDist = CalculateRequiredStopDistance() + stopDistanceFromSignal;
            Gizmos.DrawSphere(origin + dir * stopDist, 1f);

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawSphere(origin + dir * stopDistanceFromSignal, 0.8f);
        }
    }
}
