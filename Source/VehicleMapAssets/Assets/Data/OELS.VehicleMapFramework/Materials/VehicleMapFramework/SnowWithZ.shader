Shader "VehicleMapFramework/SnowWithZ" {
    Properties {
        _MainTex ("Main texture", 2D) = "white" {}
        _PollutedTex ("Polluted texture", 2D) = "white" {}
        _MacroTex ("Macro texture", 2D) = "white" {}
        _AlphaAddTex ("Alpha add texture", 2D) = "white" {}
    }
    SubShader {
        Tags { 
            "IGNOREPROJECTOR" = "true" 
            "QUEUE" = "Transparent+175" // SunShadow(Transparent+170)の後
            "RenderType" = "Transparent" 
        }
        Pass {
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float4 color : COLOR;
            };

            struct v2f {
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR0;
            };

            sampler2D _MainTex;
            sampler2D _PollutedTex;
            sampler2D _MacroTex;
            sampler2D _AlphaAddTex;

            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.vertex.xz * 0.0625;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv;
                float4 macroTex = tex2D(_MacroTex, uv * 0.25);
                float4 mainTex = tex2D(_MainTex, uv);
                float4 pollutedTex = tex2D(_PollutedTex, uv * 0.5);

                float4 vertexColorMask = float4(1.0, i.color.yzw);
                float4 baseColor = macroTex * mainTex * vertexColorMask;

                // ノイズ生成
                float noise1   = tex2D(_AlphaAddTex, uv).g;
                float noise05  = tex2D(_AlphaAddTex, uv * 0.5).r;
                float noise8   = tex2D(_AlphaAddTex, uv * 8.0).b;
                
                float noiseSum = (noise1 + noise05 + noise8) * 0.333;

                // アルファ値の階調化（ポスタリゼーション）
                float alphaVal = noiseSum * 0.4 + baseColor.a * 0.6;
                float finalAlpha;
                if (alphaVal > 0.65) {
                    finalAlpha = baseColor.a;
                } else if (alphaVal > 0.4) {
                    finalAlpha = baseColor.a * 0.9;
                } else if (alphaVal > 0.18) {
                    finalAlpha = baseColor.a * 0.75;
                } else {
                    finalAlpha = baseColor.a * 0.6;
                }
                clip(finalAlpha - 0.01);

                // Pollutionブレンド
                float lerpFactor = (2.0 - finalAlpha * 0.5) * i.color.r;
                lerpFactor = saturate(lerpFactor);
                float3 finalRGB = lerp(baseColor.rgb, pollutedTex.rgb, lerpFactor);

                return float4(finalRGB, finalAlpha);
            }
            ENDCG
        }
    }
}