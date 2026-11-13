Shader "UTT/Train Exterior Angular Guard"
{
 Properties { _TrainForward("Train Forward",Vector)=(0,0,1,0) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Overlay+1000" "RenderType"="Transparent" }
  Pass
  {
   Cull Off ZTest Always ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile_instancing
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   CBUFFER_START(UnityPerMaterial)
   float4 _TrainForward;
   CBUFFER_END
   struct A { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
   struct V { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
   V Vert(A v) { V o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); o.positionWS=TransformObjectToWorld(v.positionOS.xyz); o.positionCS=TransformWorldToHClip(o.positionWS); return o; }
   half4 Frag(V v):SV_Target
   {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
    float3 direction=normalize(v.positionWS-GetCameraPositionWS());
    float horizontal=max(length(direction.xz),.0001);
    float cosineYaw=dot(direction.xz,normalize(_TrainForward.xz))/horizontal;
    float visibleYaw=smoothstep(.34202014,.5,cosineYaw); // solid black beyond70deg, clear within60deg
    float visiblePitch=smoothstep(-.9063078,-.819152,direction.y)*(1-smoothstep(.34202014,.5,direction.y)); // down65/up30
    return half4(0,0,0,1-visibleYaw*visiblePitch);
   }
   ENDHLSL
  }
 }
}
