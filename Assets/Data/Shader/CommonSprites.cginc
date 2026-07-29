#include "UnitySprites.cginc"

float4 _LightColor0;
half _IsIgnoredLight;

// 参考: https://gam0022.net/blog/2019/07/23/unity-y-axis-billboard-shader/
v2f BillboardVert(appdata_t IN)
{
    v2f OUT;

    UNITY_SETUP_INSTANCE_ID (IN);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    OUT.vertex = UnityFlipSprite(IN.vertex, _Flip);

    // Meshの原点をModelView変換
    float3 viewPos = UnityObjectToViewPos(float3(0, 0, 0));
        
    // スケールと回転（平行移動なし）だけModel変換して、View変換はスキップ
    float3 scaleRotatePos = mul((float3x3)unity_ObjectToWorld, IN.vertex);
        
    // scaleRotatePosを右手系に変換して、viewPosに加算
    // 本来はView変換で暗黙的にZが反転されているので、
    // View変換をスキップする場合は明示的にZを反転する必要がある
    viewPos += float3(scaleRotatePos.xy, -scaleRotatePos.z);
        
    OUT.vertex = mul(UNITY_MATRIX_P, float4(viewPos, 1));

    OUT.texcoord = IN.texcoord;

    OUT.color = IN.color * _Color * _RendererColor;

    float4 lightColor = lerp(_LightColor0, 1, _IsIgnoredLight);
    OUT.color.rgb *= lightColor.rgb;

    #ifdef PIXELSNAP_ON
    OUT.vertex = UnityPixelSnap (OUT.vertex);
    #endif

    return OUT;
}