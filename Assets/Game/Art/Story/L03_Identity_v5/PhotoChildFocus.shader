Shader "BorrowTomorrow/UI/PhotoChildFocus" {
Properties { [PerRendererData] _MainTex("Texture",2D)="white"{} _Pulse("Pulse",Float)=.5 _TargetRect("Target Rect",Vector)=(.55,.13,.15,.46) }
SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"} Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode] Blend SrcAlpha OneMinusSrcAlpha
Pass { CGPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "UnityCG.cginc"
struct appdata {float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct v2f {float4 vertex:SV_POSITION;float2 uv:TEXCOORD0;};
sampler2D _MainTex;float4 _TargetRect;float _Pulse;
v2f vert(appdata v){v2f o;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o;}
fixed4 frag(v2f i):SV_Target {float2 d=(i.uv-(_TargetRect.xy+_TargetRect.zw*.5))/(_TargetRect.zw*.5);float mask=1-smoothstep(.45,1,length(d));fixed4 c=tex2D(_MainTex,i.uv);return fixed4(saturate(c.rgb+float3(.30,.22,.10)),mask*_Pulse*c.a);}
ENDCG } } }
