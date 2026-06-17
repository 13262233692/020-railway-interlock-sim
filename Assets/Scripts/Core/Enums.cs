namespace RailwayInterlock.Core
{
    public enum SignalAspect
    {
        Red = 0,
        Yellow = 1,
        Green = 2
    }

    public enum SwitchPosition
    {
        Normal = 0,
        Reverse = 1
    }

    public enum TrackState
    {
        Clear = 0,
        Occupied = 1
    }

    public enum TrainState
    {
        Stopped = 0,
        Moving = 1,
        Braking = 2
    }

    public enum RouteState
    {
        NotSet = 0,
        Setting = 1,
        Set = 2,
        Occupied = 3,
        Cancelling = 4
    }

    public enum SwitchType
    {
        Single,
        Double
    }

    public enum Direction
    {
        Up,
        Down
    }

    public enum GraphNodeType
    {
        TrackCircuit,
        SwitchCommon,
        SwitchNormal,
        SwitchReverse,
        SignalStation
    }

    public enum GraphEdgeType
    {
        Straight,
        NormalSwitch,
        ReverseSwitch,
        Shunting
    }

    public enum PathStatus
    {
        NotComputed,
        Searching,
        Found,
        NoPath,
        Executing,
        Aborted,
        Completed
    }

    public enum ConflictType
    {
        None,
        HeadOn,
        RearEnd,
        Blocked,
        Overtake
    }
}
