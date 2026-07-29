Shader "Custom/WaterSurface" {
    Properties {
        _BaseColor ("Base Color", Color) = (0,0.6,1)
        _MainTex ("Texture", 2D) = "black" {}
        _TilingScale ("Tiling Scale", float) = 0.07

        [Header(Main Wave)]
        _WaveTiling ("Tiling", float) = 1
        _WaveColor ("Color", Color) = (1,1,1,0.7)
        _WaveSpeed ("Scroll Speed", float) = 1
        _WaveAngle ("Scroll Angle", Range(0, 360)) = 0
        
        [Header(Sub Wave)]
        _WaveTilingSub ("Tiling Scale", float) = 1.5
        _WaveColorSub ("Color", Color) = (1,1,1,0.5)
        _WaveSpeedSub ("Scroll Speed", float) = 0.7
        _WaveAngleSub ("Scroll Angle", Range(0, 360)) = 120

        _EdgeColor("EdgeColor", Color) = (1, 1, 1, 1)
        _DepthFactor("Depth Factor", Range(0.01, 10)) = 1
    }
    SubShader {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
                float4 screenPos : TEXCOORD2;
            };

            float4 _BaseColor;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _TilingScale;

            float _WaveTiling;
            float4 _WaveColor;
            float _WaveSpeed;
            float _WaveAngle;

            float _WaveTilingSub;
            float4 _WaveColorSub;
            float _WaveSpeedSub;
            float _WaveAngleSub;

            uniform sampler2D _CameraDepthTexture;
            fixed4 _EdgeColor;
            float _DepthFactor;

            float2 GetWorldUV(float4 vertexWS, float4 tex_ST) {
                float2 tiling = tex_ST.xy;
                float2 offset = tex_ST.zw;
                return vertexWS.xz * tiling + offset;
            }

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                float4 vertexWS = mul(unity_ObjectToWorld, v.vertex);
                // ワールド座標からUV座標を計算
                o.uv = GetWorldUV(vertexWS, _MainTex_ST);
                UNITY_TRANSFER_FOG(o,o.vertex);
                // スクリーン座標を計算
                o.screenPos = ComputeScreenPos(o.vertex);
                return o;
            }

            float2 CalcVector(float angle, float length) {
                float rad = radians(angle);
                return float2(cos(rad), sin(rad)) * length;
            }

            fixed4 CalcWaveColor(fixed4 base, float2 uv, float tiling, float4 color, float speed, float angle) {
                // UVスクロールのオフセットベクトルを計算
                float2 scroll = CalcVector(angle, _Time.x * speed);
                tiling *= _TilingScale;
                fixed4 tex = tex2D(_MainTex, uv * tiling + scroll);
                color = lerp(_BaseColor, color, color.a);
                return lerp(base, color, tex.a);
            }

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = _BaseColor;

                // 波テクスチャの適用とスクロール
                col = CalcWaveColor(col, i.uv, _WaveTilingSub, _WaveColorSub, _WaveSpeedSub, _WaveAngleSub);
                col = CalcWaveColor(col, i.uv, _WaveTiling, _WaveColor, _WaveSpeed, _WaveAngle);

                //深度の計算
                float4 depthSample = SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.screenPos));
                half depth = LinearEyeDepth(depthSample);
                half screenDepth = depth - i.screenPos.w;
                float edgeLine = 1 - saturate(_DepthFactor * screenDepth);
                col = lerp(col, _EdgeColor, edgeLine * _EdgeColor.a);

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
