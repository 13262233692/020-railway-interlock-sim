using System.Collections.Generic;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Interfaces;

namespace RailwayInterlock.Interlocking
{
    public class BooleanLogicEvaluator
    {
        private readonly Dictionary<string, ITrackCircuit> _trackCircuits;
        private readonly Dictionary<string, ISwitch> _switches;
        private readonly Dictionary<string, ISignal> _signals;

        public BooleanLogicEvaluator(
            Dictionary<string, ITrackCircuit> trackCircuits,
            Dictionary<string, ISwitch> switches,
            Dictionary<string, ISignal> signals)
        {
            _trackCircuits = trackCircuits;
            _switches = switches;
            _signals = signals;
        }

        public bool EvaluateConditionGroup(ConditionGroup group)
        {
            foreach (var trackCond in group.TrackConditions)
            {
                if (!EvaluateTrackCondition(trackCond))
                    return false;
            }

            foreach (var switchCond in group.SwitchConditions)
            {
                if (!EvaluateSwitchCondition(switchCond))
                    return false;
            }

            foreach (var signalCond in group.SignalConditions)
            {
                if (!EvaluateSignalCondition(signalCond))
                    return false;
            }

            return true;
        }

        public bool EvaluateTrackCondition(TrackOccupiedCondition condition)
        {
            if (_trackCircuits.TryGetValue(condition.TrackId, out var track))
            {
                bool isOccupied = track.State == TrackState.Occupied;
                return condition.ShouldBeOccupied ? isOccupied : !isOccupied;
            }
            return false;
        }

        public bool EvaluateSwitchCondition(SwitchPositionCondition condition)
        {
            if (_switches.TryGetValue(condition.SwitchId, out var sw))
            {
                return sw.Position == condition.RequiredPosition;
            }
            return false;
        }

        public bool EvaluateSignalCondition(SignalAspectCondition condition)
        {
            if (_signals.TryGetValue(condition.SignalId, out var signal))
            {
                bool matches = signal.Aspect == condition.RequiredAspect;
                return condition.Invert ? !matches : matches;
            }
            return false;
        }

        public bool EvaluateSignalLogicConditions(List<SignalLogicCondition> conditions, out SignalAspect result)
        {
            result = SignalAspect.Red;

            foreach (var logic in conditions)
            {
                bool anyGroupSatisfied = false;

                foreach (var group in logic.ConditionGroups)
                {
                    if (EvaluateConditionGroup(group))
                    {
                        anyGroupSatisfied = true;
                        break;
                    }
                }

                if (anyGroupSatisfied)
                {
                    result = logic.TargetAspect;
                    return true;
                }
            }

            return false;
        }
    }
}
