Shader "VehicleMapFramework/TerrainHardWithZ" {
    Properties {
       _MainTex ("Main texture", 2D) = "white" {}
       _Color ("Color", Color) = (1,1,1,1)
    }
    SubShader {
       Tags { "RenderType" = "Opaque" }
       
       Pass {
          ZWrite On
          
          Blend SrcAlpha OneMinusSrcAlpha
          
          CGPROGRAM
          #pragma vertex vert
          #pragma fragment frag
          #include "UnityCG.cginc"
          
          struct appdata { float4 vertex : POSITION; };
          struct v2f { float4 position : SV_POSITION; float2 texcoord : TEXCOORD0; };
          
          sampler2D _MainTex;
          float4 _MainTex_ST;
          float4 _Color;
          
          v2f vert(appdata v)
          {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                float2 baseUV = v.vertex.xz * 0.0625;
                o.texcoord = TRANSFORM_TEX(baseUV, _MainTex);
                return o;
          }
          
          fixed4 frag(v2f inp) : SV_Target
          {
                return tex2D(_MainTex, inp.texcoord) * _Color;
          }
          ENDCG
       }
    }
}