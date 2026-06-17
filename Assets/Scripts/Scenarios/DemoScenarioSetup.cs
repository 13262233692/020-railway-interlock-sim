using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Components;
using RailwayInterlock.Management;

namespace RailwayInterlock.Scenarios
{
    [RequireComponent(typeof(GameManager))]
    public class DemoScenarioSetup : MonoBehaviour
    {
        private GameManager _gameManager;

        public bool autoBuildOnStart = true;
        public float trackWidth = 3f;
        public float trackSegmentLength = 30f;
        public float spacingBetweenTracks = 10f;
        public float signalOffset = 2f;

        private readonly List<TrackCircuit> _createdTracks = new List<TrackCircuit>();
        private readonly List<SwitchPoint> _createdSwitches = new List<SwitchPoint>();
        private readonly List<Signal> _createdSignals = new List<Signal>();
        private readonly List<Train> _createdTrains = new List<Train>();
        private readonly List<RouteData> _createdRoutes = new List<RouteData>();

        private void Start()
        {
            _gameManager = GetComponent<GameManager>();

            if (autoBuildOnStart)
            {
                BuildDemoScenario();
            }
        }

        public void BuildDemoScenario()
        {
            ClearAll();
            BuildTrackLayout();
            BuildSwitches();
            BuildSignals();
            BuildTrains();
            BuildRoutes();
            PopulateGameManager();
            _gameManager.StartSimulation();
            Debug.Log("[DemoScenarioSetup] 演示场景构建完成");
        }

        private void BuildTrackLayout()
        {
            _createdTracks.Add(CreateTrack("T1-1", "1道-段1", new Vector3(0, 0, -30), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T1-2", "1道-段2", new Vector3(0, 0, 0), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T1-3", "1道-段3", new Vector3(0, 0, 30), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T1-4", "1道-段4", new Vector3(0, 0, 60), new Vector3(3, trackSegmentLength)));

            _createdTracks.Add(CreateTrack("T2-1", "2道-段1", new Vector3(0, 0, -30 + spacingBetweenTracks), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T2-2", "2道-段2", new Vector3(0, 0, 0 + spacingBetweenTracks), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T2-3", "2道-段3", new Vector3(0, 0, 30 + spacingBetweenTracks), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T2-4", "2道-段4", new Vector3(0, 0, 60 + spacingBetweenTracks), new Vector3(3, trackSegmentLength)));

            _createdTracks.Add(CreateTrack("T3-1", "3道-段1", new Vector3(0, 0, -30 + spacingBetweenTracks * 2), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T3-2", "3道-段2", new Vector3(0, 0, 0 + spacingBetweenTracks * 2), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T3-3", "3道-段3", new Vector3(0, 0, 30 + spacingBetweenTracks * 2), new Vector3(3, trackSegmentLength)));
            _createdTracks.Add(CreateTrack("T3-4", "3道-段4", new Vector3(0, 0, 60 + spacingBetweenTracks * 2), new Vector3(3, trackSegmentLength)));

            _createdTracks.Add(CreateTrack("SD1", "渡线1-2", new Vector3(spacingBetweenTracks / 2, 0, 15), new Vector3(spacingBetweenTracks, trackSegmentLength / 2), 45));
            _createdTracks.Add(CreateTrack("SD2", "渡线2-3", new Vector3(spacingBetweenTracks * 1.5f, 0, 15), new Vector3(spacingBetweenTracks, trackSegmentLength / 2), 45));
            _createdTracks.Add(CreateTrack("SD3", "渡线1-2反", new Vector3(spacingBetweenTracks / 2, 0, -15), new Vector3(spacingBetweenTracks, trackSegmentLength / 2), -45));
            _createdTracks.Add(CreateTrack("SD4", "渡线2-3反", new Vector3(spacingBetweenTracks * 1.5f, 0, -15), new Vector3(spacingBetweenTracks, trackSegmentLength / 2), -45));
        }

        private void BuildSwitches()
        {
            _createdSwitches.Add(CreateSwitch("SW1", "道岔1", new Vector3(spacingBetweenTracks / 2, 0, 30), "T1-3", "T1-3", "SD1"));
            _createdSwitches.Add(CreateSwitch("SW2", "道岔2", new Vector3(spacingBetweenTracks * 1.5f, 0, 30), "T2-3", "T2-3", "SD2"));
            _createdSwitches.Add(CreateSwitch("SW3", "道岔3", new Vector3(spacingBetweenTracks / 2, 0, 0), "T1-2", "T1-2", "SD3"));
            _createdSwitches.Add(CreateSwitch("SW4", "道岔4", new Vector3(spacingBetweenTracks * 1.5f, 0, 0), "T2-2", "T2-2", "SD4"));
        }

        private void BuildSignals()
        {
            _createdSignals.Add(CreateSignal("S1", "进站1", new Vector3(-signalOffset, 0, -45), 90, Direction.Up, "T1-1"));
            _createdSignals.Add(CreateSignal("S2", "进站2", new Vector3(-signalOffset + spacingBetweenTracks, 0, -45), 90, Direction.Up, "T2-1"));
            _createdSignals.Add(CreateSignal("S3", "进站3", new Vector3(-signalOffset + spacingBetweenTracks * 2, 0, -45), 90, Direction.Up, "T3-1"));

            _createdSignals.Add(CreateSignal("X1", "出站1", new Vector3(signalOffset, 0, 75), 270, Direction.Down, "T1-4"));
            _createdSignals.Add(CreateSignal("X2", "出站2", new Vector3(signalOffset + spacingBetweenTracks, 0, 75), 270, Direction.Down, "T2-4"));
            _createdSignals.Add(CreateSignal("X3", "出站3", new Vector3(signalOffset + spacingBetweenTracks * 2, 0, 75), 270, Direction.Down, "T3-4"));

            _createdSignals.Add(CreateSignal("SZ1", "中转1", new Vector3(-signalOffset, 0, 0), 90, Direction.Up, "T1-2"));
            _createdSignals.Add(CreateSignal("SZ2", "中转2", new Vector3(-signalOffset + spacingBetweenTracks, 0, 0), 90, Direction.Up, "T2-2"));
            _createdSignals.Add(CreateSignal("SZ3", "中转3", new Vector3(-signalOffset + spacingBetweenTracks * 2, 0, 0), 90, Direction.Up, "T3-2"));
        }

        private void BuildTrains()
        {
            Train t1 = CreateTrain("T001", "红箭1号", new Vector3(0, 0, -60), 90, Direction.Up, 80);
            _createdTrains.Add(t1);

            Train t2 = CreateTrain("T002", "白驹2号", new Vector3(spacingBetweenTracks, 0, 80), 270, Direction.Down, 60);
            _createdTrains.Add(t2);
        }

        private void BuildRoutes()
        {
            _createdRoutes.Add(new RouteData
            {
                Id = "ROUTE_UP_1",
                Name = "上行1道通过",
                EntrySignalId = "S1",
                ExitSignalId = "X1",
                Direction = Direction.Up,
                TrackSequence = new List<string> { "T1-1", "T1-2", "T1-3", "T1-4" },
                SwitchRequirements = new List<SwitchPositionRequirement>
                {
                    new SwitchPositionRequirement { SwitchId = "SW1", RequiredPosition = SwitchPosition.Normal },
                    new SwitchPositionRequirement { SwitchId = "SW3", RequiredPosition = SwitchPosition.Normal }
                },
                ConflictingRoutes = new List<string> { "ROUTE_UP_1_TO_2", "ROUTE_DOWN_1", "ROUTE_UP_SHUNT_1_2" },
                SpeedLimitKmh = 60
            });

            _createdRoutes.Add(new RouteData
            {
                Id = "ROUTE_UP_1_TO_2",
                Name = "上行1道转2道",
                EntrySignalId = "S1",
                ExitSignalId = "X2",
                Direction = Direction.Up,
                TrackSequence = new List<string> { "T1-1", "T1-2", "SD3", "T2-3", "T2-4" },
                SwitchRequirements = new List<SwitchPositionRequirement>
                {
                    new SwitchPositionRequirement { SwitchId = "SW1", RequiredPosition = SwitchPosition.Normal },
                    new SwitchPositionRequirement { SwitchId = "SW3", RequiredPosition = SwitchPosition.Reverse }
                },
                ConflictingRoutes = new List<string> { "ROUTE_UP_1", "ROUTE_DOWN_2", "ROUTE_UP_SHUNT_1_2" },
                SpeedLimitKmh = 45
            });

            _createdRoutes.Add(new RouteData
            {
                Id = "ROUTE_DOWN_2",
                Name = "下行2道通过",
                EntrySignalId = "X2",
                ExitSignalId = "S2",
                Direction = Direction.Down,
                TrackSequence = new List<string> { "T2-4", "T2-3", "T2-2", "T2-1" },
                SwitchRequirements = new List<SwitchPositionRequirement>
                {
                    new SwitchPositionRequirement { SwitchId = "SW2", RequiredPosition = SwitchPosition.Normal },
                    new SwitchPositionRequirement { SwitchId = "SW4", RequiredPosition = SwitchPosition.Normal }
                },
                ConflictingRoutes = new List<string> { "ROUTE_UP_1_TO_2", "ROUTE_UP_2", "ROUTE_DOWN_SHUNT_2_3" },
                SpeedLimitKmh = 60
            });

            _createdRoutes.Add(new RouteData
            {
                Id = "ROUTE_UP_2",
                Name = "上行2道通过",
                EntrySignalId = "S2",
                ExitSignalId = "X2",
                Direction = Direction.Up,
                TrackSequence = new List<string> { "T2-1", "T2-2", "T2-3", "T2-4" },
                SwitchRequirements = new List<SwitchPositionRequirement>
                {
                    new SwitchPositionRequirement { SwitchId = "SW2", RequiredPosition = SwitchPosition.Normal },
                    new SwitchPositionRequirement { SwitchId = "SW4", RequiredPosition = SwitchPosition.Normal }
                },
                ConflictingRoutes = new List<string> { "ROUTE_DOWN_2", "ROUTE_UP_2_TO_3" },
                SpeedLimitKmh = 60
            });

            _createdRoutes.Add(new RouteData
            {
                Id = "ROUTE_UP_3",
                Name = "上行3道通过",
                EntrySignalId = "S3",
                ExitSignalId = "X3",
                Direction = Direction.Up,
                TrackSequence = new List<string> { "T3-1", "T3-2", "T3-3", "T3-4" },
                SwitchRequirements = new List<SwitchPositionRequirement>(),
                ConflictingRoutes = new List<string> { "ROUTE_DOWN_3", "ROUTE_UP_2_TO_3" },
                SpeedLimitKmh = 60
            });

            _createdRoutes.Add(new RouteData
            {
                Id = "ROUTE_DOWN_3",
                Name = "下行3道通过",
                EntrySignalId = "X3",
                ExitSignalId = "S3",
                Direction = Direction.Down,
                TrackSequence = new List<string> { "T3-4", "T3-3", "T3-2", "T3-1" },
                SwitchRequirements = new List<SwitchPositionRequirement>(),
                ConflictingRoutes = new List<string> { "ROUTE_UP_3" },
                SpeedLimitKmh = 60
            });

            _createdRoutes.Add(new RouteData
            {
                Id = "ROUTE_UP_2_TO_3",
                Name = "上行2道转3道",
                EntrySignalId = "S2",
                ExitSignalId = "X3",
                Direction = Direction.Up,
                TrackSequence = new List<string> { "T2-1", "T2-2", "SD4", "T3-3", "T3-4" },
                SwitchRequirements = new List<SwitchPositionRequirement>
                {
                    new SwitchPositionRequirement { SwitchId = "SW4", RequiredPosition = SwitchPosition.Reverse },
                    new SwitchPositionRequirement { SwitchId = "SW2", RequiredPosition = SwitchPosition.Normal }
                },
                ConflictingRoutes = new List<string> { "ROUTE_UP_2", "ROUTE_UP_3", "ROUTE_DOWN_3" },
                SpeedLimitKmh = 45
            });
        }

        private TrackCircuit CreateTrack(string id, string name, Vector3 position, Vector2 sizeXY, float yRotation = 0)
        {
            GameObject go = new GameObject($"Track_{id}");
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yRotation, 0);

            TrackCircuit tc = go.AddComponent<TrackCircuit>();
            tc.trackId = id;
            tc.displayName = name;
            tc.length = sizeXY.y;

            BoxCollider col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, 0.5f, 0);
            col.size = new Vector3(sizeXY.x, 1f, sizeXY.y);

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.mesh = CreateTrackMesh(sizeXY);

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.25f, 0.25f, 0.25f);
            mat.SetFloat("_Glossiness", 0.3f);
            mr.material = mat;
            tc.trackRenderer = mr;

            CreateSleepers(go.transform, sizeXY);

            return tc;
        }

        private SwitchPoint CreateSwitch(string id, string name, Vector3 position, string common, string normal, string reverse)
        {
            GameObject go = new GameObject($"Switch_{id}");
            go.transform.SetParent(transform);
            go.transform.position = position;

            SwitchPoint sw = go.AddComponent<SwitchPoint>();
            sw.switchId = id;
            sw.displayName = name;
            sw.switchType = SwitchType.Single;
            sw.commonTrackId = common;
            sw.normalTrackId = normal;
            sw.reverseTrackId = reverse;
            sw.switchTime = 2f;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "SwitchVisual";
            visual.transform.SetParent(go.transform);
            visual.transform.localPosition = new Vector3(0, 0.15f, 0);
            visual.transform.localScale = new Vector3(2f, 0.3f, 4f);
            Destroy(visual.GetComponent<BoxCollider>());

            MeshRenderer mr = visual.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.4f, 0.4f, 0.5f);
            mr.material = mat;

            sw.movingRail = visual.transform;
            sw.normalPositionOffset = new Vector3(-1.5f, 0, 0);
            sw.reversePositionOffset = new Vector3(1.5f, 0, 0);

            return sw;
        }

        private Signal CreateSignal(string id, string name, Vector3 position, float yRotation, Direction dir, string protectingTrack)
        {
            GameObject go = new GameObject($"Signal_{id}");
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yRotation, 0);

            Signal sig = go.AddComponent<Signal>();
            sig.signalId = id;
            sig.displayName = name;
            sig.direction = dir;
            sig.protectingTrackId = protectingTrack;
            sig.detectionDistance = 40f;

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.name = "Post";
            post.transform.SetParent(go.transform);
            post.transform.localPosition = new Vector3(0, 1.5f, 0);
            post.transform.localScale = new Vector3(0.12f, 1.5f, 0.12f);
            Destroy(post.GetComponent<CapsuleCollider>());

            GameObject panel = new GameObject("LightPanel");
            panel.transform.SetParent(go.transform);
            panel.transform.localPosition = new Vector3(0, 3.2f, 0);
            panel.transform.localScale = Vector3.one;

            sig.redLight = CreateLightMesh("RedLight", panel.transform, new Vector3(0, 0.5f, 0), new Color(0.5f, 0.1f, 0.1f));
            sig.yellowLight = CreateLightMesh("YellowLight", panel.transform, new Vector3(0, 0f, 0), new Color(0.5f, 0.4f, 0.1f));
            sig.greenLight = CreateLightMesh("GreenLight", panel.transform, new Vector3(0, -0.5f, 0), new Color(0.1f, 0.45f, 0.15f));

            BoxCollider detectionZone = go.AddComponent<BoxCollider>();
            detectionZone.isTrigger = true;
            detectionZone.center = new Vector3(0, 1.5f, sig.detectionDistance / 2f);
            detectionZone.size = new Vector3(3f, 3f, sig.detectionDistance);
            sig.stopZone = detectionZone;

            return sig;
        }

        private Renderer CreateLightMesh(string name, Transform parent, Vector3 localPos, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.35f;
            Destroy(go.GetComponent<SphereCollider>());

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_Glossiness", 0.8f);
            mr.material = mat;

            return mr;
        }

        private Train CreateTrain(string id, string name, Vector3 position, float yRotation, Direction dir, float maxSpeed)
        {
            GameObject go = new GameObject($"Train_{id}");
            go.transform.SetParent(transform);
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0, yRotation, 0);

            TrainSpawnPoint spawn = go.AddComponent<TrainSpawnPoint>();
            spawn.spawnPointId = $"SPAWN_{id}";

            Train train = go.AddComponent<Train>();
            train.trainId = id;
            train.displayName = name;
            train.travelDirection = dir;
            train.maxSpeedKmh = maxSpeed;
            train.acceleration = 1.5f;
            train.brakeDeceleration = 4f;
            train.emergencyBrakeDeceleration = 8f;
            train.signalDetectionRange = 35f;

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = 30000f;
            rb.drag = 0.05f;
            rb.angularDrag = 1f;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            BoxCollider bodyCol = go.AddComponent<BoxCollider>();
            bodyCol.center = new Vector3(0, 1.5f, 0);
            bodyCol.size = new Vector3(2.2f, 3f, 14f);

            GameObject detectionPoint = new GameObject("SignalDetectionPoint");
            detectionPoint.transform.SetParent(go.transform);
            detectionPoint.transform.localPosition = new Vector3(0, 2.5f, 7.5f);
            train.signalDetectionPoint = detectionPoint.transform;

            CreateTrainBody(go.transform);

            return train;
        }

        private void CreateTrainBody(Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(parent);
            body.transform.localPosition = new Vector3(0, 1.5f, 0);
            body.transform.localScale = new Vector3(2.2f, 3f, 14f);
            Destroy(body.GetComponent<BoxCollider>());
            Material bodyMat = new Material(Shader.Find("Standard"));
            bodyMat.color = new Color(0.85f, 0.1f, 0.1f);
            bodyMat.SetFloat("_Glossiness", 0.5f);
            body.GetComponent<MeshRenderer>().material = bodyMat;

            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cabin";
            cabin.transform.SetParent(parent);
            cabin.transform.localPosition = new Vector3(0, 3.5f, 4.5f);
            cabin.transform.localScale = new Vector3(2f, 1.2f, 3f);
            Destroy(cabin.GetComponent<BoxCollider>());
            Material cabinMat = new Material(Shader.Find("Standard"));
            cabinMat.color = new Color(0.15f, 0.15f, 0.35f);
            cabin.GetComponent<MeshRenderer>().material = cabinMat;

            GameObject windowFront = GameObject.CreatePrimitive(PrimitiveType.Cube);
            windowFront.name = "WindowFront";
            windowFront.transform.SetParent(parent);
            windowFront.transform.localPosition = new Vector3(0, 3.6f, 6.5f);
            windowFront.transform.localScale = new Vector3(1.7f, 0.8f, 0.1f);
            Destroy(windowFront.GetComponent<BoxCollider>());
            Material windowMat = new Material(Shader.Find("Standard"));
            windowMat.color = new Color(0.5f, 0.8f, 0.9f);
            windowFront.GetComponent<MeshRenderer>().material = windowMat;

            for (int i = 0; i < 4; i++)
            {
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Wheel_{i}";
                wheel.transform.SetParent(parent);
                wheel.transform.localPosition = new Vector3(
                    i % 2 == 0 ? -1.1f : 1.1f,
                    0.5f,
                    i < 2 ? -4.5f : 4.5f);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                wheel.transform.localScale = new Vector3(0.7f, 0.25f, 0.7f);
                Destroy(wheel.GetComponent<CapsuleCollider>());
            }

            GameObject smoke = new GameObject("BrakeSmoke");
            smoke.transform.SetParent(parent);
            smoke.transform.localPosition = new Vector3(0, 0.5f, -6f);
            ParticleSystem ps = smoke.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startColor = new Color(0.3f, 0.3f, 0.3f, 0.6f);
            main.startSize = 0.5f;
            main.startLifetime = 2f;
            main.loop = false;
            main.maxParticles = 100;
            train.brakeSmoke = ps;
        }

        private void CreateSleepers(Transform parent, Vector2 trackSize)
        {
            int sleeperCount = Mathf.FloorToInt(trackSize.y / 2f);
            float startZ = -trackSize.y / 2f + 1f;

            for (int i = 0; i < sleeperCount; i++)
            {
                GameObject sleeper = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sleeper.name = $"Sleeper_{i}";
                sleeper.transform.SetParent(parent);
                sleeper.transform.localPosition = new Vector3(0, 0.05f, startZ + i * 2f);
                sleeper.transform.localScale = new Vector3(trackSize.x * 1.2f, 0.1f, 0.3f);
                Destroy(sleeper.GetComponent<BoxCollider>());

                MeshRenderer mr = sleeper.GetComponent<MeshRenderer>();
                Material mat = new Material(Shader.Find("Standard"));
                mat.color = new Color(0.35f, 0.2f, 0.1f);
                mr.material = mat;
            }

            float railOffset = trackSize.x * 0.35f;
            CreateRail(parent, trackSize, -railOffset);
            CreateRail(parent, trackSize, railOffset);
        }

        private void CreateRail(Transform parent, Vector2 trackSize, float xOffset)
        {
            GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rail.name = $"Rail_{(xOffset < 0 ? "L" : "R")}";
            rail.transform.SetParent(parent);
            rail.transform.localPosition = new Vector3(xOffset, 0.18f, 0);
            rail.transform.localScale = new Vector3(0.15f, 0.2f, trackSize.y);
            Destroy(rail.GetComponent<BoxCollider>());

            MeshRenderer mr = rail.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = new Color(0.7f, 0.7f, 0.75f);
            mat.SetFloat("_Glossiness", 0.9f);
            mat.SetFloat("_Metallic", 0.8f);
            mr.material = mat;
        }

        private Mesh CreateTrackMesh(Vector2 size)
        {
            Mesh mesh = new Mesh();
            mesh.name = "TrackBallast";

            float halfX = size.x * 0.7f;
            float halfY = 0.02f;
            float halfZ = size.y * 0.5f;

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-halfX, -halfY, -halfZ),
                new Vector3(halfX, -halfY, -halfZ),
                new Vector3(halfX, halfY, -halfZ),
                new Vector3(-halfX, halfY, -halfZ),
                new Vector3(-halfX, -halfY, halfZ),
                new Vector3(halfX, -halfY, halfZ),
                new Vector3(halfX, halfY, halfZ),
                new Vector3(-halfX, halfY, halfZ),
            };

            int[] triangles = new int[]
            {
                0, 2, 1, 0, 3, 2,
                4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4,
                3, 6, 2, 3, 7, 6,
                0, 7, 3, 0, 4, 7,
                1, 2, 6, 1, 6, 5
            };

            Vector2[] uvs = new Vector2[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                uvs[i] = new Vector2(vertices[i].x / size.x + 0.5f, vertices[i].z / size.y + 0.5f);
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uvs;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        private void PopulateGameManager()
        {
            _gameManager.trackCircuits = new List<TrackCircuit>(_createdTracks);
            _gameManager.switchPoints = new List<SwitchPoint>(_createdSwitches);
            _gameManager.signals = new List<Signal>(_createdSignals);
            _gameManager.trains = new List<Train>(_createdTrains);
            _gameManager.routeDefinitions = new List<RouteData>(_createdRoutes);
        }

        private void ClearAll()
        {
            foreach (var tc in _createdTracks)
                if (tc != null) Destroy(tc.gameObject);
            foreach (var sw in _createdSwitches)
                if (sw != null) Destroy(sw.gameObject);
            foreach (var sig in _createdSignals)
                if (sig != null) Destroy(sig.gameObject);
            foreach (var train in _createdTrains)
                if (train != null) Destroy(train.gameObject);

            _createdTracks.Clear();
            _createdSwitches.Clear();
            _createdSignals.Clear();
            _createdTrains.Clear();
            _createdRoutes.Clear();
        }

        public List<RouteData> GetAllRoutes() => _createdRoutes;
    }
}
