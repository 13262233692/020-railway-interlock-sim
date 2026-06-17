using System;
using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Interfaces;
using RailwayInterlock.Components;
using RailwayInterlock.Interlocking;
using RailwayInterlock.Pathfinding;

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

        [Header("A* Pathfinding System")]
        public bool enableAStarDiversion = true;
        public bool autoCreateScheduler = true;
        public AStarDiversionScheduler diversionScheduler;
        public PathVisualizer pathVisualizer;
        public List<OutOfControlEngineer> rogueEngineers = new List<OutOfControlEngineer>();

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
        public AStarDiversionScheduler Scheduler => diversionScheduler;

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
            if (enableAStarDiversion && autoCreateScheduler)
            {
                InitializeAStarScheduler();
            }
            StartSimulation();
        }

        private void InitializeAStarScheduler()
        {
            if (diversionScheduler == null)
            {
                GameObject schedulerObj = new GameObject("AStarDiversionScheduler");
                schedulerObj.transform.SetParent(transform);
                diversionScheduler = schedulerObj.AddComponent<AStarDiversionScheduler>();
            }

            diversionScheduler.trackCircuits = trackCircuits;
            diversionScheduler.switchPoints = switchPoints;
            diversionScheduler.signals = signals;
            diversionScheduler.normalTrains = trains;
            diversionScheduler.rogueEngineers = rogueEngineers;
            diversionScheduler.interlockController = _interlockController;

            diversionScheduler.Initialize();

            if (pathVisualizer == null)
            {
                PathVisualizer pv = diversionScheduler.gameObject.GetComponent<PathVisualizer>();
                if (pv == null) pv = diversionScheduler.gameObject.AddComponent<PathVisualizer>();
                pathVisualizer = pv;
            }
            pathVisualizer.scheduler = diversionScheduler;

            diversionScheduler.OnConflictDetected += HandleConflictDetected;
            diversionScheduler.OnDiversionPathComputed += HandleDiversionComputed;
            diversionScheduler.OnDiversionExecuted += HandleDiversionExecuted;

            if (enableDebugLogging)
                Debug.Log("[GameManager] A* 调度系统初始化完成");
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

        private void HandleConflictDetected(ConflictReport report)
        {
            if (enableDebugLogging)
            {
                Debug.LogWarning($"[GameManager] 冲突检测 [{report.Type}]: " +
                                 $"{report.SubjectTrainId} 与 {report.ObstacleTrainId} " +
                                 $"在 {report.ConflictTrackId ?? "未知区段"}, " +
                                 $"距离 {report.DistanceToContact:F1}m, " +
                                 $"预计 {report.EstimatedTimeToContact:F1}s 后接触");
            }
        }

        private void HandleDiversionComputed(string trainId, AStarPathResult path)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[GameManager] {trainId} 避让路径已计算: " +
                          $"轨道={path.TrackSequence.Count}段, " +
                          $"道岔={path.RequiredSwitchPositions.Count}个, " +
                          $"权重={path.TotalWeight:F2}");
            }
        }

        private void HandleDiversionExecuted(string trainId)
        {
            if (enableDebugLogging)
            {
                Debug.Log($"[GameManager] {trainId} 避让操作已启动");
            }
        }

        public OutOfControlEngineer SpawnRogueEngineer(
            string id, string name, Vector3 position, float yRotation,
            Direction dir, float speedKmh)
        {
            GameObject go = new GameObject($"Engineer_{id}");
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yRotation, 0);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 8000f;
            rb.drag = 0.15f;
            rb.angularDrag = 1.2f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            BoxCollider col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.2f, 0);
            col.size = new Vector3(2f, 2.4f, 8f);
            col.isTrigger = false;

            OutOfControlEngineer eng = go.AddComponent<OutOfControlEngineer>();
            eng.engineerId = id;
            eng.displayName = name;
            eng.travelDirection = dir;
            eng.cruiseSpeedKmh = speedKmh;

            CreateEngineerVisual(go.transform);

            rogueEngineers.Add(eng);

            if (diversionScheduler != null)
            {
                diversionScheduler.rogueEngineers = rogueEngineers;
                diversionScheduler.RefreshEvaluatorReferences();
            }

            return eng;
        }

        private void CreateEngineerVisual(Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(parent);
            body.transform.localPosition = new Vector3(0, 1.2f, 0);
            body.transform.localScale = new Vector3(2f, 2.4f, 8f);
            Destroy(body.GetComponent<BoxCollider>());
            Material bodyMat = new Material(Shader.Find("Standard"));
            bodyMat.color = new Color(0.95f, 0.55f, 0.1f);
            bodyMat.SetFloat("_Glossiness", 0.4f);
            body.GetComponent<MeshRenderer>().material = bodyMat;

            GameObject stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.name = "WarningStripe";
            stripe.transform.SetParent(parent);
            stripe.transform.localPosition = new Vector3(0, 1.4f, 0);
            stripe.transform.localScale = new Vector3(2.05f, 0.2f, 7.8f);
            Destroy(stripe.GetComponent<BoxCollider>());
            Material stripeMat = new Material(Shader.Find("Standard"));
            stripeMat.color = Color.black;
            stripeMat.SetFloat("_Glossiness", 0.2f);
            stripe.GetComponent<MeshRenderer>().material = stripeMat;

            GameObject beacon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            beacon.name = "WarningBeacon";
            beacon.transform.SetParent(parent);
            beacon.transform.localPosition = new Vector3(0, 2.8f, 0);
            beacon.transform.localScale = Vector3.one * 0.4f;
            Destroy(beacon.GetComponent<SphereCollider>());
            Material beaconMat = new Material(Shader.Find("Standard"));
            beaconMat.color = Color.red;
            beaconMat.EnableKeyword("_EMISSION");
            beaconMat.SetColor("_EmissionColor", Color.red * 1.5f);
            beacon.GetComponent<MeshRenderer>().material = beaconMat;
        }

        public void RebuildAStarTopology()
        {
            if (diversionScheduler != null)
            {
                diversionScheduler.trackCircuits = trackCircuits;
                diversionScheduler.switchPoints = switchPoints;
                diversionScheduler.signals = signals;
                diversionScheduler.normalTrains = trains;
                diversionScheduler.rogueEngineers = rogueEngineers;
                diversionScheduler.RebuildTopology();
            }
        }

        public AStarPathResult ComputePathBetweenTracks(
            string startTrackId, string goalTrackId, Direction dir,
            HashSet<string> excludeTracks = null)
        {
            return diversionScheduler?.ComputePathBetweenTracks(
                startTrackId, goalTrackId, dir, excludeTracks);
        }

        public void ClearAllDiversions()
        {
            diversionScheduler?.ClearActivePaths();
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
