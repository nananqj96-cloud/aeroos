using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace OpenRoad.Spike
{
    /// <summary>
    /// Phase 0 functional render-target mirrors. Each view reflects the eye pose across its plane,
    /// renders to 256x128, skips post/shadows, and updates every other primary frame.
    /// </summary>
    public sealed class MirrorCameraRig : MonoBehaviour
    {
        private const int MirrorWidth = 256;
        private const int MirrorHeight = 128;
        private const int DefaultUpdateInterval = 2;

        public Transform bikeRoot;
        public Camera eyeCamera;
        public bool mirrorsEnabledAtStart = true;

        private MirrorSlot[] _slots;
        private bool _mirrorsEnabled;

        public bool MirrorsEnabled => _mirrorsEnabled;
        public bool IsReady => _slots != null && _slots.Length == 2 && _slots[0].camera != null && _slots[1].camera != null;
        public int ActiveRenderTargetCount
        {
            get
            {
                if (_slots == null) return 0;
                int count = 0;
                for (int i = 0; i < _slots.Length; i++)
                {
                    if (_slots[i].target != null && _slots[i].target.IsCreated()) count++;
                }
                return count;
            }
        }

        private sealed class MirrorSlot
        {
            public Transform surface;
            public Renderer surfaceRenderer;
            public Camera camera;
            public RenderTexture target;
            public Material material;
        }

        private void Awake()
        {
            if (eyeCamera == null) eyeCamera = Camera.main;
            _mirrorsEnabled = mirrorsEnabledAtStart;
            _slots = new MirrorSlot[2];
            _slots[0] = CreateSlot(0);
            _slots[1] = CreateSlot(1);
            ApplyEnabledState(createTargets: _mirrorsEnabled);
        }

        private MirrorSlot CreateSlot(int index)
        {
            var slot = new MirrorSlot();
            string side = index == 0 ? "Left" : "Right";

            GameObject surfaceObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
            surfaceObject.name = $"Phase 0 {side} mirror surface";
            if (bikeRoot != null)
            {
                surfaceObject.transform.SetParent(bikeRoot, false);
                surfaceObject.transform.localPosition = new Vector3(index == 0 ? -0.52f : 0.52f, 1.55f, 0.88f);
                surfaceObject.transform.localRotation = Quaternion.Euler(0f, index == 0 ? -7f : 7f, 0f);
                surfaceObject.transform.localScale = new Vector3(0.34f, 0.17f, 1f);
            }

            Collider collider = surfaceObject.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            slot.surface = surfaceObject.transform;
            slot.surfaceRenderer = surfaceObject.GetComponent<Renderer>();

            GameObject cameraObject = new GameObject($"Phase 0 {side} mirror camera");
            if (bikeRoot != null) cameraObject.transform.SetParent(bikeRoot, false);
            slot.camera = cameraObject.AddComponent<Camera>();
            slot.camera.enabled = false; // rendered manually at a bounded update rate
            slot.camera.fieldOfView = 78f;
            slot.camera.nearClipPlane = 0.1f;
            slot.camera.farClipPlane = 180f;
            slot.camera.allowHDR = false;
            slot.camera.allowMSAA = false;
            slot.camera.useOcclusionCulling = false;
            slot.camera.cullingMask = ~0;
            slot.camera.clearFlags = CameraClearFlags.Skybox;

            UniversalAdditionalCameraData additionalCameraData = cameraObject.AddComponent<UniversalAdditionalCameraData>();
            additionalCameraData.renderPostProcessing = false;
            additionalCameraData.renderShadows = false;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Texture");
            if (shader != null)
            {
                slot.material = new Material(shader) { name = $"OPEN ROAD / {side} mirror material" };
                if (slot.surfaceRenderer != null) slot.surfaceRenderer.sharedMaterial = slot.material;
            }

            if (slot.surfaceRenderer != null) slot.surfaceRenderer.enabled = _mirrorsEnabled;
            return slot;
        }

        public void SetMirrorsEnabled(bool enabled)
        {
            if (_mirrorsEnabled == enabled)
            {
                return;
            }

            _mirrorsEnabled = enabled;
            ApplyEnabledState(createTargets: enabled);
        }

        private void ApplyEnabledState(bool createTargets)
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                MirrorSlot slot = _slots[i];
                if (slot.surfaceRenderer != null) slot.surfaceRenderer.enabled = _mirrorsEnabled;
                if (!createTargets)
                {
                    ReleaseTarget(slot);
                    continue;
                }

                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    EnsureTarget(slot, i == 0 ? "Left" : "Right");
                }
            }
        }

        private static void EnsureTarget(MirrorSlot slot, string side)
        {
            if (slot.target != null && slot.target.IsCreated()) return;

            slot.target = new RenderTexture(MirrorWidth, MirrorHeight, 16, RenderTextureFormat.ARGB32)
            {
                name = $"OPEN ROAD / {side} mirror 256x128",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };
            slot.target.Create();
            if (slot.camera != null) slot.camera.targetTexture = slot.target;
            if (slot.material != null)
            {
                slot.material.mainTexture = slot.target;
                if (slot.material.HasProperty("_BaseMap")) slot.material.SetTexture("_BaseMap", slot.target);
            }
        }

        private static void ReleaseTarget(MirrorSlot slot)
        {
            if (slot.camera != null) slot.camera.targetTexture = null;
            if (slot.material != null)
            {
                slot.material.mainTexture = null;
                if (slot.material.HasProperty("_BaseMap")) slot.material.SetTexture("_BaseMap", null);
            }
            if (slot.target != null)
            {
                slot.target.Release();
                Destroy(slot.target);
                slot.target = null;
            }
        }

        private void LateUpdate()
        {
            if (!_mirrorsEnabled || !IsReady || eyeCamera == null || bikeRoot == null ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || Time.frameCount % DefaultUpdateInterval != 0)
            {
                return;
            }

            for (int i = 0; i < _slots.Length; i++)
            {
                MirrorSlot slot = _slots[i];
                if (slot.target == null || !slot.target.IsCreated() || slot.surface == null || slot.camera == null)
                {
                    continue;
                }

                // Mirror across the surface plane while preserving a non-reflected camera basis.
                // Using a reflected right vector with this forward/up pair produces a proper rotation;
                // camera.Render plus invertCulling then gives the view the expected mirrored winding.
                Vector3 normal = slot.surface.forward;
                Vector3 reflectedPosition = slot.surface.position + Vector3.Reflect(eyeCamera.transform.position - slot.surface.position, normal);
                Vector3 reflectedForward = Vector3.Reflect(eyeCamera.transform.forward, normal);
                Vector3 reflectedUp = Vector3.Reflect(eyeCamera.transform.up, normal);
                Vector3 reflectedRight = -Vector3.Reflect(eyeCamera.transform.right, normal);
                if (reflectedForward.sqrMagnitude < 0.001f || reflectedUp.sqrMagnitude < 0.001f)
                {
                    continue;
                }

                Quaternion reflectedRotation = Quaternion.LookRotation(reflectedForward.normalized, reflectedUp.normalized);
                if (Vector3.Dot(reflectedRotation * Vector3.right, reflectedRight) < 0f)
                {
                    reflectedRotation = Quaternion.LookRotation(reflectedForward.normalized, -reflectedUp.normalized);
                }
                slot.camera.transform.SetPositionAndRotation(reflectedPosition, reflectedRotation);

                bool oldInvertCulling = GL.invertCulling;
                GL.invertCulling = !oldInvertCulling;
                try
                {
                    slot.camera.Render();
                }
                finally
                {
                    GL.invertCulling = oldInvertCulling;
                }
            }
        }

        private void OnDestroy()
        {
            if (_slots == null) return;
            for (int i = 0; i < _slots.Length; i++)
            {
                ReleaseTarget(_slots[i]);
                if (_slots[i].material != null) Destroy(_slots[i].material);
            }
        }
    }
}
