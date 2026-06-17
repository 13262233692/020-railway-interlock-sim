using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Components;

namespace RailwayInterlock.Pathfinding
{
    public class RailwayTopologyBuilder
    {
        private readonly List<TrackCircuit> _tracks;
        private readonly List<SwitchPoint> _switches;
        private readonly List<Signal> _signals;
        private readonly RailwayTopology _topology = new RailwayTopology();

        private readonly Dictionary<string, Vector3> _trackEndPointForward = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Vector3> _trackEndPointBackward = new Dictionary<string, Vector3>();
        private readonly Dictionary<string, Vector3> _trackMidPoint = new Dictionary<string, Vector3>();

        public RailwayTopologyBuilder(
            List<TrackCircuit> tracks,
            List<SwitchPoint> switches,
            List<Signal> signals)
        {
            _tracks = tracks ?? new List<TrackCircuit>();
            _switches = switches ?? new List<SwitchPoint>();
            _signals = signals ?? new List<Signal>();
        }

        public RailwayTopology Build()
        {
            _topology.Nodes.Clear();
            _topology.Edges.Clear();
            _topology.Adjacency.Clear();

            CalculateTrackEndpoints();
            CreateTrackNodes();
            CreateSwitchNodes();
            CreateSignalNodes();
            CreateStraightEdges();
            CreateSwitchEdges();
            CreateSignalProtectionEdges();
            BuildAdjacencyTable();

            Debug.Log($"[TopologyBuilder] 拓扑图构建完成: " +
                      $"{_topology.Nodes.Count} 节点, " +
                      $"{_topology.Edges.Count} 边, " +
                      $"{_topology.Adjacency.Count} 邻接");

            return _topology;
        }

        private void CalculateTrackEndpoints()
        {
            foreach (var tc in _tracks)
            {
                if (tc == null) continue;
                Transform t = tc.transform;
                BoxCollider col = tc.GetComponent<BoxCollider>();
                float halfLen = col != null ? col.size.z * 0.5f : tc.length * 0.5f;

                Vector3 forwardEnd = t.position + t.forward * halfLen;
                Vector3 backwardEnd = t.position - t.forward * halfLen;

                _trackEndPointForward[tc.trackId] = forwardEnd;
                _trackEndPointBackward[tc.trackId] = backwardEnd;
                _trackMidPoint[tc.trackId] = t.position;

                if (tc.adjacentTrackIds == null)
                    tc.adjacentTrackIds = new List<string>();
            }
        }

        private void CreateTrackNodes()
        {
            foreach (var tc in _tracks)
            {
                if (tc == null) continue;

                GraphNode midNode = new GraphNode
                {
                    Id = $"NODE_TRACK_MID_{tc.trackId}",
                    Type = GraphNodeType.TrackCircuit,
                    TrackId = tc.trackId,
                    WorldPosition = _trackMidPoint[tc.trackId]
                };
                _topology.Nodes[midNode.Id] = midNode;

                GraphNode forwardNode = new GraphNode
                {
                    Id = $"NODE_TRACK_FWD_{tc.trackId}",
                    Type = GraphNodeType.TrackCircuit,
                    TrackId = tc.trackId,
                    WorldPosition = _trackEndPointForward[tc.trackId]
                };
                _topology.Nodes[forwardNode.Id] = forwardNode;

                GraphNode backwardNode = new GraphNode
                {
                    Id = $"NODE_TRACK_BCK_{tc.trackId}",
                    Type = GraphNodeType.TrackCircuit,
                    TrackId = tc.trackId,
                    WorldPosition = _trackEndPointBackward[tc.trackId]
                };
                _topology.Nodes[backwardNode.Id] = backwardNode;
            }
        }

        private void CreateSwitchNodes()
        {
            foreach (var sw in _switches)
            {
                if (sw == null) continue;

                GraphNode commonNode = new GraphNode
                {
                    Id = $"NODE_SW_COMMON_{sw.switchId}",
                    Type = GraphNodeType.SwitchCommon,
                    SwitchId = sw.switchId,
                    TrackId = sw.commonTrackId,
                    WorldPosition = sw.transform.position
                };
                _topology.Nodes[commonNode.Id] = commonNode;

                GraphNode normalNode = new GraphNode
                {
                    Id = $"NODE_SW_NORMAL_{sw.switchId}",
                    Type = GraphNodeType.SwitchNormal,
                    SwitchId = sw.switchId,
                    TrackId = sw.normalTrackId,
                    WorldPosition = sw.transform.position
                };
                _topology.Nodes[normalNode.Id] = normalNode;

                GraphNode reverseNode = new GraphNode
                {
                    Id = $"NODE_SW_REVERSE_{sw.switchId}",
                    Type = GraphNodeType.SwitchReverse,
                    SwitchId = sw.switchId,
                    TrackId = sw.reverseTrackId,
                    WorldPosition = sw.transform.position
                };
                _topology.Nodes[reverseNode.Id] = reverseNode;
            }
        }

        private void CreateSignalNodes()
        {
            foreach (var sig in _signals)
            {
                if (sig == null) continue;

                GraphNode signalNode = new GraphNode
                {
                    Id = $"NODE_SIGNAL_{sig.signalId}",
                    Type = GraphNodeType.SignalStation,
                    SignalId = sig.signalId,
                    TrackId = sig.protectingTrackId,
                    WorldPosition = sig.transform.position
                };
                _topology.Nodes[signalNode.Id] = signalNode;
            }
        }

        private void CreateStraightEdges()
        {
            float connectThreshold = 6f;

            for (int i = 0; i < _tracks.Count; i++)
            {
                var tA = _tracks[i];
                if (tA == null) continue;

                for (int j = i + 1; j < _tracks.Count; j++)
                {
                    var tB = _tracks[j];
                    if (tB == null) continue;

                    TryConnectTrackEnds(
                        tA, tB,
                        _trackEndPointForward[tA.trackId],
                        _trackEndPointBackward[tB.trackId],
                        $"NODE_TRACK_FWD_{tA.trackId}",
                        $"NODE_TRACK_BCK_{tB.trackId}",
                        connectThreshold,
                        Direction.Up);

                    TryConnectTrackEnds(
                        tB, tA,
                        _trackEndPointForward[tB.trackId],
                        _trackEndPointBackward[tA.trackId],
                        $"NODE_TRACK_FWD_{tB.trackId}",
                        $"NODE_TRACK_BCK_{tA.trackId}",
                        connectThreshold,
                        Direction.Down);
                }

                CreateInternalTrackEdges(tA);
            }
        }

        private void TryConnectTrackEnds(
            TrackCircuit tA, TrackCircuit tB,
            Vector3 endA, Vector3 endB,
            string nodeFromId, string nodeToId,
            float threshold, Direction edgeDir)
        {
            float dist = Vector3.Distance(endA, endB);
            if (dist > threshold) return;

            if (!_topology.Nodes.ContainsKey(nodeFromId) || !_topology.Nodes.ContainsKey(nodeToId))
                return;

            bool alreadyConnectedBySwitch = false;
            foreach (var sw in _switches)
            {
                if (sw == null) continue;
                if ((sw.commonTrackId == tA.trackId &&
                     (sw.normalTrackId == tB.trackId || sw.reverseTrackId == tB.trackId)) ||
                    (sw.commonTrackId == tB.trackId &&
                     (sw.normalTrackId == tA.trackId || sw.reverseTrackId == tA.trackId)))
                {
                    alreadyConnectedBySwitch = true;
                    break;
                }
            }
            if (alreadyConnectedBySwitch) return;

            string edgeId = $"EDGE_STRAIGHT_{tA.trackId}_TO_{tB.trackId}";
            if (_topology.Edges.ContainsKey(edgeId)) return;

            GraphEdge edge = new GraphEdge
            {
                Id = edgeId,
                FromNodeId = nodeFromId,
                ToNodeId = nodeToId,
                Type = GraphEdgeType.Straight,
                BaseWeight = Mathf.Max(dist * 0.1f, 1f),
                Direction = edgeDir,
                Length = dist,
                Bidirectional = true
            };
            _topology.Edges[edgeId] = edge;

            if (!tA.adjacentTrackIds.Contains(tB.trackId))
                tA.adjacentTrackIds.Add(tB.trackId);
            if (!tB.adjacentTrackIds.Contains(tA.trackId))
                tB.adjacentTrackIds.Add(tA.trackId);
        }

        private void CreateInternalTrackEdges(TrackCircuit tc)
        {
            string midId = $"NODE_TRACK_MID_{tc.trackId}";
            string fwdId = $"NODE_TRACK_FWD_{tc.trackId}";
            string bckId = $"NODE_TRACK_BCK_{tc.trackId}";

            AddEdgeIfNotExists(
                $"EDGE_INT_{tc.trackId}_FWD",
                bckId, midId,
                GraphEdgeType.Straight, tc.length * 0.25f,
                Direction.Up, null);

            AddEdgeIfNotExists(
                $"EDGE_INT_{tc.trackId}_FWD2",
                midId, fwdId,
                GraphEdgeType.Straight, tc.length * 0.25f,
                Direction.Up, null);

            AddEdgeIfNotExists(
                $"EDGE_INT_{tc.trackId}_BCK",
                fwdId, midId,
                GraphEdgeType.Straight, tc.length * 0.25f,
                Direction.Down, null);

            AddEdgeIfNotExists(
                $"EDGE_INT_{tc.trackId}_BCK2",
                midId, bckId,
                GraphEdgeType.Straight, tc.length * 0.25f,
                Direction.Down, null);
        }

        private void CreateSwitchEdges()
        {
            foreach (var sw in _switches)
            {
                if (sw == null) continue;

                string commonNodeId = $"NODE_SW_COMMON_{sw.switchId}";
                string normalNodeId = $"NODE_SW_NORMAL_{sw.switchId}";
                string reverseNodeId = $"NODE_SW_REVERSE_{sw.switchId}";

                if (!_topology.Nodes.ContainsKey(commonNodeId) ||
                    !_topology.Nodes.ContainsKey(normalNodeId) ||
                    !_topology.Nodes.ContainsKey(reverseNodeId))
                    continue;

                ConnectSwitchToTrackEnd(
                    sw, commonNodeId, sw.commonTrackId,
                    "COMMON");

                ConnectSwitchToTrackEnd(
                    sw, normalNodeId, sw.normalTrackId,
                    "NORMAL");

                ConnectSwitchToTrackEnd(
                    sw, reverseNodeId, sw.reverseTrackId,
                    "REVERSE");

                AddEdgeIfNotExists(
                    $"EDGE_SW_{sw.switchId}_NORMAL_FWD",
                    commonNodeId, normalNodeId,
                    GraphEdgeType.NormalSwitch, 1.2f,
                    Direction.Up, sw, SwitchPosition.Normal);

                AddEdgeIfNotExists(
                    $"EDGE_SW_{sw.switchId}_NORMAL_BCK",
                    normalNodeId, commonNodeId,
                    GraphEdgeType.NormalSwitch, 1.2f,
                    Direction.Down, sw, SwitchPosition.Normal);

                AddEdgeIfNotExists(
                    $"EDGE_SW_{sw.switchId}_REVERSE_FWD",
                    commonNodeId, reverseNodeId,
                    GraphEdgeType.ReverseSwitch, 1.8f,
                    Direction.Up, sw, SwitchPosition.Reverse);

                AddEdgeIfNotExists(
                    $"EDGE_SW_{sw.switchId}_REVERSE_BCK",
                    reverseNodeId, commonNodeId,
                    GraphEdgeType.ReverseSwitch, 1.8f,
                    Direction.Down, sw, SwitchPosition.Reverse);
            }
        }

        private void ConnectSwitchToTrackEnd(
            SwitchPoint sw, string switchNodeId,
            string trackId, string role)
        {
            if (string.IsNullOrEmpty(trackId) ||
                !_trackEndPointForward.ContainsKey(trackId) ||
                !_trackEndPointBackward.ContainsKey(trackId))
                return;

            Vector3 swPos = sw.transform.position;
            Vector3 fwd = _trackEndPointForward[trackId];
            Vector3 bck = _trackEndPointBackward[trackId];

            string targetNodeId;
            float dist;

            if (Vector3.Distance(swPos, fwd) <= Vector3.Distance(swPos, bck))
            {
                targetNodeId = $"NODE_TRACK_FWD_{trackId}";
                dist = Vector3.Distance(swPos, fwd);
            }
            else
            {
                targetNodeId = $"NODE_TRACK_BCK_{trackId}";
                dist = Vector3.Distance(swPos, bck);
            }

            if (!_topology.Nodes.ContainsKey(targetNodeId)) return;

            AddEdgeIfNotExists(
                $"EDGE_SWCONN_{sw.switchId}_{role}_FWD",
                targetNodeId, switchNodeId,
                GraphEdgeType.Straight, Mathf.Max(dist * 0.1f, 0.5f),
                Direction.Up, null);

            AddEdgeIfNotExists(
                $"EDGE_SWCONN_{sw.switchId}_{role}_BCK",
                switchNodeId, targetNodeId,
                GraphEdgeType.Straight, Mathf.Max(dist * 0.1f, 0.5f),
                Direction.Down, null);
        }

        private void CreateSignalProtectionEdges()
        {
            foreach (var sig in _signals)
            {
                if (sig == null || string.IsNullOrEmpty(sig.protectingTrackId))
                    continue;

                string signalNodeId = $"NODE_SIGNAL_{sig.signalId}";
                string trackId = sig.protectingTrackId;

                if (!_topology.Nodes.ContainsKey(signalNodeId)) continue;
                if (!_trackEndPointForward.ContainsKey(trackId) ||
                    !_trackEndPointBackward.ContainsKey(trackId) ||
                    !_trackMidPoint.ContainsKey(trackId))
                    continue;

                Vector3 sigPos = sig.transform.position;
                Vector3 fwd = _trackEndPointForward[trackId];
                Vector3 bck = _trackEndPointBackward[trackId];
                Vector3 mid = _trackMidPoint[trackId];

                float dFwd = Vector3.Distance(sigPos, fwd);
                float dBck = Vector3.Distance(sigPos, bck);
                float dMid = Vector3.Distance(sigPos, mid);

                string nearestNodeId;
                float nearestDist;

                if (dFwd <= dBck && dFwd <= dMid)
                {
                    nearestNodeId = $"NODE_TRACK_FWD_{trackId}";
                    nearestDist = dFwd;
                }
                else if (dBck <= dMid)
                {
                    nearestNodeId = $"NODE_TRACK_BCK_{trackId}";
                    nearestDist = dBck;
                }
                else
                {
                    nearestNodeId = $"NODE_TRACK_MID_{trackId}";
                    nearestDist = dMid;
                }

                if (!_topology.Nodes.ContainsKey(nearestNodeId)) continue;

                GraphEdge protectiveEdge = new GraphEdge
                {
                    Id = $"EDGE_SIGPROTECT_{sig.signalId}",
                    FromNodeId = nearestNodeId,
                    ToNodeId = signalNodeId,
                    Type = GraphEdgeType.Straight,
                    BaseWeight = 0.5f,
                    Direction = sig.direction,
                    ProtectingSignalId = sig.signalId,
                    Length = nearestDist,
                    Bidirectional = false
                };
                _topology.Edges[protectiveEdge.Id] = protectiveEdge;
            }
        }

        private void AddEdgeIfNotExists(
            string id,
            string fromId, string toId,
            GraphEdgeType type, float weight,
            Direction dir, SwitchPoint sw,
            SwitchPosition? requiredPos = null)
        {
            if (_topology.Edges.ContainsKey(id)) return;
            if (!_topology.Nodes.ContainsKey(fromId) ||
                !_topology.Nodes.ContainsKey(toId))
                return;

            GraphEdge edge = new GraphEdge
            {
                Id = id,
                FromNodeId = fromId,
                ToNodeId = toId,
                Type = type,
                BaseWeight = weight,
                Direction = dir,
                SwitchId = sw != null ? sw.switchId : null,
                RequiredSwitchPosition = requiredPos ?? SwitchPosition.Normal,
                Length = weight * 5f,
                Bidirectional = type == GraphEdgeType.Straight
            };
            _topology.Edges[id] = edge;
        }

        private void BuildAdjacencyTable()
        {
            foreach (var kvp in _topology.Nodes)
            {
                _topology.Adjacency[kvp.Key] = new List<GraphEdge>();
            }

            foreach (var kvp in _topology.Edges)
            {
                GraphEdge edge = kvp.Value;
                if (_topology.Adjacency.TryGetValue(edge.FromNodeId, out var list))
                {
                    list.Add(edge);
                }
            }
        }

        public string FindNearestNodeId(Vector3 worldPos, Direction preferDir)
        {
            string bestId = null;
            float bestDist = float.MaxValue;

            foreach (var kvp in _topology.Nodes)
            {
                GraphNode n = kvp.Value;
                float d = Vector3.Distance(n.WorldPosition, worldPos);

                bool dirMatch = true;
                if (preferDir == Direction.Up && n.Id.Contains("_BCK_")) dirMatch = true;
                if (preferDir == Direction.Down && n.Id.Contains("_FWD_")) dirMatch = true;

                float score = d * (dirMatch ? 1f : 1.5f);

                if (score < bestDist)
                {
                    bestDist = score;
                    bestId = kvp.Key;
                }
            }

            return bestId;
        }

        public string FindNodeIdByTrackId(string trackId, Direction enterDir)
        {
            if (string.IsNullOrEmpty(trackId)) return null;

            if (enterDir == Direction.Up)
                return $"NODE_TRACK_BCK_{trackId}";
            else
                return $"NODE_TRACK_FWD_{trackId}";
        }

        public Dictionary<string, Vector3> GetTrackMidPoints() => _trackMidPoint;
    }
}
