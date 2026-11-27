using OpenRoad.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace OpenRoad.Rendering
{
    /// <summary>
    /// Tier-A single-camera horizontal projection remap. It intentionally runs only on the marked
    /// player camera, after post-processing, so mirror cameras remain cheap and HUD stays straight.
    /// </summary>
    public sealed class HorizontalFisheyeRendererFeature : ScriptableRendererFeature
    {
        [SerializeField] public Shader fisheyeShader;
        private Material _material;
        private HorizontalFisheyePass _pass;

        public override void Create()
        {
            if (fisheyeShader == null)
            {
                fisheyeShader = Shader.Find("Hidden/OpenRoad/HorizontalFisheye");
            }

            CoreUtils.Destroy(_material);
            _material = fisheyeShader != null ? CoreUtils.CreateEngineMaterial(fisheyeShader) : null;
            _pass ??= new HorizontalFisheyePass();
            _pass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            if (_material == null || renderingData.cameraData.cameraType != CameraType.Game)
            {
                return;
            }

            Camera camera = renderingData.cameraData.camera;
            LensCameraMarker marker = camera != null ? camera.GetComponent<LensCameraMarker>() : null;
            if (marker == null || !marker.isActiveAndEnabled || marker.distortionStrength <= 0.001f)
            {
                return;
            }

            _pass.Setup(_material, marker.distortionStrength, marker.edgeCoverage, marker.verticalScale);
            renderer.EnqueuePass(_pass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
            _material = null;
            _pass = null;
        }

        private sealed class HorizontalFisheyePass : ScriptableRenderPass
        {
            private const string PassName = "OPEN ROAD / Horizontal Fisheye";
            private Material _material;
            private float _strength;
            private float _edgeCoverage;
            private float _verticalScale;

            public HorizontalFisheyePass()
            {
                // The remap samples camera color, so force URP to provide an intermediate texture.
                requiresIntermediateTexture = true;
            }

            public void Setup(Material material, float strength, float edgeCoverage, float verticalScale)
            {
                _material = material;
                _strength = Mathf.Clamp01(strength);
                _edgeCoverage = Mathf.Clamp(edgeCoverage, 0.65f, 1f);
                _verticalScale = Mathf.Clamp(verticalScale, 0.75f, 1.25f);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                if (_material == null)
                {
                    return;
                }

                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
                UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
                TextureHandle source = resourceData.activeColorTexture;

                // requiresIntermediateTexture should make this false. Keep the guard so a future
                // renderer setting cannot accidentally sample the swap-chain backbuffer.
                if (resourceData.isActiveTargetBackBuffer || !source.IsValid())
                {
                    return;
                }

                TextureDesc destinationDesc = renderGraph.GetTextureDesc(source);
                destinationDesc.name = "OpenRoad.HorizontalFisheye";
                destinationDesc.clearBuffer = false;
                destinationDesc.depthBufferBits = 0;
                TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

                Camera camera = cameraData.camera;
                float sourceHalfTangent = camera != null && !camera.orthographic
                    ? Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * Mathf.Max(0.1f, camera.aspect)
                    : 1f;
                float targetHalfAngle = Mathf.Atan(sourceHalfTangent * _edgeCoverage);

                _material.SetFloat("_OpenRoadStrength", _strength);
                _material.SetFloat("_OpenRoadSourceHalfTan", sourceHalfTangent);
                _material.SetFloat("_OpenRoadTargetHalfAngle", targetHalfAngle);
                _material.SetFloat("_OpenRoadVerticalScale", _verticalScale);

                RenderGraphUtils.BlitMaterialParameters parameters = new RenderGraphUtils.BlitMaterialParameters(
                    source,
                    destination,
                    _material,
                    0);
                renderGraph.AddBlitPass(parameters, passName: PassName);

                // Make the remapped target the camera's color result for URP's final resolve.
                resourceData.cameraColor = destination;
            }
        }
    }
}
