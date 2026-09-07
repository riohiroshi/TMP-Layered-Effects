// Rasterizes TMP's laid-out glyph quads into a plain antialiased face texture.
// TMP is only the layout / atlas source here; no TMP outline or underlay participates.
Shader "Hidden/TMPLayeredEffects/Layered Text Mask"
{
    Properties { _MainTex ("Font Atlas", 2D) = "white" {} }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            CGPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            struct AppData
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(AppData input)
            {
                Varyings output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.color = input.color;
                output.uv = input.uv;
                return output;
            }

            fixed4 Frag(Varyings input) : SV_Target
            {
                half distance = tex2D(_MainTex, input.uv).a;
                half antialias = max(fwidth(distance), 1.0 / 255.0);
                half coverage = smoothstep(0.5 - antialias, 0.5 + antialias, distance);
                return fixed4(input.color.rgb, input.color.a * coverage);
            }
            ENDCG
        }
    }
}
