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
}
