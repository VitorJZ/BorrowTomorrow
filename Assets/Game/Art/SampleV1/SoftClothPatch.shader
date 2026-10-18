Shader "BorrowTomorrow/SoftClothPatch" {
 Properties { [PerRendererData] _MainTex("Texture",2D)="white"{} _Region("Region",Vector)=(0,0,1,1) }
 SubShader {
 Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
 Cull Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
 Pass { CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;float4 color:COLOR;};
 sampler2D _MainTex; float4 _Region;
 v2f vert(appdata i){v2f o;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
 fixed4 frag(v2f i):SV_Target {fixed4 c=tex2D(_MainTex,i.uv)*i.color;float2 p=(i.uv-_Region.xy)/_Region.zw;float2 edge=min(p,1-p);c.a*=smoothstep(0,.18,min(edge.x,edge.y));return c;}
 ENDCG }
 }
}
