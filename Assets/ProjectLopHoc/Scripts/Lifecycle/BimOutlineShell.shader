Shader "UTT/BIM XRay Outline Adapter"
{
 Properties{_BaseColor("Outline",Color)=(0.05,0.8,1,1) _Width("Pixels",Float)=3}
 SubShader{Tags{"RenderPipeline"="UniversalPipeline" "Queue"="Transparent+101"}
 Pass{ZWrite Off ZTest Always Cull Off Blend SrcAlpha OneMinusSrcAlpha Stencil{Ref 128 Comp NotEqual Pass Keep}
 HLSLPROGRAM
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 CBUFFER_START(UnityPerMaterial)
 half4 _BaseColor;float _Width;
 CBUFFER_END
 struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct Varyings{float4 positionCS:SV_POSITION;UNITY_VERTEX_OUTPUT_STEREO};
 Varyings Vert(Attributes input){Varyings output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);float4 clip=TransformObjectToHClip(input.positionOS.xyz);float3 viewN=mul((float3x3)UNITY_MATRIX_V,TransformObjectToWorldNormal(input.normalOS));float2 dir=viewN.xy/max(length(viewN.xy),.00001);clip.xy+=dir*_Width*2/_ScreenParams.xy*clip.w;output.positionCS=clip;return output;}
 half4 Frag(Varyings input):SV_Target{UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);return _BaseColor;}
 ENDHLSL
 }}
}
