Shader "Hidden/OpenRoad/HorizontalFisheye"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "OpenRoadHorizontalFisheye"
            ZTest Always
            ZWrite Off
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _OpenRoadStrength;
            float _OpenRoadSourceHalfTan;
            float _OpenRoadTargetHalfAngle;
            float _OpenRoadVerticalScale;

            half4 Frag(Varyings input) : SV_Target
            {
                float2 outputUv = input.texcoord;
                float sourceHalfTan = max(_OpenRoadSourceHalfTan, 0.0001);

                // Linear X is the rectilinear source. The warped X maps screen position to
                // horizontal angle, then back to rectilinear source UV. This curves horizontal
                // road edges without changing the U coordinate of a vertical line.
                float centeredX = (outputUv.x - 0.5) * 2.0;
                float warpedX = tan(centeredX * _OpenRoadTargetHalfAngle) / sourceHalfTan;
                float mappedX = lerp(centeredX, warpedX, saturate(_OpenRoadStrength));
                float sourceX = 0.5 + 0.5 * mappedX;

                // Keep V near-rectilinear; optional verticalScale is a comfort/framing control.
                float sourceY = 0.5 + (outputUv.y - 0.5) * _OpenRoadVerticalScale;
                float2 sourceUv = clamp(float2(sourceX, sourceY), 0.001, 0.999);
                return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, sourceUv);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
