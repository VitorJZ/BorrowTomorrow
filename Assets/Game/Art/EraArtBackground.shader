Shader "BorrowTomorrow/EraArtBackground"
{
 Properties { [PerRendererData] _MainTex("Texture",2D)="white"{} _Boundary("Boundary",Float)=0 _Past("Past",Float)=1 }
 SubShader {
  Tags { "Queue"="Transparent" "RenderType"="Transparent" }
  Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "UnityCG.cginc"
   struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
   struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;float x:TEXCOORD1;};
   sampler2D _MainTex;float _Boundary;float _Past;
   v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color;o.x=mul(unity_ObjectToWorld,v.vertex).x;return o;}
   fixed4 frag(v2f i):SV_Target {
    clip((_Past>.5?_Boundary-i.x:i.x-_Boundary)+.0001);
    fixed4 c=tex2D(_MainTex,i.uv)*i.color;
    float grey=dot(c.rgb,float3(.299,.587,.114));c.rgb=lerp(grey.xxx,c.rgb,.68)*.72;return c;
   }
   ENDCG
  }
 }
}
