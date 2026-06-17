using System;
using RailwayInterlock.Core;

namespace RailwayInterlock.Interfaces
{
    public interface ITrackCircuit
    {
        string Id { get; }
        TrackState State { get; }
        event Action<string, TrackState> OnStateChanged;
        void SetOccupied();
        void SetClear();
    }

    public interface ISwitch
    {
        string Id { get; }
        SwitchPosition Position { get; }
        SwitchType Type { get; }
        event Action<string, SwitchPosition> OnPositionChanged;
        bool SetPosition(SwitchPosition position);
        bool IsInConsistentPosition();
    }

    public interface ISignal
    {
        string Id { get; }
        SignalAspect Aspect { get; }
        event Action<string, SignalAspect> OnAspectChanged;
        void SetAspect(SignalAspect aspect);
    }

    public interface IInterlockController
    {
        bool CanSetRoute(string routeId);
        bool SetRoute(string routeId);
        bool CancelRoute(string routeId);
        void EvaluateAllSignals();
        SignalAspect CalculateSignalAspect(string signalId);
    }

    public interface ITrain
    {
        string Id { get; }
        TrainState State { get; }
        float Speed { get; }
        void ApplyBrake();
        void ReleaseBrake();
    }
}
