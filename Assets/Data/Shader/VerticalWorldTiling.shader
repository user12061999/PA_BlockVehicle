Shader "Custom/VerticalWorldTiling" {
    Properties {
        _BaseColor ("Base Color", Color) = (0,0.6,1)
        _MainTex ("Texture", 2D) = "black" {}
        _TilingScale ("Tiling Scale", float) = 0.07
    }
    SubShader {
        Tags {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        
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

            float2 GetWorldUV(float4 vertexWS, float4 tex_ST) {
                float2 tiling = tex_ST.xy;
                float2 offset = tex_ST.zw;
                return vertexWS.xy * tiling + offset;
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

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv * _TilingScale) * _BaseColor;
                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
