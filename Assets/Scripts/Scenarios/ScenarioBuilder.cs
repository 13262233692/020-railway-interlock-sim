using System.Collections.Generic;
using UnityEngine;
using RailwayInterlock.Core;
using RailwayInterlock.Data;
using RailwayInterlock.Components;
using RailwayInterlock.Management;

namespace RailwayInterlock.Scenarios
{
    public class ScenarioBuilder : MonoBehaviour
    {
        public static ScenarioBuilder Instance { get; private set; }

        public GameManager gameManager;
        public GameObject trackPrefab;
        public GameObject signalPrefab;
        public GameObject switchPrefab;
        public GameObject trainPrefab;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public TrackCircuit CreateTrackCircuit(
            string id,
            string name,
            Vector3 position,
            Vector3 rotation,
            Vector3 size,
            List<string> adjacentTracks = null)
        {
            GameObject go = Instantiate(trackPrefab, position, Quaternion.Euler(rotation), transform);
            go.name = $"Track_{id}";

            TrackCircuit tc = go.GetComponent<TrackCircuit>();
            if (tc == null)
                tc = go.AddComponent<TrackCircuit>();

            tc.trackId = id;
            tc.displayName = name;
            tc.adjacentTrackIds = adjacentTracks ?? new List<string>();
            tc.length = size.z;

            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null)
                col = go.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.center = new Vector3(0, 0.5f, 0);
            col.size = new Vector3(size.x, 1f, size.z);

            if (tc.trackRenderer == null)
            {
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr == null)
                {
                    MeshFilter mf = go.AddComponent<MeshFilter>();
                    mf.mesh = CreateBoxMesh(size);
                    mr = go.AddComponent<MeshRenderer>();
                    mr.material = new Material(Shader.Find("Standard"));
                }
                tc.trackRenderer = mr;
            }

            return tc;
        }

        public Signal CreateSignal(
            string id,
            string name,
            Vector3 position,
            Vector3 rotation,
            Direction direction,
            string protectingTrackId)
        {
            GameObject go = Instantiate(signalPrefab, position, Quaternion.Euler(rotation), transform);
            go.name = $"Signal_{id}";

            Signal sig = go.GetComponent<Signal>();
            if (sig == null)
                sig = go.AddComponent<Signal>();

            sig.signalId = id;
            sig.displayName = name;
            sig.direction = direction;
            sig.protectingTrackId = protectingTrackId;

            CreateSignalLights(sig);

            return sig;
        }

        private void CreateSignalLights(Signal sig)
        {
            Transform lightPanel = new GameObject("LightPanel").transform;
            lightPanel.SetParent(sig.transform);
            lightPanel.localPosition = new Vector3(0, 2.5f, 0);

            GameObject red = CreateLight("RedLight", lightPanel, new Vector3(0, 0.6f, 0), new Color(0.5f, 0.1f, 0.1f));
            GameObject yellow = CreateLight("YellowLight", lightPanel, new Vector3(0, 0f, 0), new Color(0.5f, 0.4f, 0.1f));
            GameObject green = CreateLight("GreenLight", lightPanel, new Vector3(0, -0.6f, 0), new Color(0.1f, 0.5f, 0.2f));

            sig.redLight = red.GetComponent<MeshRenderer>();
            sig.yellowLight = yellow.GetComponent<MeshRenderer>();
            sig.greenLight = green.GetComponent<MeshRenderer>();

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            post.transform.SetParent(sig.transform);
            post.transform.localPosition = new Vector3(0, 1f, 0);
            post.transform.localScale = new Vector3(0.15f, 1f, 0.15f);
            Destroy(post.GetComponent<CapsuleCollider>());
        }

        private GameObject CreateLight(string name, Transform parent, Vector3 localPos, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = Vector3.one * 0.3f;
            Destroy(go.GetComponent<SphereCollider>());

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = color;
            mat.SetFloat("_Glossiness", 0.5f);
            mr.material = mat;

            return go;
        }

        public SwitchPoint CreateSwitchPoint(
            string id,
            string name,
            Vector3 position,
            Vector3 rotation,
            SwitchType type,
            string commonTrackId,
            string normalTrackId,
            string reverseTrackId)
        {
            GameObject go = Instantiate(switchPrefab, position, Quaternion.Euler(rotation), transform);
            go.name = $"Switch_{id}";

            SwitchPoint sw = go.GetComponent<SwitchPoint>();
            if (sw == null)
                sw = go.AddComponent<SwitchPoint>();

            sw.switchId = id;
            sw.displayName = name;
            sw.switchType = type;
            sw.commonTrackId = commonTrackId;
            sw.normalTrackId = normalTrackId;
            sw.reverseTrackId = reverseTrackId;

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "SwitchMarker";
            marker.transform.SetParent(go.transform);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(1f, 0.2f, 1f);
            Destroy(marker.GetComponent<BoxCollider>());
            sw.movingRail = marker.transform;

            sw.normalPositionOffset = new Vector3(0, 0, 0);
            sw.reversePositionOffset = new Vector3(2f, 0, 0);

            return sw;
        }

        public Train CreateTrain(
            string id,
            string name,
            Vector3 position,
            Vector3 rotation,
            Direction direction,
            float maxSpeedKmh = 60f)
        {
            GameObject go = Instantiate(trainPrefab, position, Quaternion.Euler(rotation), transform);
            go.name = $"Train_{id}";

            Train train = go.GetComponent<Train>();
            if (train == null)
                train = go.AddComponent<Train>();

            train.trainId = id;
            train.displayName = name;
            train.travelDirection = direction;
            train.maxSpeedKmh = maxSpeedKmh;

            Rigidbody rb = go.GetComponent<Rigidbody>();
            if (rb == null)
                rb = go.AddComponent<Rigidbody>();
            rb.mass = 50000f;
            rb.drag = 0.1f;
            rb.angularDrag = 1f;

            BoxCollider col = go.GetComponent<BoxCollider>();
            if (col == null)
                col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.5f, 0);
            col.size = new Vector3(2.5f, 3f, 15f);
            col.isTrigger = false;

            CreateTrainVisual(go.transform);

            return train;
        }

        private void CreateTrainVisual(Transform parent)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "TrainBody";
            body.transform.SetParent(parent);
            body.transform.localPosition = new Vector3(0, 1.5f, 0);
            body.transform.localScale = new Vector3(2.5f, 3f, 15f);
            Destroy(body.GetComponent<BoxCollider>());

            MeshRenderer bodyMr = body.GetComponent<MeshRenderer>();
            Material bodyMat = new Material(Shader.Find("Standard"));
            bodyMat.color = new Color(0.8f, 0.1f, 0.1f);
            bodyMr.material = bodyMat;

            GameObject cabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cabin.name = "Cabin";
            cabin.transform.SetParent(parent);
            cabin.transform.localPosition = new Vector3(0, 3.5f, 5f);
            cabin.transform.localScale = new Vector3(2.3f, 1.5f, 3f);
            Destroy(cabin.GetComponent<BoxCollider>());

            MeshRenderer cabinMr = cabin.GetComponent<MeshRenderer>();
            Material cabinMat = new Material(Shader.Find("Standard"));
            cabinMat.color = new Color(0.2f, 0.2f, 0.4f);
            cabinMr.material = cabinMat;

            for (int i = 0; i < 4; i++)
            {
                GameObject wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                wheel.name = $"Wheel_{i}";
                wheel.transform.SetParent(parent);
                wheel.transform.localPosition = new Vector3(
                    i % 2 == 0 ? -1.2f : 1.2f,
                    0.5f,
                    i < 2 ? -5f : 5f);
                wheel.transform.localRotation = Quaternion.Euler(0, 0, 90);
                wheel.transform.localScale = new Vector3(0.8f, 0.3f, 0.8f);
                Destroy(wheel.GetComponent<CapsuleCollider>());
            }
        }

        private Mesh CreateBoxMesh(Vector3 size)
        {
            Mesh mesh = new Mesh();
            mesh.name = "TrackMesh";

            float halfX = size.x * 0.5f;
            float halfY = 0.1f;
            float halfZ = size.z * 0.5f;

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

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }
    }
}
