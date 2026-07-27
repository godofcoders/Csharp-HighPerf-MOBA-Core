Shader "MOBA/Storm/LensWaterDroplets"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.88, 0.96, 1.0, 0.36)
        _Intensity ("Intensity", Range(0, 1)) = 0.55
        _Scale ("Scale", Float) = 1.0
        _Speed ("Speed", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent+80"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            fixed4 _Tint;
            float _Intensity;
            float _Scale;
            float _Speed;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float DropletLayer(float2 uv, float time, float scale, float speed)
            {
                uv *= scale;

                float2 cell = floor(uv);
                float2 local = frac(uv) - 0.5;
                float rnd = Hash21(cell);

                float active = step(0.56, rnd);
                float slide = frac(time * speed * lerp(0.18, 0.58, rnd) + rnd);
                local.y = frac(local.y + slide + 0.5) - 0.5;
                local.x += sin(time * lerp(0.8, 1.8, rnd) + rnd * 6.2831) * 0.045;

                float width = lerp(0.045, 0.085, Hash21(cell + 7.13));
                float height = lerp(0.13, 0.28, Hash21(cell + 19.9));
                float2 dropShape = float2(local.x / width, local.y / height);
                float body = smoothstep(1.0, 0.25, dot(dropShape, dropShape));

                float tailX = smoothstep(0.035, 0.0, abs(local.x));
                float tailY = smoothstep(0.42, -0.02, local.y) * smoothstep(-0.5, -0.16, local.y);
                float tail = tailX * tailY * 0.38;

                return saturate(max(body, tail)) * active;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float aspect = _ScreenParams.x / max(1.0, _ScreenParams.y);
                float2 lensUv = float2((uv.x - 0.5) * aspect + 0.5, uv.y);
                float time = _Time.y * _Speed;

                float largeDrops = DropletLayer(lensUv + float2(0.07, 0.11), time, 4.8 * _Scale, 1.0);
                float smallDrops = DropletLayer(lensUv + float2(0.41, 0.27), time + 3.7, 9.5 * _Scale, 1.35) * 0.58;

                float2 streakUv = lensUv * float2(16.0, 5.5) + float2(time * -0.38, time * 1.25);
                float streakRnd = Hash21(floor(streakUv));
                float streak = smoothstep(0.985, 1.0, streakRnd) * 0.20;

                float droplet = saturate(largeDrops + smallDrops + streak);
                float edgeFade = smoothstep(0.0, 0.16, uv.x) *
                                 (1.0 - smoothstep(0.84, 1.0, uv.x)) *
                                 smoothstep(0.0, 0.12, uv.y) *
                                 (1.0 - smoothstep(0.88, 1.0, uv.y));

                float alpha = droplet * _Tint.a * _Intensity * edgeFade;
                float3 color = _Tint.rgb + droplet * 0.22;
                return fixed4(color, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
