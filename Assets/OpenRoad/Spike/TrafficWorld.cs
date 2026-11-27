using System;
using UnityEngine;

namespace OpenRoad.Spike
{
    /// <summary>
    /// Deterministic, allocation-free-after-start traffic fixture. This is kinematic test traffic,
    /// not the future IDM/lane-graph traffic system.
    /// </summary>
    public sealed class TrafficWorld : MonoBehaviour
    {
        public const int LowPresetVehicleCap = 50;
        private const float RouteLengthMetres = 1000f;

        [Min(0)] public int requestedVehicleCount = LowPresetVehicleCap;
        public int seed = 20261127;
        public Material trafficMaterial;

        private TrafficState[] _vehicles;
        private Mesh _sharedVehicleMesh;
        private Material _ownedMaterial;
        private int _activeLimit;

        public int ActiveVehicleCount { get; private set; }
        public int AllocatedVehicleCount => _vehicles != null ? _vehicles.Length : 0;
        public bool IsInitialized => _vehicles != null;

        private struct TrafficState
        {
            public Transform transform;
            public float initialDistance;
            public float distance;
            public float laneX;
            public float speedMetresPerSecond;
            public bool active;
        }

        private void Start()
        {
            InitializeTraffic();
        }

        private void InitializeTraffic()
        {
            if (_vehicles != null)
            {
                return;
            }

            int count = Mathf.Clamp(requestedVehicleCount, 0, LowPresetVehicleCap);
            requestedVehicleCount = count;
            _vehicles = new TrafficState[count];
            _sharedVehicleMesh = CreateVehicleMesh();
            Material material = ResolveMaterial();
            System.Random random = new System.Random(seed);
            float spacing = count > 0 ? RouteLengthMetres / count : 0f;

            for (int i = 0; i < count; i++)
            {
                var car = new GameObject($"Traffic fixture {i + 1:00}");
                car.transform.SetParent(transform, false);
                car.transform.localScale = Vector3.one;

                var filter = car.AddComponent<MeshFilter>();
                filter.sharedMesh = _sharedVehicleMesh;
                var renderer = car.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                float jitter = (float)random.NextDouble() * 4f;
                float initialDistance = WrapDistance(i * spacing + jitter, RouteLengthMetres);
                float laneX = (i & 1) == 0 ? -2.45f : 2.45f;
                float speed = 14f + (float)random.NextDouble() * 9f;
                _vehicles[i] = new TrafficState
                {
                    transform = car.transform,
                    initialDistance = initialDistance,
                    distance = initialDistance,
                    laneX = laneX,
                    speedMetresPerSecond = speed,
                    active = true
                };
                SetVehiclePosition(i);
            }

            SetActiveVehicleLimit(count);
        }

        private Material ResolveMaterial()
        {
            if (trafficMaterial != null)
            {
                return trafficMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null)
            {
                return null;
            }

            _ownedMaterial = new Material(shader)
            {
                name = "OPEN ROAD / generated traffic fallback",
                color = new Color(0.26f, 0.34f, 0.39f, 1f)
            };
            return _ownedMaterial;
        }

        private static Mesh CreateVehicleMesh()
        {
            GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Mesh cube = primitive.GetComponent<MeshFilter>().sharedMesh;
            var pieces = new CombineInstance[3];
            pieces[0] = new CombineInstance
            {
                mesh = cube,
                transform = Matrix4x4.TRS(new Vector3(0f, 0.40f, 0f), Quaternion.identity, new Vector3(1.85f, 0.72f, 3.8f))
            };
            pieces[1] = new CombineInstance
            {
                mesh = cube,
                transform = Matrix4x4.TRS(new Vector3(0f, 0.88f, -0.22f), Quaternion.identity, new Vector3(1.38f, 0.62f, 1.75f))
            };
            pieces[2] = new CombineInstance
            {
                mesh = cube,
                transform = Matrix4x4.TRS(new Vector3(0f, 0.76f, 1.2f), Quaternion.identity, new Vector3(1.6f, 0.28f, 0.68f))
            };

            var mesh = new Mesh { name = "OPEN ROAD / pooled low-poly traffic fixture" };
            mesh.CombineMeshes(pieces, true, true, false);
            mesh.RecalculateBounds();
            Destroy(primitive);
            return mesh;
        }

        private void Update()
        {
            if (_vehicles == null || _activeLimit == 0)
            {
                return;
            }

            float delta = Time.deltaTime;
            for (int i = 0; i < _activeLimit; i++)
            {
                TrafficState state = _vehicles[i];
                state.distance = WrapDistance(state.distance + state.speedMetresPerSecond * delta, RouteLengthMetres);
                _vehicles[i] = state;
                SetVehiclePosition(i);
            }
        }

        private void SetVehiclePosition(int index)
        {
            TrafficState state = _vehicles[index];
            if (state.transform == null)
            {
                return;
            }

            state.transform.localPosition = new Vector3(state.laneX, 0.035f, state.distance - RouteLengthMetres * 0.5f);
            state.transform.localRotation = Quaternion.identity;
        }

        /// <summary>Restores the exact seeded positions before a measured benchmark begins.</summary>
        public void ResetTraffic()
        {
            if (_vehicles == null)
            {
                return;
            }

            for (int i = 0; i < _vehicles.Length; i++)
            {
                TrafficState state = _vehicles[i];
                state.distance = state.initialDistance;
                _vehicles[i] = state;
                SetVehiclePosition(i);
            }
        }

        /// <summary>
        /// Watchdog hook used if a supported profiler counter shows Low's draw-call ceiling is
        /// exceeded. Vehicles remain pooled; excess renderers are simply hidden from that frame on.
        /// </summary>
        public void SetActiveVehicleLimit(int requestedLimit)
        {
            if (_vehicles == null)
            {
                return;
            }

            _activeLimit = Mathf.Clamp(requestedLimit, 0, _vehicles.Length);
            ActiveVehicleCount = _activeLimit;
            for (int i = 0; i < _vehicles.Length; i++)
            {
                TrafficState state = _vehicles[i];
                bool shouldBeActive = i < _activeLimit;
                if (state.active != shouldBeActive)
                {
                    state.active = shouldBeActive;
                    _vehicles[i] = state;
                    if (state.transform != null)
                    {
                        state.transform.gameObject.SetActive(shouldBeActive);
                    }
                }
            }
        }

        private static float WrapDistance(float value, float length)
        {
            value %= length;
            return value < 0f ? value + length : value;
        }

        private void OnDestroy()
        {
            if (_sharedVehicleMesh != null) Destroy(_sharedVehicleMesh);
            if (_ownedMaterial != null) Destroy(_ownedMaterial);
        }
    }
}
