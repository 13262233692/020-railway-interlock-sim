using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Components;

namespace RailwayInterlock.Pathfinding
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public class OutOfControlEngineer : MonoBehaviour
    {
        [Header("基本配置")]
        public string engineerId = "ENG-001";
        public string displayName = "失控工程维修车";
        public Direction travelDirection = Direction.Up;
        public float cruiseSpeedKmh = 40f;
        public float maxSpeedKmh = 55f;
        public float acceleration = 1.2f;
        public float laneChangeInterval = 12f;

        [Header("行为模式")]
        public bool ignoreSignals = true;
        public bool randomLaneChange = true;
        [Range(0f, 1f)] public float laneChangeProbability = 0.4f;

        [Header("状态")]
        [SerializeField] private float _currentSpeedMs;
        [SerializeField] private TrainState _state = TrainState.Stopped;
        [SerializeField] private string _currentTrackId;
        [SerializeField] private List<string> _occupiedTrackIds = new List<string>();

        private Rigidbody _rb;
        private BoxCollider _bodyCol;
        private TrackScanner _scanner;
        private readonly HashSet<TrackCircuit> _registeredTracks = new HashSet<TrackCircuit>();

        private float _nextLaneChangeTime;
        private Vector3 _lastPosition;
        private bool _isInitialized;

        public event Action<OutOfControlEngineer, string, string> OnTrackChanged;
        public event Action<OutOfControlEngineer> OnEmergencyDetected;

        public float Speed => _currentSpeedMs;
        public TrainState State => _state;
        public Direction Direction => travelDirection;
        public string CurrentTrackId => _currentTrackId;
        public IReadOnlyList<string> OccupiedTrackIds => _occupiedTrackIds;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.mass = 8000f;
            _rb.drag = 0.15f;
            _rb.angularDrag = 1.2f;
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            _bodyCol = GetComponent<BoxCollider>();
            _bodyCol.isTrigger = false;
            _bodyCol.center = new Vector3(0, 1.2f, 0);
            _bodyCol.size = new Vector3(2f, 2.4f, 8f);

            _scanner = GetComponent<TrackScanner>();
            if (_scanner == null)
            {
                _scanner = gameObject.AddComponent<TrackScanner>();
            }
            _scanner.signalScanRange = 35f;
            _scanner.trackOverlapHalfExtent = 1.2f;
            _scanner.raycastSubdivisions = 4;

            _scanner.OnGenericTrackEntered += HandleTrackEntered;
            _scanner.OnGenericTrackLeft += HandleTrackLeft;
        }

        private void Start()
        {
            _isInitialized = true;
            _lastPosition = transform.position;
            _nextLaneChangeTime = Time.time + laneChangeInterval;
        }

        private void FixedUpdate()
        {
            if (!_isInitialized) return;

            float dt = Time.fixedDeltaTime;
            UpdateOccupancyState();
            EvaluateEngineerBehavior(dt);
            HandlePhysicsMovement(dt);

            if (randomLaneChange && Time.time >= _nextLaneChangeTime)
            {
                TryRandomLaneChange();
                _nextLaneChangeTime = Time.time + laneChangeInterval * UnityEngine.Random.Range(0.7f, 1.3f);
            }
        }

        private void UpdateOccupancyState()
        {
            _occupiedTrackIds.Clear();
            foreach (var tc in _registeredTracks)
            {
                if (tc != null) _occupiedTrackIds.Add(tc.trackId);
            }
            if (_occupiedTrackIds.Count > 0)
            {
                _currentTrackId = _occupiedTrackIds[0];
            }
        }

        private void EvaluateEngineerBehavior(float dt)
        {
            float targetSpeed = cruiseSpeedKmh / 3.6f;
            if (_scanner != null)
            {
                var redSignal = _scanner.GetRedSignalAhead();
                if (redSignal.signal != null && !ignoreSignals)
                {
                    float brakeDist = _currentSpeedMs * _currentSpeedMs / (2f * 5f);
                    if (redSignal.distance <= brakeDist + 2f)
                    {
                        targetSpeed = 0f;
                    }
                }

                var trainAhead = _scanner.GetTrainAhead();
                if (trainAhead.HasValue && trainAhead.Value.train != null)
                {
                    float brakeDist = _currentSpeedMs * _currentSpeedMs / (2f * 6f);
                    if (trainAhead.Value.distance <= brakeDist + 4f)
                    {
                        targetSpeed = 0f;
                        OnEmergencyDetected?.Invoke(this);
                    }
                }
            }

            if (_currentSpeedMs < targetSpeed)
            {
                _currentSpeedMs += acceleration * dt;
                if (_currentSpeedMs > targetSpeed) _currentSpeedMs = targetSpeed;
            }
            else if (_currentSpeedMs > targetSpeed)
            {
                _currentSpeedMs -= 4f * dt;
                if (_currentSpeedMs < targetSpeed) _currentSpeedMs = targetSpeed;
            }

            if (_currentSpeedMs <= 0.05f)
            {
                _currentSpeedMs = 0f;
                _state = TrainState.Stopped;
            }
            else if (targetSpeed < 0.05f)
            {
                _state = TrainState.Braking;
            }
            else
            {
                _state = TrainState.Moving;
            }
        }

        private void HandlePhysicsMovement(float dt)
        {
            Vector3 dirVec = travelDirection == Direction.Up
                ? transform.forward
                : -transform.forward;

            Vector3 targetVelocity = dirVec * _currentSpeedMs;
            Vector3 clampedVel = Vector3.MoveTowards(_rb.velocity, targetVelocity, 10f * dt);

            float maxDispPerFrame = 6f;
            float dispThisFrame = (clampedVel * dt).magnitude;
            if (dispThisFrame > maxDispPerFrame)
            {
                clampedVel = clampedVel.normalized * (maxDispPerFrame / Mathf.Max(dt, 0.001f));
            }

            _rb.velocity = clampedVel;
            _lastPosition = transform.position;
        }

        private void TryRandomLaneChange()
        {
            if (UnityEngine.Random.value > laneChangeProbability) return;

            if (_registeredTracks.Count == 0) return;

            int attemptCount = 0;
            foreach (var tc in _registeredTracks)
            {
                if (tc == null || tc.adjacentTrackIds == null) continue;

                foreach (var adjId in tc.adjacentTrackIds)
                {
                    attemptCount++;
                    if (attemptCount > 3) break;

                    Vector3 switchDir = UnityEngine.Random.value >= 0.5f
                        ? transform.right
                        : -transform.right;

                    Ray ray = new Ray(transform.position + Vector3.up * 1.5f, switchDir);
                    if (Physics.Raycast(ray, 10f, LayerMask.GetMask("Default")))
                    {
                        continue;
                    }

                    StartCoroutine(CoSimulateLaneChange(switchDir));
                    return;
                }
            }
        }

        private System.Collections.IEnumerator CoSimulateLaneChange(Vector3 switchDir)
        {
            float duration = 2f;
            float elapsed = 0f;
            Vector3 startPos = transform.position;
            Vector3 targetPos = startPos + switchDir * 10f;

            float originalSpeed = _currentSpeedMs;
            _currentSpeedMs *= 0.6f;

            Quaternion startRot = transform.rotation;
            Quaternion targetRot = startRot * Quaternion.Euler(0, switchDir.x > 0 ? 10f : -10f, 0);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(startPos, targetPos, t);
                transform.rotation = Quaternion.Slerp(startRot, t < 0.5f ? targetRot : startRot, t * 2f);
                yield return null;
            }

            transform.rotation = startRot;
            _currentSpeedMs = originalSpeed;
        }

        private void HandleTrackEntered(TrackCircuit tc)
        {
            if (tc == null) return;
            if (_registeredTracks.Add(tc))
            {
                tc.RegisterTrainOccupancy(null);
                string prevTrack = _currentTrackId;
                _currentTrackId = tc.trackId;
                OnTrackChanged?.Invoke(this, prevTrack, tc.trackId);
            }
        }

        private void HandleTrackLeft(TrackCircuit tc)
        {
            if (tc == null) return;
            if (_registeredTracks.Remove(tc))
            {
                tc.UnregisterTrainOccupancy(null);
            }
        }

        public TrainPositionSnapshot GetSnapshot()
        {
            return new TrainPositionSnapshot
            {
                TrainId = engineerId,
                CurrentTrackId = _currentTrackId,
                OccupiedTrackIds = new List<string>(_occupiedTrackIds),
                TravelDirection = travelDirection,
                SpeedMs = _currentSpeedMs,
                WorldPosition = transform.position,
                ArrivalTimeAtNextNode = float.MaxValue
            };
        }

        public Vector3 GetForwardPoint(float distance)
        {
            Vector3 dir = travelDirection == Direction.Up
                ? transform.forward
                : -transform.forward;
            return transform.position + dir * distance;
        }

        private void OnDisable()
        {
            foreach (var tc in _registeredTracks)
            {
                if (tc != null) tc.UnregisterTrainOccupancy(null);
            }
            _registeredTracks.Clear();
        }
    }
}
