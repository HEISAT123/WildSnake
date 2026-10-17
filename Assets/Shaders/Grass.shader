// Трава WildSnake (URP): вырез по альфе, ветер в вершинах (сильнее к верхушкам), темнее у корня, светлее к кончикам,
// приём теней и туман. Существа раздвигают траву: точки из глобального массива _GrassBenders (их задаёт GrassBender),
// xyz — позиция, w — радиус. Высота травинки — UV.y (0 у земли, 1 у кончика).
Shader "WildSnake/Grass"
{
    Properties
    {
        [MainTexture] _BaseMap ("Травинки (альфа — вырез)", 2D) = "white" {}
        [MainColor] _BaseColor ("Цвет", Color) = (1, 1, 1, 1)
        _Cutoff ("Порог выреза", Range(0, 1)) = 0.5
        _RootShade ("Тень у корня", Range(0, 1)) = 0.55
        _TipTint ("Оттенок кончиков", Color) = (1.12, 1.1, 0.85, 1)
        _WindStrength ("Сила ветра, м", Float) = 0.12
        _WindSpeed ("Скорость ветра", Float) = 1.6
        _WindScale ("Размер порывов", Float) = 0.3
        _BendStrength ("Сила раздвигания", Float) = 0.7
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "GrassForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define MAX_BENDERS 4

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _Cutoff;
                float _RootShade;
                float4 _TipTint;
                float _WindStrength;
                float _WindSpeed;
                float _WindScale;
                float _BendStrength;
            CBUFFER_END

            float4 _GrassBenders[MAX_BENDERS];
            float _GrassBenderCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float height : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings o;
                float h = saturate(input.uv.y);
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                float sway = h * h;

                // Порывы: две волны по миру с разной частотой — соседние пучки качаются вразнобой.
                float t = _Time.y * _WindSpeed;
                float gust = sin(t + ws.x * _WindScale + ws.z * _WindScale * 0.7) * 0.7 + sin(t * 2.3 + ws.z * _WindScale * 2.1) * 0.3;
                ws.xz += float2(gust, gust * 0.45 + 0.25) * _WindStrength * sway;

                // Раздвигание: верхушки отклоняются от существа и чуть приминаются.
                int count = (int)_GrassBenderCount;
                for (int i = 0; i < MAX_BENDERS; i++)
                {
                    if (i >= count) break;
                    float4 b = _GrassBenders[i];
                    float2 d = ws.xz - b.xz;
                    float dist = length(d) + 1e-4;
                    float k = saturate(1 - dist / b.w) * step(abs(ws.y - b.y), 2.0);
                    ws.xz += d / dist * (k * sway * b.w * _BendStrength);
                    ws.y -= k * h * 0.3 * b.w;
                }

                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                o.height = h;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv);
                clip(tex.a - _Cutoff);
                float h = i.height;
                float3 albedo = tex.rgb * _BaseColor.rgb * lerp(_RootShade, 1, h) * lerp(float3(1, 1, 1), _TipTint.rgb, h * h);

                // Свет как у плоской травы: нормаль вверх, мягкий «полупрозрачный» рассеянный свет.
                float3 up = float3(0, 1, 0);
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float atten = light.shadowAttenuation * light.distanceAttenuation;
                float3 color = albedo * (SampleSH(up) + light.color * atten * (0.35 + 0.65 * saturate(dot(up, light.direction))));
                color = MixFog(color, i.fogFactor);
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
