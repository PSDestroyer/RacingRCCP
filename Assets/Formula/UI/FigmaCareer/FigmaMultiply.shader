Shader "Formula/UI/Figma Multiply"
{
    Properties { [PerRendererData] _MainTex ("Texture", 2D) = "white" {} _Color ("Color", Color) = (1,1,1,1) }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend DstColor Zero
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; };
            sampler2D _MainTex; fixed4 _Color;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                #ifndef UNITY_COLORSPACE_GAMMA
                float3 factor=lerp(float3(1,1,1),LinearToGammaSpace(c.rgb),c.a);
                return fixed4(GammaToLinearSpaceExact(factor.r),GammaToLinearSpaceExact(factor.g),GammaToLinearSpaceExact(factor.b),1);
                #else
                return fixed4(lerp(float3(1,1,1),c.rgb,c.a),1);
                #endif
            }
            ENDCG
        }
    }
}
