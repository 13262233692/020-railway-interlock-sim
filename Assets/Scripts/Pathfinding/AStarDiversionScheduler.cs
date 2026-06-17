using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Components;
using RailwayInterlock.Interlocking;

namespace RailwayInterlock.Pathfinding
{
    [DefaultExecutionOrder(50)]
    public class AStarDiversionScheduler : MonoBehaviour
    {
        [Header("拓扑与寻路")]
        public bool autoBuildTopology = true;
        public float topologyRebuildInterval = 10f;

        [Header("冲突检测")]
        public float conflictScanInterval = 0.5f;
        public float dangerForwardDistance = 150f;
        public float conflictWarningTime = 20f;
        public float minSafeGapTime = 8f;

        [Header("避让策略")]
        public bool enableAutoDiversion = true;
        public int maxKShortestPaths = 3;
        public float diversionDecisionThreshold = 0.6f;

        [Header("调试与可视化")]
        public bool drawComputedPaths = true;
        public bool drawConflictZones = true;
        public float pathLineLifetime = 2f;
        public Color mainPathColor = new Color(0.2f, 0.8f, 0.3f, 1f);
        public Color altPathColor = new Color(0.3f, 0.6f, 1f, 0.6f);
        public Color conflictColor = new Color(1f, 0.2f, 0.2f, 1f);

        [Header("引用")]
        public List<TrackCircuit> trackCircuits = new List<TrackCircuit>();
        public List<SwitchPoint> switchPoints = new List<SwitchPoint>();
        public List<Signal> signals = new List<Signal>();
        public List<Train> normalTrains = new List<Train>();
        public List<OutOfControlEngineer> rogueEngineers = new List<OutOfControlEngineer>();
        public InterlockController interlockController;

        private RailwayTopology _topology;
        private RailwayTopologyBuilder _builder;
        private AStarPathfinder _pathfinder;
        private DynamicWeightEvaluator _weightEvaluator;

        private float _lastTopologyBuildTime;
        private float _lastConflictScanTime;

        private readonly Dictionary<string, AStarPathResult> _activeDiversionPaths =
            new Dictionary<string, AStarPathResult>();
        private readonly Dictionary<string, ConflictReport> _pendingConflicts =
            new Dictionary<string, ConflictReport>();
        private readonly Dictionary<string, Coroutine> _executingDiversions =
            new Dictionary<string, Coroutine>();

        private bool _isInitialized;

        public event Action<string, AStarPathResult> OnDiversionPathComputed;
        public event Action<ConflictReport> OnConflictDetected;
        public event Action<string> OnDiversionExecuted;

        public RailwayTopology Topology => _topology;
        public IReadOnlyDictionary<string, AStarPathResult> ActivePaths => _activeDiversionPaths;
        public IReadOnlyDictionary<string, ConflictReport> PendingConflicts => _pendingConflicts;

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_isInitialized) return;

            _weightEvaluator = new DynamicWeightEvaluator(trackCircuits, switchPoints, signals);

            if (autoBuildTopology)
            {
                RebuildTopology();
            }

            _isInitialized = true;
            Debug.Log("[AStarScheduler] 初始化完成");
        }

        public void RebuildTopology()
        {
            _builder = new RailwayTopologyBuilder(trackCircuits, switchPoints, signals);
            _topology = _builder.Build();
            _pathfinder = new AStarPathfinder(_topology, _weightEvaluator);
            _lastTopologyBuildTime = Time.time;
        }

        public void RefreshEvaluatorReferences()
        {
            _weightEvaluator.UpdateDynamicReferences(trackCircuits, switchPoints, signals);
        }

        private void FixedUpdate()
        {
            if (!_isInitialized || _topology == null) return;

            if (Time.time - _lastTopologyBuildTime > topologyRebuildInterval)
            {
                RebuildTopology();
            }

            if (Time.time - _lastConflictScanTime > conflictScanInterval)
            {
                _lastConflictScanTime = Time.time;
                RefreshEvaluatorReferences();
                ScanForConflicts();
            }

            if (drawComputedPaths)
            {
                DrawActivePaths();
            }
        }

        private void ScanForConflicts()
        {
            _pendingConflicts.Clear();

            foreach (var train in normalTrains)
            {
                if (train == null) continue;

                foreach (var engineer in rogueEngineers)
                {
                    if (engineer == null) continue;

                    var conflict = EvaluatePairwiseConflict(train, engineer);
                    if (conflict.Type != ConflictType.None)
                    {
                        _pendingConflicts[$"{train.trainId}_{engineer.engineerId}"] = conflict;
                        OnConflictDetected?.Invoke(conflict);

                        if (enableAutoDiversion &&
                            !_executingDiversions.ContainsKey(train.trainId))
                        {
                            TryTriggerDiversion(train, conflict);
                        }
                    }
                }

                for (int j = normalTrains.IndexOf(train) + 1; j < normalTrains.Count; j++)
                {
                    var otherTrain = normalTrains[j];
                    if (otherTrain == null) continue;

                    var conflict = EvaluateTrainTrainConflict(train, otherTrain);
                    if (conflict.Type != ConflictType.None)
                    {
                        _pendingConflicts[$"{train.trainId}_{otherTrain.trainId}"] = conflict;
                        OnConflictDetected?.Invoke(conflict);
                    }
                }
            }

            if (drawConflictZones)
            {
                DrawConflictZones();
            }
        }

        private ConflictReport EvaluatePairwiseConflict(Train train, OutOfControlEngineer eng)
        {
            ConflictReport report = new ConflictReport
            {
                SubjectTrainId = train.trainId,
                ObstacleTrainId = eng.engineerId
            };

            Vector3 trainPos = train.transform.position;
            Vector3 engPos = eng.transform.position;

            Vector3 trainFwd = train.travelDirection == Direction.Up
                ? train.transform.forward
                : -train.transform.forward;
            Vector3 engFwd = eng.Direction == Direction.Up
                ? eng.transform.forward
                : -eng.transform.forward;

            float distance = Vector3.Distance(trainPos, engPos);
            if (distance > dangerForwardDistance * 1.5f)
            {
                report.Type = ConflictType.None;
                return report;
            }

            float forwardProjection = Vector3.Dot(engPos - trainPos, trainFwd);
            float sideOffset = Vector3.Cross(engPos - trainPos, trainFwd).magnitude;

            if (forwardProjection < -10f || sideOffset > 12f)
            {
                report.Type = ConflictType.None;
                return report;
            }

            if (distance < 15f)
            {
                report.Type = ConflictType.RearEnd;
                report.DistanceToContact = distance;
                report.EstimatedTimeToContact = 0f;
                return report;
            }

            float relSpeed = Mathf.Max(0.1f, train.Speed - eng.Speed);
            float ttc = forwardProjection / relSpeed;
            report.DistanceToContact = forwardProjection;
            report.EstimatedTimeToContact = ttc;

            if (Vector3.Dot(trainFwd, engFwd) < -0.5f)
            {
                report.Type = ConflictType.HeadOn;
                return report;
            }

            if (ttc < conflictWarningTime && ttc > 0f)
            {
                report.Type = ttc < minSafeGapTime ? ConflictType.Blocked : ConflictType.Overtake;
            }
            else
            {
                report.Type = ConflictType.None;
            }

            return report;
        }

        private ConflictReport EvaluateTrainTrainConflict(Train a, Train b)
        {
            var report = new ConflictReport
            {
                SubjectTrainId = a.trainId,
                ObstacleTrainId = b.trainId
            };

            Vector3 posA = a.transform.position;
            Vector3 posB = b.transform.position;
            float dist = Vector3.Distance(posA, posB);

            if (dist > dangerForwardDistance)
            {
                report.Type = ConflictType.None;
                return report;
            }

            Vector3 fwdA = a.travelDirection == Direction.Up ? a.transform.forward : -a.transform.forward;
            Vector3 fwdB = b.travelDirection == Direction.Up ? b.transform.forward : -b.transform.forward;

            float forwardOnA = Vector3.Dot(posB - posA, fwdA);
            if (forwardOnA < 0f)
            {
                report.Type = ConflictType.None;
                return report;
            }

            float relSpeed = Mathf.Max(0.1f, a.Speed - b.Speed);
            float ttc = forwardOnA / relSpeed;
            report.DistanceToContact = forwardOnA;
            report.EstimatedTimeToContact = ttc;

            if (ttc < minSafeGapTime && ttc > 0f)
            {
                report.Type = Vector3.Dot(fwdA, fwdB) < -0.5f
                    ? ConflictType.HeadOn
                    : ConflictType.RearEnd;
            }
            else if (ttc < conflictWarningTime)
            {
                report.Type = ConflictType.Overtake;
            }

            return report;
        }

        private void TryTriggerDiversion(Train train, ConflictReport conflict)
        {
            if (train == null || _builder == null) return;

            string currentTrackId = FindCurrentTrackIdForTrain(train);
            string goalTrackId = FindDiversionGoalTrack(train, conflict);

            if (string.IsNullOrEmpty(currentTrackId) || string.IsNullOrEmpty(goalTrackId))
                return;

            Direction trainDir = train.travelDirection;
            string startNode = _builder.FindNodeIdByTrackId(currentTrackId, trainDir);
            string goalNode = _builder.FindNodeIdByTrackId(goalTrackId, trainDir);

            if (string.IsNullOrEmpty(startNode) || string.IsNullOrEmpty(goalNode))
                return;

            HashSet<string> excludeTracks = new HashSet<string>();
            foreach (var eng in rogueEngineers)
            {
                if (eng == null) continue;
                foreach (var t in eng.OccupiedTrackIds)
                    excludeTracks.Add(t);
            }

            if (!string.IsNullOrEmpty(conflict.ConflictTrackId))
                excludeTracks.Add(conflict.ConflictTrackId);

            var candidatePaths = _pathfinder.FindKShortestPaths(
                startNode, goalNode, trainDir,
                maxKShortestPaths, excludeTracks, null,
                conflict.Type == ConflictType.HeadOn || conflict.Type == ConflictType.RearEnd);

            if (candidatePaths.Count == 0)
            {
                Debug.LogWarning($"[AStarScheduler] {train.trainId} 无可用避让路径");
                return;
            }

            AStarPathResult selected = SelectBestDiversionPath(train, candidatePaths, conflict);
            if (selected == null) return;

            _activeDiversionPaths[train.trainId] = selected;
            OnDiversionPathComputed?.Invoke(train.trainId, selected);

            Debug.Log($"[AStarScheduler] {train.trainId} 避让路径计算完成, " +
                      $"共 {candidatePaths.Count} 条候选, 选择权重={selected.TotalWeight:F2}");

            if (_executingDiversions.TryGetValue(train.trainId, out var existing) && existing != null)
            {
                StopCoroutine(existing);
            }

            _executingDiversions[train.trainId] = StartCoroutine(ExecuteDiversionRoutine(train, selected));
        }

        private string FindCurrentTrackIdForTrain(Train train)
        {
            foreach (var tc in trackCircuits)
            {
                if (tc == null || !tc.IsTrainRegistered(train)) continue;
                return tc.trackId;
            }

            float bestDist = float.MaxValue;
            string bestId = null;
            foreach (var tc in trackCircuits)
            {
                if (tc == null) continue;
                float d = Vector3.Distance(train.transform.position, tc.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    bestId = tc.trackId;
                }
            }
            return bestId;
        }

        private string FindDiversionGoalTrack(Train train, ConflictReport conflict)
        {
            HashSet<string> blockedTracks = new HashSet<string>();
            foreach (var eng in rogueEngineers)
            {
                if (eng == null) continue;
                foreach (var t in eng.OccupiedTrackIds) blockedTracks.Add(t);
            }

            string engCurrentTrack = null;
            foreach (var eng in rogueEngineers)
            {
                if (eng != null) engCurrentTrack = eng.CurrentTrackId;
            }

            int sameLaneCount = 0;
            int targetLane = -1;

            for (int lane = 1; lane <= 3; lane++)
            {
                bool laneBlocked = false;
                string[] laneTracks = { $"T{lane}-1", $"T{lane}-2", $"T{lane}-3", $"T{lane}-4" };
                foreach (var t in laneTracks)
                {
                    if (blockedTracks.Contains(t) || t == engCurrentTrack)
                    {
                        laneBlocked = true;
                        break;
                    }
                }
                if (!laneBlocked)
                {
                    if (targetLane < 0) targetLane = lane;
                }
                else
                {
                    sameLaneCount++;
                }
            }

            if (targetLane < 0) targetLane = 1;
            if (train.travelDirection == Direction.Down) targetLane = 3 - targetLane + 1;

            return $"T{targetLane}-4";
        }

        private AStarPathResult SelectBestDiversionPath(
            Train train,
            List<AStarPathResult> candidates,
            ConflictReport conflict)
        {
            if (candidates.Count == 0) return null;

            float urgencyScore = Mathf.Clamp01(
                1f - (conflict.EstimatedTimeToContact / Mathf.Max(conflictWarningTime, 1f)));

            AStarPathResult best = null;
            float bestScore = float.MaxValue;

            foreach (var p in candidates)
            {
                float score = p.TotalWeight * (1f - urgencyScore * 0.4f) +
                              p.RequiredSwitchPositions.Count * 5f * (1f - urgencyScore * 0.6f) +
                              p.EstimatedTravelTime * 0.3f;

                if (score < bestScore)
                {
                    bestScore = score;
                    best = p;
                }
            }

            return best;
        }

        private IEnumerator ExecuteDiversionRoutine(Train train, AStarPathResult path)
        {
            yield return null;
            OnDiversionExecuted?.Invoke(train.trainId);

            foreach (var kvp in path.RequiredSwitchPositions)
            {
                if (interlockController == null) continue;

                SwitchPoint sw = null;
                foreach (var s in switchPoints)
                {
                    if (s != null && s.switchId == kvp.Key)
                    {
                        sw = s;
                        break;
                    }
                }

                if (sw != null && sw.Position != kvp.Value)
                {
                    sw.SetPosition(kvp.Value);
                }

                yield return new WaitForSeconds(0.1f);
            }

            float timeout = 15f;
            float startTime = Time.time;
            bool allSet = false;

            while (Time.time - startTime < timeout)
            {
                allSet = true;
                foreach (var kvp in path.RequiredSwitchPositions)
                {
                    SwitchPoint sw = null;
                    foreach (var s in switchPoints)
                    {
                        if (s != null && s.switchId == kvp.Key) { sw = s; break; }
                    }
                    if (sw == null || !sw.IsInConsistentPosition() || sw.Position != kvp.Value)
                    {
                        allSet = false;
                        break;
                    }
                }
                if (allSet) break;
                yield return new WaitForSeconds(0.2f);
            }

            _executingDiversions.Remove(train.trainId);
        }

        public AStarPathResult ComputePathBetweenTracks(
            string startTrackId, string goalTrackId, Direction dir,
            HashSet<string> excludeTracks = null,
            HashSet<string> excludeSwitches = null)
        {
            if (_builder == null || _pathfinder == null) return null;

            string start = _builder.FindNodeIdByTrackId(startTrackId, dir);
            string goal = _builder.FindNodeIdByTrackId(goalTrackId,
                dir == Direction.Up ? Direction.Down : Direction.Up);

            return _pathfinder.FindPath(start, goal, dir, excludeTracks, excludeSwitches);
        }

        public Vector3 GetNodeWorldPosition(string nodeId)
        {
            if (_topology != null &&
                _topology.Nodes.TryGetValue(nodeId, out var node))
            {
                return node.WorldPosition + Vector3.up * 1.5f;
            }
            return Vector3.zero;
        }

        private void DrawActivePaths()
        {
            foreach (var kvp in _activeDiversionPaths)
            {
                DrawPath(kvp.Value, mainPathColor, 2.5f);
            }
        }

        private void DrawPath(AStarPathResult path, Color color, float width)
        {
            if (path == null || _topology == null) return;

            for (int i = 0; i < path.NodeSequence.Count - 1; i++)
            {
                string aId = path.NodeSequence[i];
                string bId = path.NodeSequence[i + 1];

                if (!_topology.Nodes.TryGetValue(aId, out var a) ||
                    !_topology.Nodes.TryGetValue(bId, out var b))
                    continue;

                Vector3 va = a.WorldPosition + Vector3.up * 1.8f;
                Vector3 vb = b.WorldPosition + Vector3.up * 1.8f;

                Debug.DrawLine(va, vb, color, pathLineLifetime);

                if (i % 2 == 0)
                {
                    Vector3 mid = (va + vb) * 0.5f;
                    Vector3 dir = (vb - va).normalized;
                    Vector3 arrow = Quaternion.Euler(0, 30f, 0) * -dir * 0.6f;
                    Debug.DrawLine(mid, mid + arrow, color, pathLineLifetime);
                }
            }

            if (path.NodeSequence.Count > 0 &&
                _topology.Nodes.TryGetValue(path.NodeSequence[0], out var first))
            {
                Debug.DrawRay(first.WorldPosition + Vector3.up * 1.8f,
                    Vector3.up * 1.2f, Color.green, pathLineLifetime);
            }

            if (path.NodeSequence.Count > 0 &&
                _topology.Nodes.TryGetValue(path.NodeSequence[path.NodeSequence.Count - 1], out var last))
            {
                Debug.DrawRay(last.WorldPosition + Vector3.up * 1.8f,
                    Vector3.up * 1.2f, Color.magenta, pathLineLifetime);
            }
        }

        private void DrawConflictZones()
        {
            foreach (var kvp in _pendingConflicts)
            {
                var report = kvp.Value;
                Train subject = null;
                foreach (var t in normalTrains)
                {
                    if (t != null && t.trainId == report.SubjectTrainId)
                    {
                        subject = t;
                        break;
                    }
                }

                if (subject == null) continue;

                Vector3 start = subject.transform.position + Vector3.up * 1.2f;
                Vector3 dir = subject.travelDirection == Direction.Up
                    ? subject.transform.forward
                    : -subject.transform.forward;

                float warnDist = Mathf.Min(report.DistanceToContact * 0.8f, dangerForwardDistance);
                Debug.DrawRay(start, dir * warnDist,
                    new Color(conflictColor.r, conflictColor.g, conflictColor.b, 0.5f),
                    conflictScanInterval * 1.2f);

                Vector3 arcCenter = start + dir * (warnDist * 0.7f);
                for (int i = 0; i < 12; i++)
                {
                    float a1 = i / 12f * Mathf.PI * 2f;
                    float a2 = (i + 1) / 12f * Mathf.PI * 2f;
                    Vector3 v1 = arcCenter + new Vector3(
                        Mathf.Cos(a1) * 2.5f, 0.5f, Mathf.Sin(a1) * 2.5f);
                    Vector3 v2 = arcCenter + new Vector3(
                        Mathf.Cos(a2) * 2.5f, 0.5f, Mathf.Sin(a2) * 2.5f);
                    Debug.DrawLine(v1, v2, conflictColor, conflictScanInterval * 1.2f);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!_isInitialized || _topology == null) return;

            Gizmos.color = new Color(0.3f, 0.5f, 1f, 0.15f);
            foreach (var kvp in _topology.Nodes)
            {
                Gizmos.DrawSphere(kvp.Value.WorldPosition + Vector3.up * 1.5f, 0.3f);
            }

            Gizmos.color = new Color(0.5f, 0.5f, 0.7f, 0.15f);
            foreach (var kvp in _topology.Edges)
            {
                var edge = kvp.Value;
                if (_topology.Nodes.TryGetValue(edge.FromNodeId, out var a) &&
                    _topology.Nodes.TryGetValue(edge.ToNodeId, out var b))
                {
                    Gizmos.DrawLine(
                        a.WorldPosition + Vector3.up * 1.5f,
                        b.WorldPosition + Vector3.up * 1.5f);
                }
            }
        }

        public void ClearActivePaths()
        {
            foreach (var kvp in _executingDiversions)
            {
                if (kvp.Value != null) StopCoroutine(kvp.Value);
            }
            _executingDiversions.Clear();
            _activeDiversionPaths.Clear();
            _pendingConflicts.Clear();
        }
    }
}
