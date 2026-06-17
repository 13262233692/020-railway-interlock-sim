using System;
using System.Collections.Generic;
using RailwayInterlock.Core;

namespace RailwayInterlock.Data
{
    [Serializable]
    public class TrackCircuitData
    {
        public string Id;
        public string Name;
        public float Length;
        public List<string> AdjacentTracks = new List<string>();
    }

    [Serializable]
    public class SwitchData
    {
        public string Id;
        public string Name;
        public SwitchType Type;
        public string NormalConnection;
        public string ReverseConnection;
        public string CommonTrack;
    }

    [Serializable]
    public class SignalData
    {
        public string Id;
        public string Name;
        public Direction Direction;
        public string ProtectingTrackId;
        public List<string> ControlledSwitches = new List<string>();
        public List<string> ControlledTracks = new List<string>();
        public List<SignalLogicCondition> LogicConditions = new List<SignalLogicCondition>();
    }

    [Serializable]
    public class SignalLogicCondition
    {
        public SignalAspect TargetAspect;
        public List<ConditionGroup> ConditionGroups = new List<ConditionGroup>();
    }

    [Serializable]
    public class ConditionGroup
    {
        public List<TrackOccupiedCondition> TrackConditions = new List<TrackOccupiedCondition>();
        public List<SwitchPositionCondition> SwitchConditions = new List<SwitchPositionCondition>();
        public List<SignalAspectCondition> SignalConditions = new List<SignalAspectCondition>();
    }

    [Serializable]
    public class TrackOccupiedCondition
    {
        public string TrackId;
        public bool ShouldBeOccupied;
    }

    [Serializable]
    public class SwitchPositionCondition
    {
        public string SwitchId;
        public SwitchPosition RequiredPosition;
    }

    [Serializable]
    public class SignalAspectCondition
    {
        public string SignalId;
        public SignalAspect RequiredAspect;
        public bool Invert;
    }

    [Serializable]
    public class RouteData
    {
        public string Id;
        public string Name;
        public string EntrySignalId;
        public string ExitSignalId;
        public Direction Direction;
        public List<string> TrackSequence = new List<string>();
        public List<SwitchPositionRequirement> SwitchRequirements = new List<SwitchPositionRequirement>();
        public List<string> ConflictingRoutes = new List<string>();
        public int SpeedLimitKmh;
    }

    [Serializable]
    public class SwitchPositionRequirement
    {
        public string SwitchId;
        public SwitchPosition RequiredPosition;
    }

    [Serializable]
    public class TrainData
    {
        public string Id;
        public string Name;
        public string StartTrackId;
        public float MaxSpeedKmh;
        public float Acceleration;
        public float BrakeDeceleration;
        public float Length;
    }

    // ========== 铁路拓扑图数据结构 ==========

    [Serializable]
    public class GraphNode
    {
        public string Id;
        public GraphNodeType Type;
        public string TrackId;
        public string SwitchId;
        public string SignalId;
        public Vector3 WorldPosition;
        public float HeuristicCache;
    }

    [Serializable]
    public class GraphEdge
    {
        public string Id;
        public string FromNodeId;
        public string ToNodeId;
        public GraphEdgeType Type;
        public float BaseWeight;
        public Direction Direction;
        public string SwitchId;
        public SwitchPosition RequiredSwitchPosition;
        public string ProtectingSignalId;
        public float Length;
        public bool Bidirectional;
    }

    [Serializable]
    public class RailwayTopology
    {
        public Dictionary<string, GraphNode> Nodes = new Dictionary<string, GraphNode>();
        public Dictionary<string, GraphEdge> Edges = new Dictionary<string, GraphEdge>();
        public Dictionary<string, List<GraphEdge>> Adjacency = new Dictionary<string, List<GraphEdge>>();
    }

    // ========== A* 寻路数据结构 ==========

    public class AStarNode : IComparable<AStarNode>
    {
        public string NodeId;
        public float G; // 从起点的实际代价
        public float H; // 启发式估计代价
        public float F => G + H;
        public AStarNode Parent;
        public GraphEdge TraversedEdge;

        public int CompareTo(AStarNode other)
        {
            if (other == null) return 1;
            int result = F.CompareTo(other.F);
            if (result == 0) result = H.CompareTo(other.H);
            return result;
        }
    }

    public class AStarPathResult
    {
        public PathStatus Status;
        public List<string> NodeSequence = new List<string>();
        public List<GraphEdge> EdgeSequence = new List<GraphEdge>();
        public List<string> TrackSequence = new List<string>();
        public float TotalWeight;
        public float EstimatedTravelTime;
        public Dictionary<string, SwitchPosition> RequiredSwitchPositions = new Dictionary<string, SwitchPosition>();
        public List<string> PassedSignalIds = new List<string>();
    }

    // ========== 冲突检测与避让 ==========

    public class TrainPositionSnapshot
    {
        public string TrainId;
        public string CurrentTrackId;
        public List<string> OccupiedTrackIds = new List<string>();
        public Direction TravelDirection;
        public float SpeedMs;
        public Vector3 WorldPosition;
        public float ArrivalTimeAtNextNode;
    }

    public class ConflictReport
    {
        public ConflictType Type;
        public string SubjectTrainId;
        public string ObstacleTrainId;
        public string ConflictTrackId;
        public float EstimatedTimeToContact;
        public float DistanceToContact;
    }

    // ========== 动态权重配置 ==========

    [Serializable]
    public class WeightConfiguration
    {
        public float StraightEdgeBaseWeight = 1.0f;
        public float NormalSwitchWeight = 1.2f;
        public float ReverseSwitchWeight = 1.8f;
        public float ShuntingEdgeWeight = 2.5f;
        public float RedSignalWeight = float.MaxValue * 0.1f; // 非MaxValue以便仍可排序
        public float OccupiedTrackWeightMultiplier = 1000.0f;
        public float OppositeDirectionPenalty = 500.0f;
        public float BlockedSwitchWeight = 500.0f;
        public float MovingSwitchWeight = 100.0f;
        public float YellowSignalWeightMultiplier = 1.5f;
        public float EmergencyDiversionExtraMultiplier = 1.0f;
    }
}
