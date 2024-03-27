Shader "PandaShad/Util/GetShaderTime"
{
	Properties { }
	SubShader
	{
		Tags { "RenderType" = "Transparent" "Queue" = "Transparent" }
		LOD 100
		ZTest Always
		ZWrite On
		Blend One Zero
		
		Pass
		{
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"
			
			struct appdata
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
			};
			
			struct v2f
			{
				float2 uv: TEXCOORD0;
				float4 vertex: SV_POSITION;
			};
			
			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				return o;
			}
			
			fixed4 frag(v2f i): SV_Target
			{
				float time = _Time.y;
				//udonがbitconverter使えないので独自形式で送る
				uint intVal = floor(time);
				float decValVal = frac(time);
				return fixed4(
					((intVal >> 16) & 0xFF) / 255.0,
					((intVal >> 8) & 0xFF) / 255.0,
					((intVal >> 0) & 0xFF) / 255.0,
					decValVal
				);
				/*
				uint val = asuint(time);
				return fixed4(
					((val >> 24) & 0xFF) / 255.0,
					((val >> 16) & 0xFF) / 255.0,
					((val >> 8) & 0xFF) / 255.0,
					((val >> 0) & 0xFF) / 255.0
				);
				*/
			}
			ENDCG
			
		}
	}
}
