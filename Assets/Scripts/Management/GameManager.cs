using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Interfaces;
using RailwayInterlock.Components;
using RailwayInterlock.Interlocking;

namespace RailwayInterlock.Management
{
    [DefaultExecutionOrder(-100)]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("Scene References")]
        public List<TrackCircuit> trackCircuits = new List<TrackCircuit>();
        public List<SwitchPoint> switchPoints = new List<SwitchPoint>();
        public List<Signal> signals = new List<Signal>();
        public List<Train> trains = new List<Train>();
        public List<RouteData> routeDefinitions = new List<RouteData>();

        [Header("Settings")]
        public bool autoEvaluateInterlock = true;
        public bool enableDebugLogging = true;
        public bool evaluateEveryFixedStep = true;

        [Header("Runtime State")]
        [SerializeField] private bool _isSimulationRunning = true;
        [SerializeField] private float _simulationTimeScale = 1f;

        private Dictionary<string, ITrackCircuit> _trackCircuitDict;
        private Dictionary<string, ISwitch> _switchDict;
        private Dictionary<string, ISignal> _signalDict;
        private Dictionary<string, SignalData> _signalDataDict;
        private Dictionary<string, RouteData> _routeDataDict;
        private InterlockController _interlockController;

        public InterlockController Interlock => _interlockController;
        public bool IsSimulationRunning => _isSimulationRunning;

        public event Action OnSimulationStarted;
        public event Action OnSimulationPaused;
        public event Action OnSimulationReset;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            InitializeDictionaries();
            InitializeInterlockController();
            RegisterEventListeners();
            StartSimulation();
        }

        private void InitializeDictionaries()
        {
            _trackCircuitDict = new Dictionary<string, ITrackCircuit>();
            _switchDict = new Dictionary<string, ISwitch>();
            _signalDict = new Dictionary<string, ISignal>();
            _signalDataDict = new Dictionary<string, SignalData>();
            _routeDataDict = new Dictionary<string, RouteData>();

            foreach (var tc in trackCircuits)
            {
                if (!string.IsNullOrEmpty(tc.trackId) && !_trackCircuitDict.ContainsKey(tc.trackId))
                {
                    _trackCircuitDict[tc.trackId] = tc;
                }
            }

            foreach (var sw in switchPoints)
            {
                if (!string.IsNullOrEmpty(sw.switchId) && !_switchDict.ContainsKey(sw.switchId))
                {
                    _switchDict[sw.switchId] = sw;
                }
            }

            foreach (var sig in signals)
            {
                if (!string.IsNullOrEmpty(sig.signalId) && !_signalDict.ContainsKey(sig.signalId))
                {
                    _signalDict[sig.signalId] = sig;
                    _signalDataDict[sig.signalId] = sig.ToData();
                }
            }

            foreach (var route in routeDefinitions)
            {
                if (!string.IsNullOrEmpty(route.Id) && !_routeDataDict.ContainsKey(route.Id))
                {
                    _routeDataDict[route.Id] = route;
                }
            }

            if (enableDebugLogging)
            {
                Debug.Log($"[GameManager] 初始化: {_trackCircuitDict.Count} 个轨道区段, {_switchDict.Count} 个道岔, {_signalDict.Count} 个信号机, {_routeDataDict.Count} 条进路");
            }
        }

        private void InitializeInterlockController()
        {
            _interlockController = new InterlockController(
                _trackCircuitDict,
                _switchDict,
                _signalDict,
                _signalDataDict,
                _routeDataDict);

            _interlockController.OnRouteStateChanged += HandleRouteStateChanged;
            _interlockController.OnSignalAspectChanged += HandleSignalAspectChanged;
        }

        private void RegisterEventListeners()
        {
            foreach (var train in trains)
            {
                train.OnEmergencyBrakeTriggered += HandleEmergencyBrake;
                train.OnStateChanged += HandleTrainStateChanged;
            }
        }

        private void FixedUpdate()
        {
            if (!_isSimulationRunning) return;

            if (autoEvaluateInterlock && evaluateEveryFixedStep)
            {
                _interlockController.EvaluateAllSignals();
            }
        }

        public void StartSimulation()
        {
            _isSimulationRunning = true;
            Time.timeScale = _simulationTimeScale;
            _interlockController.EvaluateAllSignals();
            OnSimulationStarted?.Invoke();

            if (enableDebugLogging)
                Debug.Log("[GameManager] 仿真已启动");
        }

        public void PauseSimulation()
        {
            _isSimulationRunning = false;
            Time.timeScale = 0f;
            OnSimulationPaused?.Invoke();

            if (enableDebugLogging)
                Debug.Log("[GameManager] 仿真已暂停");
        }

        public void ToggleSimulation()
        {
            if (_isSimulationRunning)
                PauseSimulation();
            else
                StartSimulation();
        }

        public void ResetSimulation()
        {
            foreach (var tc in trackCircuits)
            {
                tc.ResetOccupancy();
            }

            foreach (var train in trains)
            {
                if (train.transform.parent != null && train.transform.parent.TryGetComponent<TrainSpawnPoint>(out var spawn))
                {
                    train.ResetTrain(spawn.transform.position, spawn.transform.rotation);
                }
            }

            _interlockController.EvaluateAllSignals();
            OnSimulationReset?.Invoke();

            if (enableDebugLogging)
                Debug.Log("[GameManager] 仿真已重置");
        }

        public void SetSimulationTimeScale(float scale)
        {
            _simulationTimeScale = Mathf.Max(0.1f, Mathf.Min(5f, scale));
            if (_isSimulationRunning)
                Time.timeScale = _simulationTimeScale;
        }

        public bool SetRoute(string routeId)
        {
            bool result = _interlockController.SetRoute(routeId);
            if (enableDebugLogging)
                Debug.Log($"[GameManager] 建立进路 {routeId}: {(result ? "成功" : "失败")}");
            return result;
        }

        public bool CancelRoute(string routeId)
        {
            bool result = _interlockController.CancelRoute(routeId);
            if (enableDebugLogging)
                Debug.Log($"[GameManager] 取消进路 {routeId}: {(result ? "成功" : "失败")}");
            return result;
        }

        public bool SetSwitchPosition(string switchId, SwitchPosition position)
        {
            if (_switchDict.TryGetValue(switchId, out var sw))
            {
                return sw.SetPosition(position);
            }
            return false;
        }

        public void ToggleSwitch(string switchId)
        {
            foreach (var sw in switchPoints)
            {
                if (sw.switchId == switchId)
                {
                    sw.TogglePosition();
                    return;
                }
            }
        }

        public TrackCircuit GetTrackCircuit(string id)
        {
            return trackCircuits.Find(tc => tc.trackId == id);
        }

        public SwitchPoint GetSwitchPoint(string id)
        {
            return switchPoints.Find(sw => sw.switchId == id);
        }

        public Signal GetSignal(string id)
        {
            return signals.Find(sig => sig.signalId == id);
        }

        public Train GetTrain(string id)
        {
            return trains.Find(t => t.trainId == id);
        }

        public RouteState GetRouteState(string routeId)
        {
            return _interlockController.GetRouteState(routeId);
        }

        public Dictionary<string, RouteState> GetAllRouteStates()
        {
            return _interlockController.GetAllRouteStates();
        }

        public SignalAspect CalculateSignalAspect(string signalId)
        {
            return _interlockController.CalculateSignalAspect(signalId);
        }

        private void HandleRouteStateChanged(string routeId, RouteState state)
        {
            if (enableDebugLogging)
                Debug.Log($"[GameManager] 进路 {routeId} 状态变化: {state}");
        }

        private void HandleSignalAspectChanged(string signalId, SignalAspect aspect)
        {
        }

        private void HandleEmergencyBrake(string trainId)
        {
            Debug.LogWarning($"[GameManager] 列车 {trainId} 触发紧急制动");
        }

        private void HandleTrainStateChanged(string trainId, TrainState state)
        {
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }

    public class TrainSpawnPoint : MonoBehaviour
    {
        public string spawnPointId;
    }
}
