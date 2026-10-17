// Вода WildSnake (URP): волны в вершинах, цвет и прозрачность по глубине, пена у берега,
// блик солнца, отражение неба (окружающий свет SH — меняется с днём и ночью), туман, приём теней.
// Рябь от существ, всплесков и дождя — глобальный массив _WaterRipples (его заполняет WaterRipples), считается в пикселе.
// Течение — сдвиг UV карты нормалей (_Flow, у реки U идёт вдоль русла).
// Глубина до дна запечена в меш (UV2.x, м; BoardView считает её по рельефу): буфер глубины камеры не нужен —
// дешевле и работает на любых настройках графики.
Shader "WildSnake/Water"
{
    Properties
    {
        _ShallowColor ("Мелко (rgb, a — прозрачность)", Color) = (0.32, 0.62, 0.62, 0.35)
        _DeepColor ("Глубоко (rgb, a — прозрачность)", Color) = (0.07, 0.24, 0.32, 0.9)
        _DepthRange ("Глубина до тёмного цвета, м", Float) = 1.6
        _FoamColor ("Пена", Color) = (0.92, 0.96, 0.95, 1)
        _FoamWidth ("Ширина пены у берега, м", Float) = 0.35
        [Normal] _BumpMap ("Рябь (карта нормалей)", 2D) = "bump" {}
        _BumpScale ("Сила ряби", Float) = 0.45
        _Flow ("Течение слоёв ряби, UV/с (xy — первый, zw — второй)", Vector) = (0.02, 0.015, -0.017, 0.01)
        _WaveHeight ("Высота волн, м", Float) = 0.03
        _WaveLength ("Длина волн, м", Float) = 3.5
        _WaveSpeed ("Скорость волн", Float) = 0.8
        _Gloss ("Резкость блика", Float) = 220
        _Specular ("Сила блика", Float) = 1.4
        _Reflectivity ("Отражение неба", Range(0, 1)) = 0.75
        _RippleStrength ("Сила ряби от существ", Float) = 1
        _ShoreFade ("Мягкий край у берега, м", Float) = 0.04
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #define MAX_RIPPLES 16

            TEXTURE2D(_BumpMap);
            SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _ShallowColor;
                float4 _DeepColor;
                float _DepthRange;
                float4 _FoamColor;
                float _FoamWidth;
                float4 _BumpMap_ST;
                float _BumpScale;
                float4 _Flow;
                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;
                float _Gloss;
                float _Specular;
                float _Reflectivity;
                float _RippleStrength;
                float _ShoreFade;
            CBUFFER_END

            // Рябь: xy — точка на воде (мировые X, Z), z — время появления, w — сила.
            float4 _WaterRipples[MAX_RIPPLES];
            float _WaterRippleCount;
            // Часы ряби задаёт WaterRipples (Time.timeSinceLevelLoad) — те же, что у моментов появления колец.
            float _WaterTime;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float2 depth : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 waveNormal : TEXCOORD2;
                float fogFactor : TEXCOORD3;
                float depth : TEXCOORD4;
            };

            static const float2 WAVE_DIRS[3] = { float2(0.8, 0.6), float2(-0.45, 0.9), float2(0.2, -1.0) };
            static const float WAVE_SCALES[3] = { 1.0, 0.63, 0.41 };

            // Три пересекающиеся синусоиды по миру: дают «дыхание» глади без повторяющегося рисунка.
            float3 Waves(float3 p, out float3 normal)
            {
                float h = 0;
                float2 grad = 0;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float len = _WaveLength * WAVE_SCALES[i];
                    float k = 6.2831853 / len;
                    float a = _WaveHeight * WAVE_SCALES[i];
                    float phase = dot(WAVE_DIRS[i], p.xz) * k + _Time.y * _WaveSpeed * k * sqrt(len) * 0.5;
                    h += a * sin(phase);
                    grad += a * k * cos(phase) * WAVE_DIRS[i];
                }
                normal = normalize(float3(-grad.x, 1, -grad.y));
                return float3(0, h, 0);
            }

            // Кольца ряби: наклон поверхности (xy) и пена на гребнях (z).
            float3 Ripples(float2 p)
            {
                float2 grad = 0;
                float foam = 0;
                int count = (int)_WaterRippleCount;
                for (int i = 0; i < MAX_RIPPLES; i++)
                {
                    if (i >= count) break;
                    float4 r = _WaterRipples[i];
                    float age = _WaterTime - r.z;
                    if (age < 0 || age > 2.6) continue;
                    float2 dv = p - r.xy;
                    float d = length(dv) + 1e-4;
                    float x = d - age * 1.15;
                    // Дальше 0.8 м от гребня кольцо невидимо — не тратим exp/cos.
                    if (abs(x) > 0.8) continue;
                    float fade = saturate(1 - age / 2.6) * r.w;
                    float env = exp(-x * x * 7.0) * fade;
                    grad += dv / d * (env * cos(x * 15.0) * 0.22);
                    foam += env * saturate(1.2 - age * 1.5) * 0.7;
                }
                return float3(grad * _RippleStrength, saturate(foam * _RippleStrength));
            }

            Varyings Vert(Attributes input)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(input.positionOS.xyz);
                float3 n;
                ws += Waves(ws, n);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.waveNormal = n;
                o.uv = input.uv;
                o.depth = input.depth.x;
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // Две карты нормалей, бегущие в разные стороны, — рябь не «едет» одним листом.
                float2 uv = i.uv * _BumpMap_ST.xy + _BumpMap_ST.zw;
                float3 n1 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv + _Flow.xy * _Time.y), _BumpScale);
                float3 n2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv * 1.7 + float2(0.37, 0.71) + _Flow.zw * _Time.y), _BumpScale);
                float3 ripple = Ripples(i.positionWS.xz);
                float3 normal = normalize(i.waveNormal + float3(n1.x + n2.x, 0, n1.y + n2.y) + float3(-ripple.x, 0, -ripple.y));

                // Глубина до дна: мелко — светло и прозрачно, глубоко — темно. За кромкой берега она уходит в минус —
                // там вода плавно исчезает, и линия берега получается мягкой.
                float thickness = max(0, i.depth);
                float depth01 = saturate(thickness / max(0.01, _DepthRange));
                depth01 = 1 - (1 - depth01) * (1 - depth01);

                float3 albedo = lerp(_ShallowColor.rgb, _DeepColor.rgb, depth01);
                float alpha = lerp(_ShallowColor.a, _DeepColor.a, depth01);

                float3 viewDir = normalize(GetWorldSpaceViewDir(i.positionWS));
                Light light = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float atten = light.shadowAttenuation * light.distanceAttenuation;
                float3 ambient = SampleSH(normal);
                float ndl = saturate(dot(normal, light.direction));
                float3 color = albedo * (ambient + light.color * ndl * atten * 0.55);

                float fresnel = pow(1 - saturate(dot(normal, viewDir)), 4);
                float3 sky = SampleSH(reflect(-viewDir, normal)) * 1.15;
                color = lerp(color, sky, fresnel * _Reflectivity);
                float3 h = normalize(light.direction + viewDir);
                color += light.color * pow(saturate(dot(normal, h)), _Gloss) * _Specular * atten;

                // Пена: полоса у берега, рваная по ряби, плюс гребни колец от всплесков.
                float shore = saturate(1 - thickness / max(0.01, _FoamWidth));
                float broken = saturate(0.55 + (n1.x - n2.y) * 1.6);
                float foam = saturate(smoothstep(0.35, 0.85, shore * broken) + ripple.z);
                color = lerp(color, _FoamColor.rgb * (ambient + light.color * atten * 0.6), foam * _FoamColor.a);

                alpha = saturate(alpha + fresnel * 0.25 + foam * 0.6);
                // Мягкий край там, где вода касается берега.
                alpha *= saturate(thickness / max(1e-4, _ShoreFade));

                color = MixFog(color, i.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
