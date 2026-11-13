Shader "UTT/Environment Cluster Fade"
{
 Properties { _FadeStart("Fade start",Float)=220 _FadeEnd("Fade end",Float)=320 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="AlphaTest" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   ZWrite On
   Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float _FadeStart; float _FadeEnd;
   CBUFFER_END
   struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL;half4 color:COLOR;UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 positionCS:SV_POSITION;half3 color:TEXCOORD0;float3 positionWS:TEXCOORD1;UNITY_VERTEX_OUTPUT_STEREO };
   V Vert(A input)
   {
    V output;UNITY_SETUP_INSTANCE_ID(input);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionWS=TransformObjectToWorld(input.positionOS.xyz);output.positionCS=TransformWorldToHClip(output.positionWS);
    float3 n=TransformObjectToWorldNormal(input.normalOS);Light light=GetMainLight();
    half3 illumination=max(SampleSH(n),half3(.22,.22,.22))+light.color*saturate(dot(n,light.direction))*.35;
    output.color=input.color.rgb*min(illumination,half3(1,1,1));return output;
   }
   half4 Frag(V input):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
    float fade=saturate((_FadeEnd-distance(input.positionWS,GetCameraPositionWS()))/max(1,_FadeEnd-_FadeStart));
    float noise=frac(52.9829189*frac(dot(floor(input.positionCS.xy),float2(.06711056,.00583715))));
    clip(fade-noise-.0001);return half4(input.color,1);
   }
   ENDHLSL
  }
 }
}
