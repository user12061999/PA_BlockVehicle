Shader "Pop/CartoonGlass"
{
    Properties
    {
        _MainTex ("テクスチャ", 2D) = "white" {}
        _Color ("カラー", Color) = (1,1,1,1)
        _RimLightColor ("リムライトカラー", Color) = (0.5,0.5,0.5)
        _RimLightBorderRangeMin ("リムライトの境界 開始位置", Range(0.0, 1.0)) = 0.35
        _RimLightBorderRangeMax ("リムライトの境界 終了位置", Range(0.0, 1.0)) = 1
        _ShininessColor ("ハイライトカラー", Color) = (0.5,0.5,0.5)
        _Shininess ("ハイライト絞り", Range(0.0, 1.0)) = 0.078125
        _ShininessBorderRange("ハイライト境界", Range(0.0, 1.0)) = 1
        _Alpha ("リム透明度", Range(0.0, 1.0)) = 0.5
        _AlphaBorderRangeMin ("リム透明度の境界 開始位置", Range(0.0, 1.0)) = 0.5
        _AlphaBorderRangeMax ("リム透明度の境界 終了位置", Range(0.0, 1.0)) = 1
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
        }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD1;
                half3 lightDir : TEXCOORD2;
                half3 viewDir : TEXCOORD3;
            };

            float4 _LightColor0;
            float4 _WorldSpaceLightPosCustom;
            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed3 _RimLightColor;
            half _RimLightBorderRangeMin;
            half _RimLightBorderRangeMax;
            fixed _Alpha;
            half _AlphaBorderRangeMin;
            half _AlphaBorderRangeMax;
            fixed3 _ShininessColor;
            half _Shininess;
            half _ShininessBorderRange;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.normal = normalize(v.normal);
                o.lightDir = normalize(ObjSpaceLightDir(v.vertex));
                o.viewDir = normalize(ObjSpaceViewDir(v.vertex));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                col = _Color;

                half rimRate = dot(i.normal, i.viewDir);
                rimRate = (rimRate + 1) / 2;

                // リムカラー
                half rimLightRate = smoothstep(_RimLightBorderRangeMin, _RimLightBorderRangeMax, rimRate);
                col.rgb = lerp(_RimLightColor, col.rgb, rimLightRate);

                // リムアルファ
                half rimAlphaRate = smoothstep(_AlphaBorderRangeMin, _AlphaBorderRangeMax, rimRate);
                col.a = lerp(col.a, _Alpha, rimAlphaRate);

                // 鏡面反射
                half3 halfDir = normalize(i.lightDir + i.viewDir);
                half specularRate = pow(max(0, dot(i.normal, halfDir)), _Shininess * 128.0);
                half shininessBorderRangeHalf = _ShininessBorderRange / 2;
                specularRate = smoothstep(0.5 - shininessBorderRangeHalf, 0.5 + shininessBorderRangeHalf, specularRate);
                half3 specular = specularRate * _LightColor0.rgb * _ShininessColor;
                col.rgb += specular;
                col.a += specularRate / 2;

                return col;
            }
            ENDCG
        }
    }
}