// Vertical two-color gradient skybox (unlit). The gradient is mixed by
// SCREEN-space Y (NDC): the top of the screen shows _TopColor, the bottom
// shows _BottomColor. Direction-based mixing would collapse to a nearly
// solid fill under the game's orthographic camera (parallel view rays see
// a narrow band of sky directions), so the gradient is screen-anchored -
// it stays a clean vertical wash for any camera.
Shader "AmazingTrack/GradientSkybox"
{
    Properties
    {
        _TopColor ("Top Color", Color) = (0.32, 0.42, 0.62, 1)
        _BottomColor ("Bottom Color", Color) = (0.92, 0.89, 0.82, 1)
        _GradientOffset ("Gradient Offset", Float) = 0.0
        _GradientSmoothness ("Gradient Smoothness", Float) = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor;
                half4 _BottomColor;
                float _GradientOffset;
                float _GradientSmoothness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 screenUV : TEXCOORD0; // xy: 0..1 across the screen, w: clip w
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // NDC 0..1 across the screen, y-flip aware - same math as
                // GetVertexPositionInputs().positionNDC. Perspective-correct
                // for perspective cameras, exact for the orthographic one.
                float4 ndc = output.positionCS * 0.5f;
                output.screenUV = float4(ndc.x, ndc.y * _ProjectionParams.x, 0, ndc.w)
                                  + float4(ndc.w, ndc.w, 0, 0);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.screenUV.xy / max(input.screenUV.w, 1e-5f);
                float t = saturate(uv.y * _GradientSmoothness + _GradientOffset);
                return half4(lerp(_BottomColor.rgb, _TopColor.rgb, t), 1.0);
            }
            ENDHLSL
        }
    }
}
