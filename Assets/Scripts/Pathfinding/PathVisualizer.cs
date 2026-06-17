using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;

namespace RailwayInterlock.Pathfinding
{
    public class PathVisualizer : MonoBehaviour
    {
        [Header("全局开关")]
        public bool drawTopologyGraph = false;
        public bool drawActiveDiversionPaths = true;
        public bool drawWeightsOnEdges = false;
        public bool drawNodeLabels = false;
        public bool drawCandidatePaths = false;

        [Header("样式配置")]
        public float topologyEdgeAlpha = 0.2f;
        public float mainPathThickness = 1.5f;
        public float pathArrowInterval = 12f;
        public float nodeSphereRadius = 0.25f;
        public float edgeLineLifetime = 0.1f;

        [Header("颜色配置")]
        public Color nodeTrackColor = new Color(0.4f, 0.7f, 1f);
        public Color nodeSwitchCommonColor = new Color(0.9f, 0.7f, 0.2f);
        public Color nodeSwitchNormalColor = new Color(0.3f, 0.85f, 0.4f);
        public Color nodeSwitchReverseColor = new Color(0.95f, 0.55f, 0.15f);
        public Color nodeSignalColor = new Color(0.8f, 0.2f, 0.8f);

        public Color edgeStraightColor = Color.gray;
        public Color edgeNormalSwitchColor = new Color(0.3f, 0.85f, 0.4f);
        public Color edgeReverseSwitchColor = new Color(0.95f, 0.55f, 0.15f);
        public Color edgeRedSignalColor = new Color(1f, 0.2f, 0.2f);

        public Color mainPathLineColor = new Color(0.2f, 1f, 0.4f);
        public Color altPathLineColor = new Color(0.3f, 0.6f, 1f);

        [Header("引用")]
        public AStarDiversionScheduler scheduler;

        private readonly GUIStyle _labelStyle = new GUIStyle();

        private void Awake()
        {
            _labelStyle.fontSize = 11;
            _labelStyle.normal.textColor = Color.white;
            _labelStyle.alignment = TextAnchor.MiddleCenter;
            _labelStyle.fontStyle = FontStyle.Bold;
        }

        private void Update()
        {
            if (scheduler == null || scheduler.Topology == null) return;

            if (drawTopologyGraph)
            {
                DrawFullTopology();
            }

            if (drawActiveDiversionPaths)
            {
                DrawSchedulerActivePaths();
            }
        }

        private void DrawFullTopology()
        {
            var topo = scheduler.Topology;

            foreach (var kvp in topo.Edges)
            {
                GraphEdge edge = kvp.Value;
                if (!topo.Nodes.TryGetValue(edge.FromNodeId, out var a) ||
                    !topo.Nodes.TryGetValue(edge.ToNodeId, out var b))
                    continue;

                Vector3 va = a.WorldPosition + Vector3.up * 1.8f;
                Vector3 vb = b.WorldPosition + Vector3.up * 1.8f;

                Color edgeColor = edge.Type switch
                {
                    GraphEdgeType.NormalSwitch => edgeNormalSwitchColor,
                    GraphEdgeType.ReverseSwitch => edgeReverseSwitchColor,
                    _ => edgeStraightColor
                };

                if (!string.IsNullOrEmpty(edge.ProtectingSignalId))
                {
                    var evaluator = GetEvaluator();
                    if (evaluator != null &&
                        evaluator.GetSignalAspect(edge.ProtectingSignalId) == SignalAspect.Red)
                    {
                        edgeColor = edgeRedSignalColor;
                    }
                }

                edgeColor.a = topologyEdgeAlpha;
                Debug.DrawLine(va, vb, edgeColor, edgeLineLifetime, false);
            }

            foreach (var kvp in topo.Nodes)
            {
                GraphNode node = kvp.Value;
                Vector3 p = node.WorldPosition + Vector3.up * 1.8f;

                Color nodeColor = node.Type switch
                {
                    GraphNodeType.TrackCircuit => nodeTrackColor,
                    GraphNodeType.SwitchCommon => nodeSwitchCommonColor,
                    GraphNodeType.SwitchNormal => nodeSwitchNormalColor,
                    GraphNodeType.SwitchReverse => nodeSwitchReverseColor,
                    GraphNodeType.SignalStation => nodeSignalColor,
                    _ => Color.white
                };

                DebugShapes.DrawDot(p, nodeSphereRadius, nodeColor, edgeLineLifetime);
            }
        }

        private void DrawSchedulerActivePaths()
        {
            int idx = 0;
            foreach (var kvp in scheduler.ActivePaths)
            {
                var path = kvp.Value;
                if (path == null) continue;

                Color c = idx == 0 ? mainPathLineColor : altPathLineColor;
                DrawPathWithArrowsAndLabels(path, c, kvp.Key);

                idx++;
                if (idx >= 1 && !drawCandidatePaths) break;
            }
        }

        public void DrawPathWithArrowsAndLabels(
            AStarPathResult path,
            Color color,
            string labelPrefix = "")
        {
            if (path == null || scheduler == null || scheduler.Topology == null) return;

            var topo = scheduler.Topology;
            Vector3 prev = Vector3.zero;
            Vector3 first = Vector3.zero;
            Vector3 last = Vector3.zero;
            float totalAccum = 0f;
            bool hasPrev = false;
            int nodeCount = 0;

            for (int i = 0; i < path.NodeSequence.Count; i++)
            {
                if (!topo.Nodes.TryGetValue(path.NodeSequence[i], out var node))
                    continue;

                Vector3 p = node.WorldPosition + Vector3.up * 2f;

                if (!hasPrev)
                {
                    first = p;
                    hasPrev = true;
                }
                else
                {
                    float segLen = Vector3.Distance(prev, p);
                    Debug.DrawLine(prev, p, color, edgeLineLifetime * 20f, false);

                    totalAccum += segLen;
                    while (totalAccum >= pathArrowInterval)
                    {
                        float t = (totalAccum - pathArrowInterval) / Mathf.Max(segLen, 0.01f);
                        Vector3 arrowBase = Vector3.Lerp(prev, p, t);
                        Vector3 dir = (p - prev).normalized;
                        DrawArrowHead(arrowBase, dir, color, 0.7f);
                        totalAccum -= pathArrowInterval;
                    }
                }

                prev = p;
                last = p;
                nodeCount++;
            }

            if (nodeCount > 0)
            {
                DebugShapes.DrawRing(first, 0.7f, Color.green, edgeLineLifetime * 20f);
                DebugShapes.DrawRing(last, 0.7f, Color.magenta, edgeLineLifetime * 20f);

                Vector3 flagStart = first + Vector3.up * 0.8f;
                Debug.DrawLine(first, flagStart, Color.green, edgeLineLifetime * 20f);
                DebugShapes.DrawFlag(flagStart, Color.green, labelPrefix, edgeLineLifetime * 20f);

                Vector3 flagEnd = last + Vector3.up * 0.8f;
                Debug.DrawLine(last, flagEnd, Color.magenta, edgeLineLifetime * 20f);
                DebugShapes.DrawFlag(flagEnd, Color.magenta, "GOAL", edgeLineLifetime * 20f);
            }

            if (drawWeightsOnEdges)
            {
                DrawEdgeWeights(path, color);
            }
        }

        private void DrawEdgeWeights(AStarPathResult path, Color color)
        {
            if (scheduler == null || scheduler.Topology == null) return;

            foreach (var edge in path.EdgeSequence)
            {
                if (!scheduler.Topology.Nodes.TryGetValue(edge.FromNodeId, out var a) ||
                    !scheduler.Topology.Nodes.TryGetValue(edge.ToNodeId, out var b))
                    continue;

                Vector3 mid = (a.WorldPosition + b.WorldPosition) * 0.5f + Vector3.up * 2.5f;
                string label = edge.Type switch
                {
                    GraphEdgeType.NormalSwitch => $"NORM {edge.BaseWeight:F1}",
                    GraphEdgeType.ReverseSwitch => $"REV {edge.BaseWeight:F1}",
                    GraphEdgeType.Shunting => $"SH {edge.BaseWeight:F1}",
                    _ => $"{edge.BaseWeight:F1}"
                };
                DebugShapes.DrawText(mid, label, color, edgeLineLifetime * 20f);
            }
        }

        private static void DrawArrowHead(Vector3 pos, Vector3 dir, Color color, float size)
        {
            Vector3 side1 = Quaternion.Euler(0, 150f, 0) * dir * size;
            Vector3 side2 = Quaternion.Euler(0, -150f, 0) * dir * size;
            Debug.DrawLine(pos, pos + side1, color, edgeLineLifetime * 20f, false);
            Debug.DrawLine(pos, pos + side2, color, edgeLineLifetime * 20f, false);
        }

        private DynamicWeightEvaluator GetEvaluator()
        {
            if (scheduler == null) return null;
            var field = scheduler.GetType().GetField("_weightEvaluator",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);
            return field?.GetValue(scheduler) as DynamicWeightEvaluator;
        }
    }

    public static class DebugShapes
    {
        public static void DrawDot(Vector3 center, float radius, Color color, float duration)
        {
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float a1 = i / (float)segments * Mathf.PI * 2f;
                float a2 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 p1 = center + new Vector3(Mathf.Cos(a1) * radius, 0, Mathf.Sin(a1) * radius);
                Vector3 p2 = center + new Vector3(Mathf.Cos(a2) * radius, 0, Mathf.Sin(a2) * radius);
                Debug.DrawLine(p1, p2, color, duration, false);
            }

            Vector3 top = center + Vector3.up * radius;
            Vector3 bot = center - Vector3.up * radius;
            Debug.DrawLine(center + Vector3.left * radius, center + Vector3.right * radius, color, duration);
            Debug.DrawLine(center + Vector3.back * radius, center + Vector3.forward * radius, color, duration);
        }

        public static void DrawRing(Vector3 center, float radius, Color color, float duration)
        {
            int segments = 24;
            float y = center.y;
            for (int i = 0; i < segments; i++)
            {
                float a1 = i / (float)segments * Mathf.PI * 2f;
                float a2 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 p1 = new Vector3(center.x + Mathf.Cos(a1) * radius, y, center.z + Mathf.Sin(a1) * radius);
                Vector3 p2 = new Vector3(center.x + Mathf.Cos(a2) * radius, y, center.z + Mathf.Sin(a2) * radius);
                Debug.DrawLine(p1, p2, color, duration, false);
            }
        }

        public static void DrawFlag(Vector3 basePos, Color color, string text, float duration)
        {
            Vector3 top = basePos + Vector3.up * 0.6f;
            Vector3 corner1 = top + Vector3.right * 0.5f;
            Vector3 corner2 = top + Vector3.right * 0.5f + Vector3.down * 0.3f;

            Debug.DrawLine(basePos, top, color, duration);
            Debug.DrawLine(top, corner1, color, duration);
            Debug.DrawLine(corner1, corner2, color, duration);
            Debug.DrawLine(corner2, top, color, duration);
        }

        public static void DrawText(Vector3 pos, string text, Color color, float duration)
        {
            Vector3 offset1 = new Vector3(-0.05f, 0.05f, 0);
            Vector3 offset2 = new Vector3(0.05f, -0.05f, 0);

            Debug.DrawRay(pos + offset1 + Vector3.left, Vector3.right * 0.2f, color, duration);
            Debug.DrawRay(pos + offset2 + Vector3.left, Vector3.right * 0.2f, color, duration);
        }
    }
}
