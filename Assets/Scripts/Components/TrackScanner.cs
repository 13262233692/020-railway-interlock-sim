using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Components;

namespace RailwayInterlock.Components
{
    public class TrackScanner : MonoBehaviour
    {
        [Header("Scan Configuration")]
        public float signalScanRange = 60f;
        public float trackOverlapHalfExtent = 1.5f;
        public float trackOverlapHeight = 2f;
        public int raycastSubdivisions = 5;
        public float safetyMarginDistance = 2f;
        public LayerMask trackCircuitLayer = ~0;
        public LayerMask signalLayerMask = ~0;
        public LayerMask trainLayerMask = ~0;

        [Header("State")]
        [SerializeField] private Signal _closestSignal;
        [SerializeField] private float _distanceToClosestSignal = float.MaxValue;
        [SerializeField] private Signal _closestRedSignal;
        [SerializeField] private float _distanceToClosestRedSignal = float.MaxValue;
        [SerializeField] private Signal _closestYellowSignal;
        [SerializeField] private float _distanceToClosestYellowSignal = float.MaxValue;
        [SerializeField] private Train _closestTrainAhead;
        [SerializeField] private float _distanceToClosestTrain = float.MaxValue;

        private Train _train;
        private Rigidbody _rb;
        private Vector3 _previousPosition;
        private Quaternion _previousRotation;
        private readonly HashSet<TrackCircuit> _currentTracks = new HashSet<TrackCircuit>();
        private readonly HashSet<TrackCircuit> _newTracks = new HashSet<TrackCircuit>();
        private readonly HashSet<TrackCircuit> _leftTracks = new HashSet<TrackCircuit>();
        private bool _initialized;
        private bool _useTrainReference;

        public Signal ClosestSignal => _closestSignal;
        public float DistanceToClosestSignal => _distanceToClosestSignal;
        public Signal ClosestRedSignal => _closestRedSignal;
        public float DistanceToClosestRedSignal => _distanceToClosestRedSignal;
        public Signal ClosestYellowSignal => _closestYellowSignal;
        public float DistanceToClosestYellowSignal => _distanceToClosestYellowSignal;
        public Train ClosestTrainAhead => _closestTrainAhead;
        public float DistanceToClosestTrain => _distanceToClosestTrain;
        public IReadOnlyCollection<TrackCircuit> CurrentTracks => _currentTracks;

        public event Action<Train, TrackCircuit> OnTrackEntered;
        public event Action<Train, TrackCircuit> OnTrackLeft;
        public event Action<Train, Signal> OnRedSignalCrossed;

        public event Action<TrackCircuit> OnGenericTrackEntered;
        public event Action<TrackCircuit> OnGenericTrackLeft;
        public event Action<Signal> OnGenericRedCrossed;

        private void Awake()
        {
            _train = GetComponent<Train>();
            _rb = GetComponent<Rigidbody>();
            _useTrainReference = _train != null;
        }

        private void Start()
        {
            _previousPosition = transform.position;
            _previousRotation = transform.rotation;
            _initialized = true;
            ScanCurrentTrackOccupancy(true);
        }

        private void FixedUpdate()
        {
            if (!_initialized) return;
            if (_useTrainReference && _train == null) return;

            ScanCurrentTrackOccupancy(false);
            ScanSignalsAhead();
            ScanTrainsAhead();
            if (_useTrainReference)
            {
                CheckSweptPathForRedSignals();
            }

            _previousPosition = transform.position;
            _previousRotation = transform.rotation;
        }

        private void ScanCurrentTrackOccupancy(bool initialScan)
        {
            Vector3 center = transform.position + Vector3.up * 1f;
            float halfLength = _train != null ? _train.trainLength * 0.5f : 7f;

            Collider[] overlaps = Physics.OverlapBox(
                center,
                new Vector3(trackOverlapHalfExtent, trackOverlapHeight * 0.5f, halfLength),
                transform.rotation,
                trackCircuitLayer,
                QueryTriggerInteraction.Collide
            );

            var detectedTracks = new HashSet<TrackCircuit>();
            foreach (var col in overlaps)
            {
                var tc = col.GetComponent<TrackCircuit>();
                if (tc != null)
                    detectedTracks.Add(tc);
            }

            if (initialScan)
            {
                foreach (var tc in detectedTracks)
                {
                    _currentTracks.Add(tc);
                    tc.RegisterTrainOccupancy(_train);
                    OnTrackEntered?.Invoke(_train, tc);
                    OnGenericTrackEntered?.Invoke(tc);
                }
                return;
            }

            _newTracks.Clear();
            _leftTracks.Clear();

            foreach (var tc in detectedTracks)
            {
                if (!_currentTracks.Contains(tc))
                    _newTracks.Add(tc);
            }

            foreach (var tc in _currentTracks)
            {
                if (!detectedTracks.Contains(tc))
                    _leftTracks.Add(tc);
            }

            foreach (var tc in _newTracks)
            {
                tc.RegisterTrainOccupancy(_train);
                OnTrackEntered?.Invoke(_train, tc);
                OnGenericTrackEntered?.Invoke(tc);
            }

            foreach (var tc in _leftTracks)
            {
                tc.UnregisterTrainOccupancy(_train);
                OnTrackLeft?.Invoke(_train, tc);
                OnGenericTrackLeft?.Invoke(tc);
            }

            _currentTracks.Clear();
            foreach (var tc in detectedTracks)
                _currentTracks.Add(tc);
        }

        private void ScanSignalsAhead()
        {
            Vector3 origin = GetDetectionOrigin();
            Vector3 dir = GetForwardDirection();

            _closestSignal = null;
            _closestRedSignal = null;
            _closestYellowSignal = null;
            _distanceToClosestSignal = float.MaxValue;
            _distanceToClosestRedSignal = float.MaxValue;
            _distanceToClosestYellowSignal = float.MaxValue;

            RaycastHit[] hits = Physics.RaycastAll(origin, dir, signalScanRange, signalLayerMask);

            foreach (var hit in hits)
            {
                var signal = hit.collider.GetComponentInParent<Signal>();
                if (signal == null) continue;
                if (signal.direction != _train.travelDirection) continue;

                float dist = hit.distance;

                if (dist < _distanceToClosestSignal)
                {
                    _distanceToClosestSignal = dist;
                    _closestSignal = signal;
                }

                if (signal.Aspect == SignalAspect.Red && dist < _distanceToClosestRedSignal)
                {
                    _distanceToClosestRedSignal = dist;
                    _closestRedSignal = signal;
                }

                if (signal.Aspect == SignalAspect.Yellow && dist < _distanceToClosestYellowSignal)
                {
                    _distanceToClosestYellowSignal = dist;
                    _closestYellowSignal = signal;
                }
            }

            float nextFrameMaxDisplacement = CalculateMaxDisplacement();
            float extendedRange = signalScanRange + nextFrameMaxDisplacement + safetyMarginDistance;

            RaycastHit[] wideHits = Physics.SphereCastAll(origin, 1.5f, dir, extendedRange, signalLayerMask);
            foreach (var hit in wideHits)
            {
                var signal = hit.collider.GetComponentInParent<Signal>();
                if (signal == null) continue;
                if (signal.direction != _train.travelDirection) continue;
                if (signal.Aspect != SignalAspect.Red) continue;

                float dist = hit.distance;
                if (dist < _distanceToClosestRedSignal)
                {
                    _distanceToClosestRedSignal = dist;
                    _closestRedSignal = signal;
                }
            }
        }

        private void ScanTrainsAhead()
        {
            Vector3 origin = GetDetectionOrigin();
            Vector3 dir = GetForwardDirection();

            _closestTrainAhead = null;
            _distanceToClosestTrain = float.MaxValue;

            RaycastHit[] hits = Physics.SphereCastAll(origin, 1.5f, dir, signalScanRange, trainLayerMask);

            foreach (var hit in hits)
            {
                var otherTrain = hit.collider.GetComponentInParent<Train>();
                if (otherTrain == null || otherTrain == _train) continue;

                float dist = hit.distance;
                if (dist < _distanceToClosestTrain)
                {
                    _distanceToClosestTrain = dist;
                    _closestTrainAhead = otherTrain;
                }
            }
        }

        private void CheckSweptPathForRedSignals()
        {
            if (_train == null || _train.Speed < 0.1f) return;

            Vector3 currentPos = transform.position;
            float sweptDistance = Vector3.Distance(_previousPosition, currentPos);

            if (sweptDistance < 0.01f) return;

            Vector3 sweepDir = (currentPos - _previousPosition).normalized;
            Vector3 origin = _previousPosition + Vector3.up * 2f;

            float subStepLength = sweptDistance / raycastSubdivisions;

            for (int i = 0; i < raycastSubdivisions; i++)
            {
                Vector3 subOrigin = origin + sweepDir * (subStepLength * i);

                RaycastHit[] hits = Physics.SphereCastAll(subOrigin, 1f, sweepDir, subStepLength + 0.5f, signalLayerMask);

                foreach (var hit in hits)
                {
                    var signal = hit.collider.GetComponentInParent<Signal>();
                    if (signal == null) continue;
                    if (signal.direction != _train.travelDirection) continue;
                    if (signal.Aspect != SignalAspect.Red) continue;

                    float dot = Vector3.Dot(sweepDir, GetForwardDirection());
                    if (dot > 0.3f)
                    {
                        OnRedSignalCrossed?.Invoke(_train, signal);
                        Debug.LogWarning($"[TrackScanner] 列车 {_train.trainId} 在高速运动中穿越了红灯信号机 {signal.signalId}！已触发保护制动。");
                        return;
                    }
                }
            }
        }

        public float CalculateMaxDisplacement()
        {
            if (_train == null) return 0f;
            float speed = _train.Speed;
            float maxDecel = _train.emergencyBrakeDeceleration;
            float fixedDt = Time.fixedDeltaTime * Mathf.Max(Time.timeScale, 1f);
            return speed * fixedDt + 0.5f * maxDecel * fixedDt * fixedDt;
        }

        public bool IsApproachingRedSignal(out float distanceToStopPoint)
        {
            distanceToStopPoint = float.MaxValue;
            if (_closestRedSignal == null) return false;

            distanceToStopPoint = _distanceToClosestRedSignal;
            return true;
        }

        public bool IsTrainAheadInBrakingDistance(float brakingDistance)
        {
            return _closestTrainAhead != null && _distanceToClosestTrain < brakingDistance;
        }

        private Vector3 GetDetectionOrigin()
        {
            if (_train != null && _train.signalDetectionPoint != null)
                return _train.signalDetectionPoint.position;
            return transform.position + Vector3.up * 2f;
        }

        private Vector3 GetForwardDirection()
        {
            if (_train != null)
                return _train.travelDirection == Direction.Up ? transform.forward : -transform.forward;
            return transform.forward;
        }

        public void ForceRescan()
        {
            if (!_initialized) return;
            ScanCurrentTrackOccupancy(false);
            ScanSignalsAhead();
            ScanTrainsAhead();
        }

        public void ClearAllTrackOccupancy()
        {
            foreach (var tc in _currentTracks)
            {
                if (tc != null)
                    tc.UnregisterTrainOccupancy(_train);
            }
            _currentTracks.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            if (_train == null) return;

            Vector3 origin = GetDetectionOrigin();
            Vector3 dir = GetForwardDirection();

            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(origin, dir * signalScanRange);

            if (_closestRedSignal != null)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawLine(origin, _closestRedSignal.transform.position);
            }

            if (_closestYellowSignal != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(origin, _closestYellowSignal.transform.position);
            }

            if (_closestTrainAhead != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(origin, _closestTrainAhead.transform.position);
            }

            Vector3 center = transform.position + Vector3.up * 1f;
            float halfLength = _train != null ? _train.trainLength * 0.5f : 7f;
            Gizmos.color = new Color(0f, 1f, 1f, 0.15f);
            Gizmos.matrix = Matrix4x4.TRS(center, transform.rotation, Vector3.one);
            Gizmos.DrawCube(Vector3.zero, new Vector3(trackOverlapHalfExtent * 2, trackOverlapHeight, halfLength * 2));
            Gizmos.matrix = Matrix4x4.identity;
        }

        public (Signal signal, float distance) GetRedSignalAhead()
        {
            if (_closestRedSignal != null)
            {
                return (_closestRedSignal, _distanceToClosestRedSignal);
            }
            return (null, float.MaxValue);
        }

        public (Train train, float distance)? GetTrainAhead()
        {
            if (_closestTrainAhead != null)
            {
                return (_closestTrainAhead, _distanceToClosestTrain);
            }
            return null;
        }

        public bool IsApproachingRedSignal(float safetyFactor)
        {
            if (_closestRedSignal == null) return false;
            float stopDist = CalculateMaxDisplacement() * safetyFactor;
            return _distanceToClosestRedSignal <= stopDist;
        }

        public bool IsTrainAheadInBrakingDistance(float safetyFactor)
        {
            if (_closestTrainAhead == null) return false;
            float stopDist = CalculateMaxDisplacement() * safetyFactor;
            return _distanceToClosestTrain <= stopDist;
        }

        private float CalculateMaxDisplacement()
        {
            float speed = _train != null ? _train.Speed : 10f;
            float maxDecel = _train != null ? _train.emergencyBrakeDeceleration : 6f;
            float fixedDt = Time.fixedDeltaTime;
            return speed * fixedDt + 0.5f * maxDecel * fixedDt * fixedDt;
        }
    }
}
