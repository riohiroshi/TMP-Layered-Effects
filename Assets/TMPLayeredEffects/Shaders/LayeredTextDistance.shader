// Jump Flood Algorithm over the cached face coverage. The result stores the UV of the
// nearest covered pixel, allowing the compositor to form genuinely round Euclidean bands.
Shader "Hidden/TMPLayeredEffects/Layered Text Distance"
{
    Properties { _MainTex ("Input", 2D) = "black" {} }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"
        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        float2 _StepUV;

        struct AppData { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
        struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

        Varyings Vert(AppData input)
        {
            Varyings output;
            output.vertex = UnityObjectToClipPos(input.vertex);
            output.uv = input.uv;
            return output;
        }
        ENDCG

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Seed

            float4 Seed(Varyings input) : SV_Target
            {
                half coverage = tex2D(_MainTex, input.uv).a;
                return coverage >= 0.5 ? float4(input.uv, 0, 1) : float4(-1, -1, 0, 1);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Jump

            float4 Jump(Varyings input) : SV_Target
            {
                float2 best = tex2D(_MainTex, input.uv).xy;
                float2 pixelScale = _MainTex_TexelSize.zw;
                float bestDistance = best.x >= 0
                    ? dot((input.uv - best) * pixelScale, (input.uv - best) * pixelScale)
                    : 3.402823466e+38;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        if (x == 0 && y == 0) continue;
                        float2 sampleUV = input.uv + float2(x, y) * _StepUV;
                        if (any(sampleUV < 0) || any(sampleUV > 1)) continue;
                        float2 candidate = tex2D(_MainTex, sampleUV).xy;
                        if (candidate.x < 0) continue;
                        float2 delta = (input.uv - candidate) * pixelScale;
                        float distanceSquared = dot(delta, delta);
                        if (distanceSquared < bestDistance)
                        {
                            bestDistance = distanceSquared;
                            best = candidate;
                        }
                    }
                }
                return float4(best, 0, 1);
            }
            ENDCG
        }
    }
}
