Shader "UTT/Railway Vertex Colour Matte"
{
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
  Pass
  {
   Tags { "LightMode"="UniversalForward" }
   ZWrite On
   Cull Off
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct Varyings { float4 positionCS:SV_POSITION; half3 color:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   Varyings Vert(Attributes input)
   {
    Varyings output; UNITY_SETUP_INSTANCE_ID(input); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
    output.positionCS=TransformObjectToHClip(input.positionOS.xyz);
    float3 normalWS=TransformObjectToWorldNormal(input.normalOS);
    Light light=GetMainLight();
    half3 diffuse=max(SampleSH(normalWS),half3(0.22,0.22,0.22))+light.color*saturate(dot(normalWS,light.direction))*0.6;
    output.color=input.color.rgb*diffuse; return output;
   }
   half4 Frag(Varyings input):SV_Target { UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input); return half4(input.color,1); }
   ENDHLSL
  }
 }
}
