using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Components;

namespace RailwayInterlock.Pathfinding
{
    public class DynamicWeightEvaluator
    {
        public WeightConfiguration Config = new WeightConfiguration();

        private readonly Dictionary<string, TrackCircuit> _tracksById;
        private readonly Dictionary<string, SwitchPoint> _switchesById;
        private readonly Dictionary<string, Signal> _signalsById;

        public DynamicWeightEvaluator(
            List<TrackCircuit> tracks,
            List<SwitchPoint> switches,
            List<Signal> signals)
        {
            _tracksById = new Dictionary<string, TrackCircuit>();
            _switchesById = new Dictionary<string, SwitchPoint>();
            _signalsById = new Dictionary<string, Signal>();

            if (tracks != null)
                foreach (var t in tracks)
                    if (t != null) _tracksById[t.trackId] = t;

            if (switches != null)
                foreach (var sw in switches)
                    if (sw != null) _switchesById[sw.switchId] = sw;

            if (signals != null)
                foreach (var sig in signals)
                    if (sig != null) _signalsById[sig.signalId] = sig;
        }

        public float EvaluateEdgeWeight(
            GraphEdge edge,
            Direction trainDirection,
            HashSet<string> excludeTrackIds = null,
            HashSet<string> excludeSwitchIds = null,
            bool emergencyMode = false)
        {
            if (edge == null) return float.MaxValue * 0.1f;

            float weight = edge.BaseWeight;

            switch (edge.Type)
            {
                case GraphEdgeType.Straight:
                    weight *= Config.StraightEdgeBaseWeight;
                    break;
                case GraphEdgeType.NormalSwitch:
                    weight *= Config.NormalSwitchWeight;
                    break;
                case GraphEdgeType.ReverseSwitch:
                    weight *= Config.ReverseSwitchWeight;
                    break;
                case GraphEdgeType.Shunting:
                    weight *= Config.ShuntingEdgeWeight;
                    break;
            }

            if (edge.Direction != trainDirection && !edge.Bidirectional)
            {
                weight += Config.OppositeDirectionPenalty;
            }

            weight = ApplySignalWeights(edge, weight);
            weight = ApplyTrackOccupancyWeights(edge, weight, excludeTrackIds);
            weight = ApplySwitchWeights(edge, weight, excludeSwitchIds);

            if (emergencyMode)
            {
                weight *= Config.EmergencyDiversionExtraMultiplier;
            }

            if (float.IsNaN(weight) || float.IsInfinity(weight))
            {
                weight = float.MaxValue * 0.1f;
            }

            return weight;
        }

        private float ApplySignalWeights(GraphEdge edge, float weight)
        {
            if (string.IsNullOrEmpty(edge.ProtectingSignalId))
                return weight;

            if (!_signalsById.TryGetValue(edge.ProtectingSignalId, out var sig) || sig == null)
                return weight;

            switch (sig.Aspect)
            {
                case SignalAspect.Red:
                    weight += Config.RedSignalWeight;
                    break;
                case SignalAspect.Yellow:
                    weight *= Config.YellowSignalWeightMultiplier;
                    break;
            }

            return weight;
        }

        private float ApplyTrackOccupancyWeights(
            GraphEdge edge,
            float weight,
            HashSet<string> excludeTrackIds)
        {
            string fromTrack = ExtractTrackIdFromNode(edge.FromNodeId);
            string toTrack = ExtractTrackIdFromNode(edge.ToNodeId);

            float ApplyTrackWeight(string trackId)
            {
                if (string.IsNullOrEmpty(trackId)) return 0f;

                if (excludeTrackIds != null && excludeTrackIds.Contains(trackId))
                    return float.MaxValue * 0.05f;

                if (_tracksById.TryGetValue(trackId, out var tc) && tc != null)
                {
                    if (tc.State == TrackState.Occupied)
                    {
                        return weight * Config.OccupiedTrackWeightMultiplier;
                    }
                }
                return 0f;
            }

            float fromW = ApplyTrackWeight(fromTrack);
            float toW = ApplyTrackWeight(toTrack);

            if (fromW > weight * 100f || toW > weight * 100f)
            {
                weight = Mathf.Max(fromW, toW);
            }
            else
            {
                weight += fromW + toW;
            }

            return weight;
        }

        private float ApplySwitchWeights(
            GraphEdge edge,
            float weight,
            HashSet<string> excludeSwitchIds)
        {
            if (string.IsNullOrEmpty(edge.SwitchId))
                return weight;

            if (excludeSwitchIds != null && excludeSwitchIds.Contains(edge.SwitchId))
            {
                weight += Config.BlockedSwitchWeight * 5f;
                return weight;
            }

            if (!_switchesById.TryGetValue(edge.SwitchId, out var sw) || sw == null)
                return weight;

            if (sw.IsMoving)
            {
                weight += Config.MovingSwitchWeight;
            }
            else if (!sw.IsInConsistentPosition())
            {
                weight += Config.BlockedSwitchWeight * 0.5f;
            }

            if ((edge.Type == GraphEdgeType.NormalSwitch &&
                 sw.Position != SwitchPosition.Normal) ||
                (edge.Type == GraphEdgeType.ReverseSwitch &&
                 sw.Position != SwitchPosition.Reverse))
            {
                weight += 10f;
            }

            return weight;
        }

        public bool IsEdgeTraversable(
            GraphEdge edge,
            Direction trainDirection,
            HashSet<string> excludeTrackIds = null,
            HashSet<string> excludeSwitchIds = null)
        {
            if (edge == null) return false;

            if (string.IsNullOrEmpty(edge.ProtectingSignalId) == false)
            {
                if (_signalsById.TryGetValue(edge.ProtectingSignalId, out var sig) &&
                    sig != null && sig.Aspect == SignalAspect.Red)
                {
                    return false;
                }
            }

            string fromTrack = ExtractTrackIdFromNode(edge.FromNodeId);
            string toTrack = ExtractTrackIdFromNode(edge.ToNodeId);

            foreach (var tid in new[] { fromTrack, toTrack })
            {
                if (string.IsNullOrEmpty(tid)) continue;
                if (excludeTrackIds != null && excludeTrackIds.Contains(tid))
                    return false;
                if (_tracksById.TryGetValue(tid, out var tc) &&
                    tc != null && tc.State == TrackState.Occupied)
                {
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(edge.SwitchId))
            {
                if (excludeSwitchIds != null && excludeSwitchIds.Contains(edge.SwitchId))
                    return false;

                if (_switchesById.TryGetValue(edge.SwitchId, out var sw) && sw != null)
                {
                    if (sw.IsOccupied()) return false;
                    if (sw.IsMoving) return false;
                }
            }

            if (edge.Direction != trainDirection && !edge.Bidirectional)
                return false;

            return true;
        }

        public static string ExtractTrackIdFromNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return null;

            const string midTag = "NODE_TRACK_MID_";
            const string fwdTag = "NODE_TRACK_FWD_";
            const string bckTag = "NODE_TRACK_BCK_";

            int idx;
            if ((idx = nodeId.IndexOf(midTag, StringComparison.Ordinal)) >= 0)
                return nodeId.Substring(idx + midTag.Length);
            if ((idx = nodeId.IndexOf(fwdTag, StringComparison.Ordinal)) >= 0)
                return nodeId.Substring(idx + fwdTag.Length);
            if ((idx = nodeId.IndexOf(bckTag, StringComparison.Ordinal)) >= 0)
                return nodeId.Substring(idx + bckTag.Length);

            const string swCommonTag = "NODE_SW_COMMON_";
            const string swNormalTag = "NODE_SW_NORMAL_";
            const string swReverseTag = "NODE_SW_REVERSE_";
            if (nodeId.StartsWith(swCommonTag) ||
                nodeId.StartsWith(swNormalTag) ||
                nodeId.StartsWith(swReverseTag))
            {
                return null;
            }

            return null;
        }

        public float Heuristic(GraphNode a, GraphNode b)
        {
            if (a == null || b == null) return 0f;
            float dist = Vector3.Distance(a.WorldPosition, b.WorldPosition);
            return dist * 0.1f;
        }

        public void UpdateDynamicReferences(
            List<TrackCircuit> tracks,
            List<SwitchPoint> switches,
            List<Signal> signals)
        {
            _tracksById.Clear();
            _switchesById.Clear();
            _signalsById.Clear();

            if (tracks != null)
                foreach (var t in tracks)
                    if (t != null) _tracksById[t.trackId] = t;

            if (switches != null)
                foreach (var sw in switches)
                    if (sw != null) _switchesById[sw.switchId] = sw;

            if (signals != null)
                foreach (var sig in signals)
                    if (sig != null) _signalsById[sig.signalId] = sig;
        }

        public bool IsTrackOccupied(string trackId)
        {
            if (string.IsNullOrEmpty(trackId)) return false;
            return _tracksById.TryGetValue(trackId, out var tc) &&
                   tc != null && tc.State == TrackState.Occupied;
        }

        public SignalAspect GetSignalAspect(string signalId)
        {
            if (string.IsNullOrEmpty(signalId)) return SignalAspect.Red;
            return _signalsById.TryGetValue(signalId, out var sig) && sig != null
                ? sig.Aspect
                : SignalAspect.Red;
        }

        public SwitchPosition? GetSwitchPosition(string switchId)
        {
            if (string.IsNullOrEmpty(switchId)) return null;
            return _switchesById.TryGetValue(switchId, out var sw) && sw != null
                ? sw.Position
                : (SwitchPosition?)null;
        }
    }
}
