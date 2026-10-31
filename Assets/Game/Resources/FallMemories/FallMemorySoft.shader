Shader "BorrowTomorrow/FallMemorySoft"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Blur ("Blur pixels", Float) = 0
        _Dim ("Brightness", Float) = 1
        _Zoom ("Zoom", Float) = 1
        _Drop ("Fall drift", Float) = 0
        _Grain ("Grain", Float) = 0
        _Vignette ("Vignette", Float) = 0
        _Ghost ("Ghost", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Overlay" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Blur, _Dim, _Zoom, _Drop, _Grain, _Vignette, _Ghost;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            fixed4 frag(v2f i):SV_Target
            {
                float2 uv=(i.uv-.5)/max(1,_Zoom)+.5+float2(0,_Drop);
                float2 d=_MainTex_TexelSize.xy*_Blur;
                fixed4 c=tex2D(_MainTex,uv)*.20;
                c+=(tex2D(_MainTex,uv+d*float2(1,0))+tex2D(_MainTex,uv-d*float2(1,0))
                    +tex2D(_MainTex,uv+d*float2(0,1))+tex2D(_MainTex,uv-d*float2(0,1)))*.12;
                c+=(tex2D(_MainTex,uv+d)+tex2D(_MainTex,uv-d)
                    +tex2D(_MainTex,uv+d*float2(1,-1))+tex2D(_MainTex,uv+d*float2(-1,1)))*.08;
                c.rgb=lerp(c.rgb,tex2D(_MainTex,uv+float2(0,.0045)).rgb,_Ghost);
                float edge=smoothstep(.18,.72,length(i.uv-.5));
                float noise=frac(sin(dot(floor(i.uv*_ScreenParams.xy),float2(12.9898,78.233)))*43758.5453)-.5;
                c.rgb=c.rgb*_Dim*(1-edge*_Vignette)+noise*_Grain;
                return c*i.color;
            }
            ENDCG
        }
    }
}
