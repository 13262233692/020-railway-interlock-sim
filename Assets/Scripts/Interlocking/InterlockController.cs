using System;
using System.Collections.Generic;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Interfaces;

namespace RailwayInterlock.Interlocking
{
    public class InterlockController : IInterlockController
    {
        private readonly Dictionary<string, ITrackCircuit> _trackCircuits;
        private readonly Dictionary<string, ISwitch> _switches;
        private readonly Dictionary<string, ISignal> _signals;
        private readonly Dictionary<string, SignalData> _signalDatas;
        private readonly Dictionary<string, RouteData> _routeDatas;
        private readonly Dictionary<string, RouteState> _routeStates;
        private readonly BooleanLogicEvaluator _logicEvaluator;

        public event Action<string, RouteState> OnRouteStateChanged;
        public event Action<string, SignalAspect> OnSignalAspectChanged;

        public InterlockController(
            Dictionary<string, ITrackCircuit> trackCircuits,
            Dictionary<string, ISwitch> switches,
            Dictionary<string, ISignal> signals,
            Dictionary<string, SignalData> signalDatas,
            Dictionary<string, RouteData> routeDatas)
        {
            _trackCircuits = trackCircuits;
            _switches = switches;
            _signals = signals;
            _signalDatas = signalDatas;
            _routeDatas = routeDatas;
            _routeStates = new Dictionary<string, RouteState>();
            _logicEvaluator = new BooleanLogicEvaluator(trackCircuits, switches, signals);

            foreach (var routeId in routeDatas.Keys)
            {
                _routeStates[routeId] = RouteState.NotSet;
            }

            SubscribeToEvents();
        }

        private void SubscribeToEvents()
        {
            foreach (var track in _trackCircuits.Values)
            {
                track.OnStateChanged += (id, state) => EvaluateAllSignals();
            }

            foreach (var sw in _switches.Values)
            {
                sw.OnPositionChanged += (id, pos) => EvaluateAllSignals();
            }
        }

        public bool CanSetRoute(string routeId)
        {
            if (!_routeDatas.TryGetValue(routeId, out var routeData))
                return false;

            if (_routeStates[routeId] != RouteState.NotSet)
                return false;

            foreach (var conflictingId in routeData.ConflictingRoutes)
            {
                if (_routeStates.TryGetValue(conflictingId, out var state) &&
                    (state == RouteState.Set || state == RouteState.Occupied || state == RouteState.Setting))
                    return false;
            }

            foreach (var trackId in routeData.TrackSequence)
            {
                if (_trackCircuits.TryGetValue(trackId, out var track) &&
                    track.State == TrackState.Occupied)
                    return false;
            }

            return true;
        }

        public bool SetRoute(string routeId)
        {
            if (!CanSetRoute(routeId))
                return false;

            var routeData = _routeDatas[routeId];
            _routeStates[routeId] = RouteState.Setting;
            OnRouteStateChanged?.Invoke(routeId, RouteState.Setting);

            foreach (var req in routeData.SwitchRequirements)
            {
                if (_switches.TryGetValue(req.SwitchId, out var sw))
                {
                    if (!sw.SetPosition(req.RequiredPosition))
                    {
                        _routeStates[routeId] = RouteState.NotSet;
                        OnRouteStateChanged?.Invoke(routeId, RouteState.NotSet);
                        return false;
                    }
                }
            }

            bool allSwitchesConsistent = true;
            foreach (var req in routeData.SwitchRequirements)
            {
                if (_switches.TryGetValue(req.SwitchId, out var sw) && !sw.IsInConsistentPosition())
                {
                    allSwitchesConsistent = false;
                    break;
                }
            }

            if (!allSwitchesConsistent)
            {
                _routeStates[routeId] = RouteState.NotSet;
                OnRouteStateChanged?.Invoke(routeId, RouteState.NotSet);
                return false;
            }

            _routeStates[routeId] = RouteState.Set;
            OnRouteStateChanged?.Invoke(routeId, RouteState.Set);

            EvaluateAllSignals();
            return true;
        }

        public bool CancelRoute(string routeId)
        {
            if (!_routeStates.TryGetValue(routeId, out var state))
                return false;

            if (state == RouteState.Occupied)
                return false;

            _routeStates[routeId] = RouteState.Cancelling;
            OnRouteStateChanged?.Invoke(routeId, RouteState.Cancelling);

            _routeStates[routeId] = RouteState.NotSet;
            OnRouteStateChanged?.Invoke(routeId, RouteState.NotSet);

            EvaluateAllSignals();
            return true;
        }

        public RouteState GetRouteState(string routeId)
        {
            return _routeStates.TryGetValue(routeId, out var state) ? state : RouteState.NotSet;
        }

        public void UpdateRouteOccupancy()
        {
            foreach (var kvp in _routeDatas)
            {
                var routeId = kvp.Key;
                var routeData = kvp.Value;
                var currentState = _routeStates[routeId];

                if (currentState == RouteState.Set)
                {
                    bool anyOccupied = false;
                    foreach (var trackId in routeData.TrackSequence)
                    {
                        if (_trackCircuits.TryGetValue(trackId, out var track) &&
                            track.State == TrackState.Occupied)
                        {
                            anyOccupied = true;
                            break;
                        }
                    }

                    if (anyOccupied)
                    {
                        _routeStates[routeId] = RouteState.Occupied;
                        OnRouteStateChanged?.Invoke(routeId, RouteState.Occupied);
                    }
                }
                else if (currentState == RouteState.Occupied)
                {
                    bool allClear = true;
                    foreach (var trackId in routeData.TrackSequence)
                    {
                        if (_trackCircuits.TryGetValue(trackId, out var track) &&
                            track.State == TrackState.Occupied)
                        {
                            allClear = false;
                            break;
                        }
                    }

                    if (allClear)
                    {
                        _routeStates[routeId] = RouteState.NotSet;
                        OnRouteStateChanged?.Invoke(routeId, RouteState.NotSet);
                        EvaluateAllSignals();
                    }
                }
            }
        }

        public void EvaluateAllSignals()
        {
            UpdateRouteOccupancy();

            foreach (var kvp in _signalDatas)
            {
                string signalId = kvp.Key;
                var signalData = kvp.Value;
                var aspect = CalculateSignalAspect(signalId);

                if (_signals.TryGetValue(signalId, out var signal))
                {
                    var oldAspect = signal.Aspect;
                    signal.SetAspect(aspect);
                    if (oldAspect != aspect)
                    {
                        OnSignalAspectChanged?.Invoke(signalId, aspect);
                    }
                }
            }
        }

        public SignalAspect CalculateSignalAspect(string signalId)
        {
            if (!_signalDatas.TryGetValue(signalId, out var signalData))
                return SignalAspect.Red;

            if (signalData.LogicConditions.Count > 0)
            {
                if (_logicEvaluator.EvaluateSignalLogicConditions(signalData.LogicConditions, out var computedAspect))
                {
                    return computedAspect;
                }
                return SignalAspect.Red;
            }

            return CalculateAspectByRouteAndTrack(signalData);
        }

        private SignalAspect CalculateAspectByRouteAndTrack(SignalData signalData)
        {
            string routeSetForThisSignal = null;
            foreach (var kvp in _routeDatas)
            {
                if (kvp.Value.EntrySignalId == signalData.Id &&
                    (_routeStates[kvp.Key] == RouteState.Set || _routeStates[kvp.Key] == RouteState.Occupied))
                {
                    routeSetForThisSignal = kvp.Key;
                    break;
                }
            }

            if (routeSetForThisSignal == null)
                return SignalAspect.Red;

            var routeData = _routeDatas[routeSetForThisSignal];
            bool firstBlockOccupied = false;
            bool secondBlockOccupied = false;
            bool thirdBlockOccupied = false;

            if (routeData.TrackSequence.Count > 0)
            {
                var firstTrack = _trackCircuits[routeData.TrackSequence[0]];
                firstBlockOccupied = firstTrack.State == TrackState.Occupied;
            }

            if (routeData.TrackSequence.Count > 1)
            {
                var secondTrack = _trackCircuits[routeData.TrackSequence[1]];
                secondBlockOccupied = secondTrack.State == TrackState.Occupied;
            }

            if (routeData.TrackSequence.Count > 2)
            {
                var thirdTrack = _trackCircuits[routeData.TrackSequence[2]];
                thirdBlockOccupied = thirdTrack.State == TrackState.Occupied;
            }

            if (firstBlockOccupied)
                return SignalAspect.Red;

            SignalAspect exitSignalAspect = SignalAspect.Red;
            if (_signals.TryGetValue(routeData.ExitSignalId, out var exitSignal))
            {
                exitSignalAspect = exitSignal.Aspect;
            }

            if (secondBlockOccupied)
                return SignalAspect.Yellow;

            if (exitSignalAspect == SignalAspect.Green && !thirdBlockOccupied)
                return SignalAspect.Green;

            if (exitSignalAspect == SignalAspect.Yellow || !secondBlockOccupied)
                return SignalAspect.Yellow;

            return SignalAspect.Red;
        }

        public Dictionary<string, RouteState> GetAllRouteStates()
        {
            return new Dictionary<string, RouteState>(_routeStates);
        }
    }
}
