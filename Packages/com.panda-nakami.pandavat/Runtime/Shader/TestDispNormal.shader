Shader "PandaShad/Test/TestDispNormal"
{
	Properties { }
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		
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
				float3 normal: NORMAL;
			};
			
			struct v2f
			{
				float2 uv: TEXCOORD0;
				float4 vertex: SV_POSITION;
				float3 normal: NORMAL;
			};
			
			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.normal = UnityObjectToWorldDir(v.normal) / 2 + 0.5;
				return o;
			}
			
			fixed4 frag(v2f i): SV_Target
			{
				// sample the texture
				fixed4 col = fixed4(i.normal, 1);
				
				return col;
			}
			ENDCG
			
		}
	}
}
