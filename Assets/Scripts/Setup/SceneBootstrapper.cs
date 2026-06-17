using UnityEngine;
using RailwayInterlock.Management;
using RailwayInterlock.UI;
using RailwayInterlock.CameraSystem;

namespace RailwayInterlock.Setup
{
    public class SceneBootstrapper : MonoBehaviour
    {
        public static SceneBootstrapper Instance { get; private set; }

        [Header("Auto Setup")]
        public bool autoSetupOnStart = true;
        public bool createGround = true;
        public bool createLights = true;
        public bool createCamera = true;
        public bool createGameManager = true;
        public bool createScenario = true;
        public bool createDebugUI = true;

        [Header("Ground Settings")]
        public float groundSize = 500f;
        public Color groundColor = new Color(0.25f, 0.4f, 0.2f);

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
            if (autoSetupOnStart)
            {
                SetupScene();
            }
        }

        public void SetupScene()
        {
            if (createGround) CreateGround();
            if (createLights) CreateLighting();
            if (createCamera) CreateCamera();

            GameObject managerObj = null;
            if (createGameManager)
            {
                managerObj = CreateGameManager();
            }

            if (createScenario && managerObj != null)
            {
                CreateScenario(managerObj);
            }

            if (createDebugUI)
            {
                CreateDebugUI();
            }

            Debug.Log("[SceneBootstrapper] 场景初始化完成！");
        }

        private void CreateGround()
        {
            GameObject existing = GameObject.Find("Ground");
            if (existing != null) return;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(spacingBetweenTracks, -0.1f, 0);
            ground.transform.localScale = new Vector3(groundSize / 10f, 1, groundSize / 10f);
            Destroy(ground.GetComponent<MeshCollider>());
            MeshCollider col = ground.AddComponent<MeshCollider>();

            MeshRenderer mr = ground.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = groundColor;
            mat.SetFloat("_Glossiness", 0.1f);
            mr.material = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        private void CreateLighting()
        {
            Light dirLight = FindObjectOfType<Light>();
            if (dirLight == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                dirLight = lightObj.AddComponent<Light>();
            }
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.96f, 0.9f);
            dirLight.intensity = 1.2f;
            dirLight.shadows = LightShadows.Soft;
            dirLight.shadowStrength = 0.6f;
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.45f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.25f, 0.2f);
            RenderSettings.ambientIntensity = 0.8f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.7f, 0.75f, 0.8f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 150f;
            RenderSettings.fogEndDistance = 400f;
        }

        private void CreateCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                GameObject camObj = new GameObject("Main Camera");
                camObj.tag = "MainCamera";
                cam = camObj.AddComponent<Camera>();
                camObj.AddComponent<AudioListener>();
            }

            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = new Color(0.5f, 0.7f, 0.9f);
            cam.fieldOfView = 50f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 1000f;
            cam.transform.position = new Vector3(spacingBetweenTracks, 80f, -50f);
            cam.transform.rotation = Quaternion.Euler(55f, 0f, 0f);

            TopDownCameraController controller = cam.gameObject.GetComponent<TopDownCameraController>();
            if (controller == null)
            {
                controller = cam.gameObject.AddComponent<TopDownCameraController>();
            }
            controller.minHeight = 15f;
            controller.maxHeight = 200f;
            controller.minAngle = 20f;
            controller.maxAngle = 85f;
            controller.panSpeed = 60f;
            controller.zoomSpeed = 120f;
            controller.offset = new Vector3(spacingBetweenTracks, 80f, -50f);
        }

        private GameObject CreateGameManager()
        {
            GameObject existing = GameObject.Find("GameManager");
            if (existing != null) return existing;

            GameObject managerObj = new GameObject("GameManager");
            GameManager gm = managerObj.AddComponent<GameManager>();
            gm.interlockEvaluationInterval = 0.05f;
            gm.autoEvaluateInterlock = true;
            gm.enableDebugLogging = true;

            return managerObj;
        }

        private void CreateScenario(GameObject managerObj)
        {
            DemoScenarioSetup setup = managerObj.GetComponent<DemoScenarioSetup>();
            if (setup == null)
            {
                setup = managerObj.AddComponent<DemoScenarioSetup>();
            }
            setup.autoBuildOnStart = false;
            setup.BuildDemoScenario();
        }

        private void CreateDebugUI()
        {
            GameObject existing = GameObject.Find("DebugUIController");
            if (existing != null) return;

            GameObject uiObj = new GameObject("DebugUIController");
            DebugControlPanel panel = uiObj.AddComponent<DebugControlPanel>();
            panel.gameManager = GameManager.Instance;
            panel.scenarioSetup = FindObjectOfType<DemoScenarioSetup>();
        }

        private float spacingBetweenTracks = 10f;
    }
}
