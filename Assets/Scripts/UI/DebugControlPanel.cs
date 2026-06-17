using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Management;
using RailwayInterlock.Components;
using RailwayInterlock.Data;

namespace RailwayInterlock.UI
{
    public class DebugControlPanel : MonoBehaviour
    {
        public GameManager gameManager;
        public DemoScenarioSetup scenarioSetup;

        [Header("Panel Settings")]
        public int panelWidth = 380;
        public int panelX = 10;
        public int panelY = 10;
        public int headerHeight = 30;
        public int lineHeight = 24;
        public int buttonWidth = 100;
        public int buttonHeight = 28;

        private Vector2 _scrollPosition;
        private bool _showRoutes = true;
        private bool _showSwitches = true;
        private bool _showSignals = true;
        private bool _showTrains = true;
        private bool _showTracks = false;
        private bool _showHelp = false;

        private GUIStyle _headerStyle;
        private GUIStyle _sectionStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _statusLabelStyle;
        private bool _stylesInitialized = false;

        private void Start()
        {
            if (gameManager == null)
                gameManager = GameManager.Instance;
            if (scenarioSetup == null)
                scenarioSetup = FindObjectOfType<DemoScenarioSetup>();
        }

        private void InitializeStyles()
        {
            if (_stylesInitialized) return;

            _headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(1f, 0.85f, 0.2f) }
            };

            _sectionStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.5f, 0.9f, 1f) }
            };

            _buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };

            _labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12
            };

            _statusLabelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleRight
            };

            _stylesInitialized = true;
        }

        private void OnGUI()
        {
            InitializeStyles();

            GUI.skin.label.fontSize = 12;
            GUI.skin.button.fontSize = 12;

            int totalHeight = Mathf.Min(Screen.height - 20, 800);

            GUILayout.BeginArea(new Rect(panelX, panelY, panelWidth, totalHeight), GUI.skin.box);

            DrawHeader();

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, false, true,
                GUILayout.Width(panelWidth - 20),
                GUILayout.Height(totalHeight - headerHeight - 10));

            DrawSimulationControls();

            if (_showRoutes) DrawRouteControls();
            if (_showSwitches) DrawSwitchControls();
            if (_showSignals) DrawSignalStatus();
            if (_showTrains) DrawTrainStatus();
            if (_showTracks) DrawTrackStatus();
            if (_showHelp) DrawHelp();

            GUILayout.EndScrollView();
            GUILayout.EndArea();

            DrawMiniStatusBar();
            HandleKeyboardShortcuts();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("=== 铁路信号联锁仿真控制台 ===", _headerStyle, GUILayout.Width(panelWidth - 20));
            GUILayout.EndHorizontal();
            GUILayout.Space(5);
        }

        private void DrawSimulationControls()
        {
            GUILayout.Label("--- 仿真控制 ---", _sectionStyle);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button(gameManager?.IsSimulationRunning == true ? "⏸ 暂停" : "▶ 继续", _buttonStyle, GUILayout.Width(buttonWidth)))
            {
                gameManager?.ToggleSimulation();
            }

            if (GUILayout.Button("↺ 重置", _buttonStyle, GUILayout.Width(buttonWidth)))
            {
                gameManager?.ResetSimulation();
            }

            if (GUILayout.Button("⚙ 重建场景", _buttonStyle, GUILayout.Width(buttonWidth)))
            {
                scenarioSetup?.BuildDemoScenario();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("速度倍率:", GUILayout.Width(80));
            float ts = GUILayout.HorizontalSlider(Time.timeScale, 0.1f, 5f, GUILayout.Width(120));
            if (Mathf.Abs(ts - Time.timeScale) > 0.01f)
            {
                gameManager?.SetSimulationTimeScale(ts);
            }
            GUILayout.Label($"{Time.timeScale:F1}x", GUILayout.Width(50));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _showRoutes = GUILayout.Toggle(_showRoutes, "进路");
            _showSwitches = GUILayout.Toggle(_showSwitches, "道岔");
            _showSignals = GUILayout.Toggle(_showSignals, "信号机");
            _showTrains = GUILayout.Toggle(_showTrains, "列车");
            _showTracks = GUILayout.Toggle(_showTracks, "轨道");
            _showHelp = GUILayout.Toggle(_showHelp, "帮助");
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
        }

        private void DrawRouteControls()
        {
            GUILayout.Label("--- 进路管理 ---", _sectionStyle);

            if (gameManager == null) return;

            var allRoutes = scenarioSetup?.GetAllRoutes();
            if (allRoutes == null || allRoutes.Count == 0)
            {
                GUILayout.Label("  (无可用进路)", _labelStyle);
                GUILayout.Space(8);
                return;
            }

            var routeStates = gameManager.GetAllRouteStates();

            foreach (var route in allRoutes)
            {
                var state = routeStates.ContainsKey(route.Id) ? routeStates[route.Id] : RouteState.NotSet;

                GUILayout.BeginHorizontal();
                GUILayout.Label($"{route.Name}", _labelStyle, GUILayout.Width(140));

                Color stateColor = state switch
                {
                    RouteState.NotSet => Color.gray,
                    RouteState.Setting => Color.yellow,
                    RouteState.Set => Color.green,
                    RouteState.Occupied => Color.cyan,
                    RouteState.Cancelling => Color.magenta,
                    _ => Color.white
                };
                GUIStyle s = new GUIStyle(_statusLabelStyle) { normal = { textColor = stateColor } };
                GUILayout.Label(state.ToString(), s, GUILayout.Width(80));

                using (new EditorGUIDisabledScope(state != RouteState.NotSet))
                {
                    if (GUILayout.Button("建立", _buttonStyle, GUILayout.Width(50)))
                    {
                        gameManager.SetRoute(route.Id);
                    }
                }

                using (new EditorGUIDisabledScope(state == RouteState.NotSet || state == RouteState.Occupied))
                {
                    if (GUILayout.Button("取消", _buttonStyle, GUILayout.Width(50)))
                    {
                        gameManager.CancelRoute(route.Id);
                    }
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
        }

        private void DrawSwitchControls()
        {
            GUILayout.Label("--- 道岔控制 ---", _sectionStyle);

            if (gameManager == null) return;

            foreach (var sw in gameManager.switchPoints)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{sw.displayName} ", _labelStyle, GUILayout.Width(100));

                Color posColor = sw.Position == SwitchPosition.Normal ? Color.green : Color.yellow;
                GUIStyle s = new GUIStyle(_statusLabelStyle) { normal = { textColor = posColor } };
                string posStr = sw.IsMoving ? $"移动中({sw.Position}→{sw._targetPosition})" : sw.Position.ToString();
                GUILayout.Label(posStr, s, GUILayout.Width(120));

                using (new EditorGUIDisabledScope(sw.IsMoving || sw.IsOccupied()))
                {
                    if (GUILayout.Button("定位→反位", _buttonStyle, GUILayout.Width(buttonWidth)))
                    {
                        sw.SetPosition(sw.Position == SwitchPosition.Normal ? SwitchPosition.Reverse : SwitchPosition.Normal);
                    }
                }

                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
        }

        private void DrawSignalStatus()
        {
            GUILayout.Label("--- 信号机状态 ---", _sectionStyle);

            if (gameManager == null) return;

            foreach (var sig in gameManager.signals)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{sig.displayName} ", _labelStyle, GUILayout.Width(100));

                Color aspectColor = sig.Aspect switch
                {
                    SignalAspect.Red => Color.red,
                    SignalAspect.Yellow => Color.yellow,
                    SignalAspect.Green => Color.green,
                    _ => Color.white
                };
                GUIStyle s = new GUIStyle(_statusLabelStyle) { normal = { textColor = aspectColor }, fontStyle = FontStyle.Bold };
                string aspectStr = sig.Aspect switch
                {
                    SignalAspect.Red => "● 红灯",
                    SignalAspect.Yellow => "● 黄灯",
                    SignalAspect.Green => "● 绿灯",
                    _ => sig.Aspect.ToString()
                };
                GUILayout.Label(aspectStr, s, GUILayout.Width(100));

                GUILayout.Label($"→{sig.protectingTrackId}", _labelStyle);

                GUILayout.EndHorizontal();
            }
            GUILayout.Space(8);
        }

        private void DrawTrainStatus()
        {
            GUILayout.Label("--- 列车状态 ---", _sectionStyle);

            if (gameManager == null) return;

            foreach (var train in gameManager.trains)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label($"{train.displayName}", _labelStyle, GUILayout.Width(100));

                Color stateColor = train.State switch
                {
                    TrainState.Stopped => Color.gray,
                    TrainState.Moving => Color.green,
                    TrainState.Braking => Color.yellow,
                    _ => Color.white
                };
                GUIStyle s = new GUIStyle(_statusLabelStyle) { normal = { textColor = stateColor } };
                GUILayout.Label(train.State.ToString(), s, GUILayout.Width(70));

                GUILayout.Label($"{train.SpeedKmh:F1}km/h", _statusLabelStyle, GUILayout.Width(80));
                GUILayout.EndHorizontal();

                if (train.CurrentSignalAhead != null)
                {
                    GUILayout.Label($"  → 前方信号: {train.CurrentSignalAhead.displayName} - {train.CurrentSignalAhead.Aspect}", _labelStyle);
                }

                if (!string.IsNullOrEmpty(train.CurrentTrackId))
                {
                    GUILayout.Label($"  → 当前轨道: {train.CurrentTrackId}", _labelStyle);
                }

                GUILayout.BeginHorizontal();
                using (new EditorGUIDisabledScope(false))
                {
                    if (GUILayout.Button(train.IsAutomaticMode ? "切手动" : "切自动", _buttonStyle, GUILayout.Width(70)))
                    {
                        train.ToggleAutomaticMode();
                    }
                }
                if (GUILayout.Button("紧急制动", _buttonStyle, GUILayout.Width(70)))
                {
                    train.TriggerEmergencyBrake();
                }
                if (GUILayout.Button("鸣笛", _buttonStyle, GUILayout.Width(50)))
                {
                    train.PlayHorn();
                }
                GUILayout.EndHorizontal();
                GUILayout.Space(4);
            }
            GUILayout.Space(8);
        }

        private void DrawTrackStatus()
        {
            GUILayout.Label("--- 轨道区段状态 ---", _sectionStyle);

            if (gameManager == null) return;

            int cols = 2;
            int count = 0;
            GUILayout.BeginHorizontal();

            foreach (var tc in gameManager.trackCircuits)
            {
                if (count > 0 && count % cols == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }

                GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(160));
                Color c = tc.State == TrackState.Occupied ? Color.red : Color.gray;
                GUIStyle s = new GUIStyle(_labelStyle) { normal = { textColor = c }, fontStyle = FontStyle.Bold };
                GUILayout.Label($"{tc.displayName}", s);
                GUILayout.Label($"状态: {tc.State}", _labelStyle);
                GUILayout.Label($"长度: {tc.Length:F0}m", _labelStyle);
                GUILayout.EndVertical();

                count++;
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(8);
        }

        private void DrawHelp()
        {
            GUILayout.Label("--- 操作帮助 ---", _sectionStyle);
            GUILayout.Label("键盘快捷键:", _labelStyle);
            GUILayout.Label("  [Space] - 暂停/继续仿真", _labelStyle);
            GUILayout.Label("  [R] - 重置仿真", _labelStyle);
            GUILayout.Label("  [H] - 切换帮助显示", _labelStyle);
            GUILayout.Label("  [1~9] - 建立进路 1~9", _labelStyle);
            GUILayout.Label("  [Shift+1~9] - 取消进路 1~9", _labelStyle);
            GUILayout.Label("  [Q/W/E] - 切换道岔1/2/3", _labelStyle);
            GUILayout.Label("  [A/S/D] - 道岔4/5/6", _labelStyle);
            GUILayout.Label("  [↑] - 加速(手动模式)", _labelStyle);
            GUILayout.Label("  [↓] - 减速(手动模式)", _labelStyle);
            GUILayout.Label("  [F] - 鸣笛", _labelStyle);

            GUILayout.Space(6);
            GUILayout.Label("信号显示规则:", _labelStyle);
            GUILayout.Label("  ● 红灯 - 禁止通行 (前方进路未建立/区段占用)", _labelStyle);
            GUILayout.Label("  ● 黄灯 - 减速运行 (下一闭塞分区占用/出站信号黄灯)", _labelStyle);
            GUILayout.Label("  ● 绿灯 - 正常运行 (前方闭塞分区空闲)", _labelStyle);

            GUILayout.Space(6);
            GUILayout.Label("道岔位置:", _labelStyle);
            GUILayout.Label("  定位(Normal) - 直线方向", _labelStyle);
            GUILayout.Label("  反位(Reverse) - 侧向/分支方向", _labelStyle);
            GUILayout.Space(8);
        }

        private void DrawMiniStatusBar()
        {
            int barWidth = 300;
            int barHeight = 30;
            int barX = Screen.width - barWidth - 10;
            int barY = 10;

            GUILayout.BeginArea(new Rect(barX, barY, barWidth, barHeight), GUI.skin.box);
            GUILayout.BeginHorizontal();

            Color simColor = gameManager?.IsSimulationRunning == true ? Color.green : Color.yellow;
            GUIStyle s1 = new GUIStyle(_labelStyle) { normal = { textColor = simColor }, fontStyle = FontStyle.Bold };
            GUILayout.Label(gameManager?.IsSimulationRunning == true ? "● 运行中" : "⏸ 已暂停", s1, GUILayout.Width(80));

            GUILayout.Label($"速度: {Time.timeScale:F1}x", _labelStyle, GUILayout.Width(80));

            int occCount = gameManager?.trackCircuits?.FindAll(t => t.State == TrackState.Occupied).Count ?? 0;
            int totalCount = gameManager?.trackCircuits?.Count ?? 0;
            GUILayout.Label($"轨道占用: {occCount}/{totalCount}", _labelStyle);

            GUILayout.EndHorizontal();
            GUILayout.EndArea();
        }

        private void HandleKeyboardShortcuts()
        {
            if (gameManager == null) return;

            if (Event.current.type == EventType.KeyDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.Space:
                        gameManager.ToggleSimulation();
                        Event.current.Use();
                        break;
                    case KeyCode.R:
                        gameManager.ResetSimulation();
                        Event.current.Use();
                        break;
                    case KeyCode.H:
                        _showHelp = !_showHelp;
                        Event.current.Use();
                        break;
                    case KeyCode.F:
                        foreach (var t in gameManager.trains) t.PlayHorn();
                        Event.current.Use();
                        break;
                    case KeyCode.Q:
                        ToggleSwitch(0);
                        Event.current.Use();
                        break;
                    case KeyCode.W:
                        ToggleSwitch(1);
                        Event.current.Use();
                        break;
                    case KeyCode.E:
                        ToggleSwitch(2);
                        Event.current.Use();
                        break;
                    case KeyCode.A:
                        ToggleSwitch(3);
                        Event.current.Use();
                        break;
                    case KeyCode.S:
                        ToggleSwitch(4);
                        Event.current.Use();
                        break;
                    case KeyCode.D:
                        ToggleSwitch(5);
                        Event.current.Use();
                        break;
                    case KeyCode.Alpha1: case KeyCode.Alpha2: case KeyCode.Alpha3:
                    case KeyCode.Alpha4: case KeyCode.Alpha5: case KeyCode.Alpha6:
                    case KeyCode.Alpha7: case KeyCode.Alpha8: case KeyCode.Alpha9:
                        int idx = (int)Event.current.keyCode - (int)KeyCode.Alpha1;
                        if (Event.current.shift)
                            CancelRoute(idx);
                        else
                            SetRoute(idx);
                        Event.current.Use();
                        break;
                }
            }
        }

        private void ToggleSwitch(int index)
        {
            if (gameManager.switchPoints != null && index < gameManager.switchPoints.Count)
            {
                gameManager.switchPoints[index].TogglePosition();
            }
        }

        private void SetRoute(int index)
        {
            var routes = scenarioSetup?.GetAllRoutes();
            if (routes != null && index < routes.Count)
            {
                gameManager.SetRoute(routes[index].Id);
            }
        }

        private void CancelRoute(int index)
        {
            var routes = scenarioSetup?.GetAllRoutes();
            if (routes != null && index < routes.Count)
            {
                gameManager.CancelRoute(routes[index].Id);
            }
        }

        private struct EditorGUIDisabledScope : System.IDisposable
        {
            private readonly bool _wasEnabled;
            public EditorGUIDisabledScope(bool disabled)
            {
                _wasEnabled = GUI.enabled;
                GUI.enabled = !disabled && _wasEnabled;
            }
            public void Dispose()
            {
                GUI.enabled = _wasEnabled;
            }
        }
    }
}
