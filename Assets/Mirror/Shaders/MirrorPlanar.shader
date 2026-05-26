Shader "Mirror/Planar"
{
    Properties
    {
        [HideInInspector] _MirrorTex("Mirror Texture", 2DArray) = "white" {}
        _Tint("Tint", Color) = (1,1,1,1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma require 2darray

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_MirrorTex);
            SAMPLER(sampler_MirrorTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
            CBUFFER_END

            float4x4 _MirrorVP_Left;
            float4x4 _MirrorVP_Right;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 mirrorScreen : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;

                float4x4 mirrorVP = (unity_StereoEyeIndex == 0) ? _MirrorVP_Left : _MirrorVP_Right;
                float4 mirrorClip = mul(mirrorVP, float4(p.positionWS, 1.0));
                o.mirrorScreen = ComputeScreenPos(mirrorClip);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 uv = i.mirrorScreen.xy / max(i.mirrorScreen.w, 1e-5);
                half4 col = SAMPLE_TEXTURE2D_ARRAY(_MirrorTex, sampler_MirrorTex, uv, unity_StereoEyeIndex);
                return col * _Tint;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
