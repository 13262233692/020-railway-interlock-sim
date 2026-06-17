#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using RailwayInterlock.Setup;
using RailwayInterlock.Management;

namespace RailwayInterlock.EditorTools
{
    public static class RailwaySceneSetup
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";
        private const string SceneFolder = "Assets/Scenes";

        [MenuItem("铁路联锁仿真/初始化项目...", priority = 1)]
        public static void InitializeProject()
        {
            if (!EditorUtility.DisplayDialog(
                "初始化铁路联锁仿真项目",
                "将创建必要的文件夹、场景和配置。是否继续？\n\n这将：\n1. 创建 Assets/Scenes 目录\n2. 创建主场景并自动构建演示站场\n3. 创建引导启动器",
                "确定",
                "取消"))
            {
                return;
            }

            CreateFolders();
            CreateMainScene();

            EditorUtility.DisplayDialog(
                "初始化完成",
                "项目初始化成功！\n\n请打开 Assets/Scenes/Main.unity 场景，\n点击 Play 按钮即可运行仿真。",
                "好的");
        }

        [MenuItem("铁路联锁仿真/打开主场景", priority = 10)]
        public static void OpenMainScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                if (EditorUtility.DisplayDialog(
                    "场景不存在",
                    "主场景尚未创建，是否立即创建？",
                    "创建",
                    "取消"))
                {
                    CreateMainScene();
                }
                return;
            }
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("铁路联锁仿真/工具/重建演示场景", priority = 20)]
        public static void RebuildDemoScene()
        {
            var setup = Object.FindObjectOfType<Setup.DemoScenarioSetup>();
            if (setup == null)
            {
                var gm = Object.FindObjectOfType<GameManager>();
                if (gm != null)
                {
                    setup = gm.gameObject.AddComponent<Setup.DemoScenarioSetup>();
                }
                else
                {
                    GameObject go = new GameObject("GameManager");
                    go.AddComponent<GameManager>();
                    setup = go.AddComponent<Setup.DemoScenarioSetup>();
                }
            }
            setup.BuildDemoScenario();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("完成", "演示场景已重建", "OK");
        }

        [MenuItem("铁路联锁仿真/工具/创建引导启动器", priority = 21)]
        public static void CreateBootstrapper()
        {
            GameObject existing = GameObject.Find("SceneBootstrapper");
            if (existing != null)
            {
                Selection.activeGameObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            GameObject boot = new GameObject("SceneBootstrapper");
            boot.AddComponent<SceneBootstrapper>();
            Selection.activeGameObject = boot;
            EditorGUIUtility.PingObject(boot);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("铁路联锁仿真/文档/架构说明", priority = 100)]
        public static void ShowArchitectureInfo()
        {
            EditorUtility.DisplayDialog(
                "铁路信号联锁仿真 - 架构说明",
                "【核心模块】\n\n" +
                "1. Core (核心)\n" +
                "   - Enums: 信号显示、道岔位置、轨道状态等枚举\n" +
                "   - Interfaces: 联锁系统各组件接口定义\n" +
                "   - DataStructures: 轨道/道岔/信号/进路数据结构\n\n" +
                "2. Interlocking (联锁逻辑)\n" +
                "   - BooleanLogicEvaluator: 布尔逻辑计算器\n" +
                "   - InterlockController: 联锁控制器\n\n" +
                "3. Components (Unity组件)\n" +
                "   - TrackCircuit: 轨道区段(碰撞检测+占用)\n" +
                "   - SwitchPoint: 道岔(定位/反位转换)\n" +
                "   - Signal: 信号机(红/黄/绿灯显示)\n" +
                "   - Train: 列车(自动行驶+制动)\n\n" +
                "4. Management (管理)\n" +
                "   - GameManager: 全局控制器\n\n" +
                "5. Scenarios (场景)\n" +
                "   - ScenarioBuilder: 程序化场景构建\n" +
                "   - DemoScenarioSetup: 演示站场配置(3股道+4道岔)\n\n" +
                "6. UI (界面)\n" +
                "   - DebugControlPanel: 调试控制台GUI\n\n" +
                "7. CameraSystem (摄像机)\n" +
                "   - TopDownCameraController: 俯视角相机控制\n\n" +
                "8. Setup (启动引导)\n" +
                "   - SceneBootstrapper: 场景引导器",
                "了解了");
        }

        [MenuItem("铁路联锁仿真/文档/操作指南", priority = 101)]
        public static void ShowOperationGuide()
        {
            EditorUtility.DisplayDialog(
                "操作指南",
                "【运行仿真】\n" +
                "1. 打开 Main 场景\n" +
                "2. 点击 Play 按钮\n" +
                "3. 场景将自动构建演示站场(3股道、4道岔、9信号机、2列车)\n\n" +
                "【使用控制面板】\n" +
                "左上角为控制台:\n" +
                "- 仿真控制: 暂停/继续/重置/速度倍率\n" +
                "- 进路管理: 点击\"建立\"按钮开通进路\n" +
                "- 道岔控制: 切换道岔定位/反位\n" +
                "- 信号机状态: 实时显示红/黄/绿灯\n" +
                "- 列车状态: 速度/前方信号/当前轨道\n\n" +
                "【键盘快捷键】\n" +
                "Space: 暂停/继续\n" +
                "R: 重置仿真\n" +
                "1~9: 建立进路 (Shift+取消)\n" +
                "Q/W/E: 切换道岔1/2/3\n" +
                "A/S/D: 切换道岔4/5/6\n" +
                "F: 鸣笛\n" +
                "鼠标滚轮: 缩放视图\n" +
                "鼠标中键拖拽: 平移视图\n" +
                "鼠标右键拖拽: 旋转视角",
                "开始使用");
        }

        private static void CreateFolders()
        {
            string[] folders = new string[]
            {
                "Assets/Scenes",
                "Assets/Prefabs",
                "Assets/Materials",
                "Assets/Textures",
                "Assets/Audio"
            };

            foreach (var folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                {
                    string parent = System.IO.Path.GetDirectoryName(folder).Replace("\\", "/");
                    string name = System.IO.Path.GetFileName(folder);
                    AssetDatabase.CreateFolder(parent, name);
                }
            }

            AssetDatabase.Refresh();
            Debug.Log("[RailwaySceneSetup] 文件夹创建完成");
        }

        private static void CreateMainScene()
        {
            if (!AssetDatabase.IsValidFolder(SceneFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Scenes");
            }

            Scene scene;
            if (System.IO.File.Exists(ScenePath))
            {
                scene = EditorSceneManager.OpenScene(ScenePath);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            GameObject boot = new GameObject("SceneBootstrapper");
            var sb = boot.AddComponent<SceneBootstrapper>();
            sb.autoSetupOnStart = true;

            EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[RailwaySceneSetup] 场景已保存: {ScenePath}");
            EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
#endif
