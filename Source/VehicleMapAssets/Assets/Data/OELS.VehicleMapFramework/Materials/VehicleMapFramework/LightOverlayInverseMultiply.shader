Shader "VehicleMapFramework/LightOverlayInverseMultiply" {
	Properties {
		_Color ("Color", Color) = (1, 1, 1, 1)
		_RestoreFactor ("Recovery Factor", Range(0, 10)) = 1
		_MaxRestore ("Max Recovery Limit", Range(0, 1)) = 0.5
		_ColorPreservation ("Color Preservation", Range(0, 1)) = 0.3
	}
	SubShader {
		Tags
		{
			"IgnoreProjector" = "true"
			"Queue" = "Transparent+101"
			"RenderType" = "Opaque"
		}
		
		GrabPass { }
		
		Pass {
			ZWrite Off
			
			Blend SrcAlpha OneMinusSrcAlpha
			
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"
			
			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
			};

			struct v2f
			{
				float4 texcoord : TEXCOORD0;
				float4 position : SV_POSITION;
				float4 color : COLOR0;
			};

			float4 _Color;
			float _RestoreFactor;
			float _MaxRestore;
			float _ColorPreservation;
			sampler2D _GrabTexture;
			
			v2f vert(appdata v)
			{
				v2f o;
				o.position = UnityObjectToClipPos(v.vertex);
				o.texcoord = ComputeGrabScreenPos(o.position);
				
				o.color = v.color;
				
				return o;
			}
			
			fixed4 frag(v2f inp) : SV_Target
			{
				fixed4 bg = tex2Dproj(_GrabTexture, inp.texcoord);
				
				// 除算
				float3 shadowColor = max(_Color.rgb, 0.001);
				float3 restored = bg.rgb / shadowColor * _RestoreFactor;
				
				// 白飛び防止
				restored = min(restored, _MaxRestore);

				// 彩度を調整
				return fixed4(restored, inp.color.a);
			}
			ENDCG
		}
	}
}