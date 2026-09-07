// Composites independent layers behind the face. Unlike a TMP SDF outline, neither band
// replaces any yellow face pixels: shadow -> pale band -> navy band -> captured face.
Shader "Hidden/TMPLayeredEffects/Layered Text Composite"
{
    Properties
    {
        _MainTex ("Face", 2D) = "black" {}
        _SeedTex ("Nearest Seed", 2D) = "black" {}
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _SeedTex;
            float2 _TextureSize;
            float _InnerRadius;
            float _OuterRadius;
            float _ShadowRadius;
            float _ShadowSoftness;
            float2 _ShadowOffset;
            fixed4 _InnerColor;
            fixed4 _OuterTopColor;
            fixed4 _OuterBottomColor;
            fixed4 _ShadowColor;

            struct AppData { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(AppData input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            float DistanceToFace(float2 uv)
            {
                if (any(uv < 0) || any(uv > 1)) return 65504.0;
                float2 seed = tex2D(_SeedTex, uv).xy;
                if (seed.x < 0) return 65504.0;
                return length((uv - seed) * _TextureSize);
            }

            fixed4 Over(fixed4 front, fixed4 back)
            {
                fixed alpha = front.a + back.a * (1 - front.a);
                fixed3 rgb = alpha > 0
                    ? (front.rgb * front.a + back.rgb * back.a * (1 - front.a)) / alpha
                    : 0;
                return fixed4(rgb, alpha);
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                float distance = DistanceToFace(input.uv);
                half innerAlpha = _InnerRadius > 0
                    ? saturate(_InnerRadius + 0.5 - distance)
                    : 0;
                half outerAlpha = _OuterRadius > 0
                    ? saturate(_OuterRadius + 0.5 - distance)
                    : 0;

                float2 shadowUV = input.uv - _ShadowOffset / _TextureSize;
                float shadowDistance = DistanceToFace(shadowUV);
                half shadowAlpha = 1 - smoothstep(_ShadowRadius,
                    _ShadowRadius + max(_ShadowSoftness, 0.001), shadowDistance);

                fixed4 result = fixed4(_ShadowColor.rgb, _ShadowColor.a * shadowAlpha);
                fixed4 outerColor = lerp(_OuterBottomColor, _OuterTopColor, input.uv.y);
                outerColor.a *= outerAlpha;
                result = Over(outerColor, result);

                fixed4 innerColor = _InnerColor;
                innerColor.a *= innerAlpha;
                result = Over(innerColor, result);

                fixed4 face = tex2D(_MainTex, input.uv);
                result = Over(face, result);
                return result;
            }
            ENDCG
        }
    }
}
