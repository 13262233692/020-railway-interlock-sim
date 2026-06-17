using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;

namespace RailwayInterlock.Pathfinding
{
    public class AStarPathfinder
    {
        private readonly RailwayTopology _topology;
        private readonly DynamicWeightEvaluator _evaluator;

        private const int MAX_ITERATIONS = 5000;

        public AStarPathfinder(RailwayTopology topology, DynamicWeightEvaluator evaluator)
        {
            _topology = topology ?? throw new ArgumentNullException(nameof(topology));
            _evaluator = evaluator ?? throw new ArgumentNullException(nameof(evaluator));
        }

        public AStarPathResult FindPath(
            string startNodeId,
            string goalNodeId,
            Direction trainDirection,
            HashSet<string> excludeTrackIds = null,
            HashSet<string> excludeSwitchIds = null,
            bool emergencyMode = false,
            List<string> preferredTrackIds = null)
        {
            AStarPathResult result = new AStarPathResult
            {
                Status = PathStatus.Searching
            };

            if (string.IsNullOrEmpty(startNodeId) || string.IsNullOrEmpty(goalNodeId))
            {
                result.Status = PathStatus.NoPath;
                Debug.LogWarning("[AStar] 起终点节点为空");
                return result;
            }

            if (!_topology.Nodes.ContainsKey(startNodeId) || !_topology.Nodes.ContainsKey(goalNodeId))
            {
                result.Status = PathStatus.NoPath;
                Debug.LogWarning($"[AStar] 节点不存在: start={startNodeId} goal={goalNodeId}");
                return result;
            }

            if (startNodeId == goalNodeId)
            {
                result.NodeSequence.Add(startNodeId);
                result.TrackSequence.Add(
                    DynamicWeightEvaluator.ExtractTrackIdFromNode(startNodeId));
                result.Status = PathStatus.Found;
                result.TotalWeight = 0f;
                return result;
            }

            GraphNode startNode = _topology.Nodes[startNodeId];
            GraphNode goalNode = _topology.Nodes[goalNodeId];

            PriorityQueue<AStarNode> openSet = new PriorityQueue<AStarNode>();
            Dictionary<string, AStarNode> allNodes = new Dictionary<string, AStarNode>();
            HashSet<string> closedSet = new HashSet<string>();

            AStarNode startAStar = new AStarNode
            {
                NodeId = startNodeId,
                G = 0f,
                H = _evaluator.Heuristic(startNode, goalNode),
                Parent = null,
                TraversedEdge = null
            };
            openSet.Enqueue(startAStar);
            allNodes[startNodeId] = startAStar;

            int iteration = 0;
            bool pathFound = false;
            AStarNode finalNode = null;

            while (openSet.Count > 0 && iteration < MAX_ITERATIONS)
            {
                iteration++;
                AStarNode current = openSet.Dequeue();

                if (closedSet.Contains(current.NodeId))
                    continue;
                closedSet.Add(current.NodeId);

                if (current.NodeId == goalNodeId)
                {
                    pathFound = true;
                    finalNode = current;
                    break;
                }

                if (!_topology.Adjacency.TryGetValue(current.NodeId, out var edges))
                    continue;

                foreach (var edge in edges)
                {
                    if (edge == null) continue;
                    if (closedSet.Contains(edge.ToNodeId)) continue;

                    if (!_evaluator.IsEdgeTraversable(
                            edge, trainDirection, excludeTrackIds, excludeSwitchIds))
                    {
                        continue;
                    }

                    float edgeWeight = _evaluator.EvaluateEdgeWeight(
                        edge, trainDirection, excludeTrackIds, excludeSwitchIds, emergencyMode);

                    if (preferredTrackIds != null && preferredTrackIds.Count > 0)
                    {
                        string toTrack = DynamicWeightEvaluator.ExtractTrackIdFromNode(edge.ToNodeId);
                        if (!string.IsNullOrEmpty(toTrack) && preferredTrackIds.Contains(toTrack))
                        {
                            edgeWeight *= 0.7f;
                        }
                    }

                    if (float.IsInfinity(edgeWeight) || edgeWeight >= float.MaxValue * 0.05f)
                        continue;

                    float tentativeG = current.G + edgeWeight;

                    if (!allNodes.TryGetValue(edge.ToNodeId, out var neighbor) ||
                        tentativeG < neighbor.G)
                    {
                        if (!_topology.Nodes.TryGetValue(edge.ToNodeId, out var toGraphNode))
                            continue;

                        AStarNode newNode = new AStarNode
                        {
                            NodeId = edge.ToNodeId,
                            G = tentativeG,
                            H = _evaluator.Heuristic(toGraphNode, goalNode),
                            Parent = current,
                            TraversedEdge = edge
                        };

                        allNodes[edge.ToNodeId] = newNode;
                        openSet.Enqueue(newNode);
                    }
                }
            }

            if (!pathFound || finalNode == null)
            {
                result.Status = PathStatus.NoPath;
                Debug.LogWarning($"[AStar] 未找到路径 (迭代{iteration}次, start={startNodeId}, goal={goalNodeId})");
                return result;
            }

            ReconstructPath(finalNode, result, trainDirection);
            result.Status = PathStatus.Found;

            return result;
        }

        private void ReconstructPath(
            AStarNode finalNode,
            AStarPathResult result,
            Direction trainDir)
        {
            LinkedList<string> nodeSeq = new LinkedList<string>();
            LinkedList<GraphEdge> edgeSeq = new LinkedList<GraphEdge>();
            HashSet<string> trackSet = new HashSet<string>();
            float totalLen = 0f;

            AStarNode cur = finalNode;
            while (cur != null)
            {
                nodeSeq.AddFirst(cur.NodeId);
                if (cur.TraversedEdge != null)
                {
                    edgeSeq.AddFirst(cur.TraversedEdge);
                    totalLen += cur.TraversedEdge.Length;

                    if (!string.IsNullOrEmpty(cur.TraversedEdge.SwitchId) &&
                        !result.RequiredSwitchPositions.ContainsKey(cur.TraversedEdge.SwitchId))
                    {
                        result.RequiredSwitchPositions[cur.TraversedEdge.SwitchId] =
                            cur.TraversedEdge.RequiredSwitchPosition;
                    }

                    if (!string.IsNullOrEmpty(cur.TraversedEdge.ProtectingSignalId) &&
                        !result.PassedSignalIds.Contains(cur.TraversedEdge.ProtectingSignalId))
                    {
                        result.PassedSignalIds.Add(cur.TraversedEdge.ProtectingSignalId);
                    }
                }

                string trackId = DynamicWeightEvaluator.ExtractTrackIdFromNode(cur.NodeId);
                if (!string.IsNullOrEmpty(trackId) && trackSet.Add(trackId))
                {
                    result.TrackSequence.Insert(0, trackId);
                }

                cur = cur.Parent;
            }

            result.NodeSequence.AddRange(nodeSeq);
            result.EdgeSequence.AddRange(edgeSeq);
            result.TotalWeight = finalNode.G;
            result.EstimatedTravelTime = totalLen / Mathf.Max(20f, 1f);

            Debug.Log($"[AStar] 找到路径: 节点={result.NodeSequence.Count}, " +
                      $"边={result.EdgeSequence.Count}, 轨道={result.TrackSequence.Count}, " +
                      $"总权重={result.TotalWeight:F2}, 预计时间={result.EstimatedTravelTime:F1}s");
        }

        public List<AStarPathResult> FindKShortestPaths(
            string startNodeId,
            string goalNodeId,
            Direction trainDirection,
            int k = 3,
            HashSet<string> excludeTrackIds = null,
            HashSet<string> excludeSwitchIds = null,
            bool emergencyMode = false)
        {
            List<AStarPathResult> results = new List<AStarPathResult>();
            HashSet<string> excludedCombinedTracks = excludeTrackIds != null
                ? new HashSet<string>(excludeTrackIds)
                : new HashSet<string>();

            for (int i = 0; i < k; i++)
            {
                var path = FindPath(
                    startNodeId, goalNodeId, trainDirection,
                    excludedCombinedTracks, excludeSwitchIds, emergencyMode);

                if (path.Status != PathStatus.Found) break;

                results.Add(path);

                if (path.TrackSequence.Count > 2)
                {
                    int idx = Mathf.Min(1, path.TrackSequence.Count - 1);
                    excludedCombinedTracks.Add(path.TrackSequence[idx]);
                }
                else if (path.TrackSequence.Count > 0)
                {
                    excludedCombinedTracks.Add(path.TrackSequence[0]);
                }
            }

            return results;
        }
    }

    public class PriorityQueue<T> where T : IComparable<T>
    {
        private readonly List<T> _heap = new List<T>();

        public int Count => _heap.Count;

        public void Enqueue(T item)
        {
            _heap.Add(item);
            int childIndex = _heap.Count - 1;
            while (childIndex > 0)
            {
                int parentIndex = (childIndex - 1) / 2;
                if (_heap[childIndex].CompareTo(_heap[parentIndex]) >= 0)
                    break;

                (_heap[childIndex], _heap[parentIndex]) = (_heap[parentIndex], _heap[childIndex]);
                childIndex = parentIndex;
            }
        }

        public T Dequeue()
        {
            int lastIndex = _heap.Count - 1;
            T frontItem = _heap[0];
            _heap[0] = _heap[lastIndex];
            _heap.RemoveAt(lastIndex);
            lastIndex--;

            int parentIndex = 0;
            while (true)
            {
                int leftChild = parentIndex * 2 + 1;
                if (leftChild > lastIndex)
                    break;

                int rightChild = leftChild + 1;
                if (rightChild <= lastIndex &&
                    _heap[rightChild].CompareTo(_heap[leftChild]) < 0)
                {
                    leftChild = rightChild;
                }

                if (_heap[parentIndex].CompareTo(_heap[leftChild]) <= 0)
                    break;

                (_heap[parentIndex], _heap[leftChild]) = (_heap[leftChild], _heap[parentIndex]);
                parentIndex = leftChild;
            }

            return frontItem;
        }

        public T Peek()
        {
            return _heap.Count > 0 ? _heap[0] : default;
        }

        public void Clear()
        {
            _heap.Clear();
        }
    }
}
