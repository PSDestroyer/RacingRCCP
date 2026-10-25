Shader "WindshieldRainDemo/BuiltIn/BillboardShader"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Size("Billboard Size", Float) = 1.0
    }
        SubShader
        {
            Tags { "Queue" = "Transparent" "RenderType" = "Transparent" }
            LOD 100

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            Pass
            {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma multi_compile_fog
                #include "UnityCG.cginc"

                sampler2D _MainTex;
                float4 _MainTex_ST;
                float4 _Color;
                float _Size;

                struct appdata
                {
                    float3 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2f
                {
                    float2 uv : TEXCOORD0;
                    float4 pos : SV_POSITION;
                    UNITY_FOG_COORDS(1)
                };

                v2f vert(appdata v)
                {
                    v2f o;

                    // World-space center of this object
                    float3 worldCenter = mul(unity_ObjectToWorld, float4(0,0,0,1)).xyz;

                    // Extract object scale from the matrix
                    float3 scale;
                    scale.x = length(unity_ObjectToWorld._m00_m10_m20);
                    scale.y = length(unity_ObjectToWorld._m01_m11_m21);
                    scale.z = length(unity_ObjectToWorld._m02_m12_m22);

                    // Billboard facing — upright (rotates only around Y)
                    float3 toCam = _WorldSpaceCameraPos - worldCenter;
                    toCam.y = 0;
                    toCam = normalize(toCam);

                    float3 right = float3(toCam.z, 0, -toCam.x);
                    float3 up = float3(0, 1, 0);

                    // Apply object scale and user-defined size
                    float sx = _Size * scale.x;
                    float sy = _Size * scale.y;

                    // Local vertex.x/y should be -0.5..0.5 for a unit quad
                    float3 worldPos = worldCenter +
                                      (right * v.vertex.x * sx) +
                                      (up * v.vertex.y * sy);

                    o.pos = UnityWorldToClipPos(worldPos);
                    o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                    UNITY_TRANSFER_FOG(o,o.pos);
                    return o;
                }

                fixed4 frag(v2f i) : SV_Target
                {
                    fixed4 col = tex2D(_MainTex, i.uv) * _Color;
                    UNITY_APPLY_FOG(i.fogCoord, col);
                    return col;
                }
                ENDCG
            }
        }

            FallBack Off
}