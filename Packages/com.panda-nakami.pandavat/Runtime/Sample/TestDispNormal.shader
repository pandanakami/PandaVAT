Shader "PandaShad/Test/TestDispNormal"
{
	Properties
	{
		[Toggle(IS_DISP_TANGENT)] _IsDispTangent ("IsDispTangent", Int) = 0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		
		Pass
		{
			CGPROGRAM
			
			#pragma vertex vert
			#pragma fragment frag
			#pragma shader_feature _ IS_DISP_TANGENT
			#include "UnityCG.cginc"
			
			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				#if IS_DISP_TANGENT
					float4 tangent : TANGENT;
				#else
					float3 normal : NORMAL;
				#endif
			};
			
			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float3 normal : NORMAL;
			};
			
			v2f vert(appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.normal = UnityObjectToWorldDir(
					#if IS_DISP_TANGENT
						v.tangent.xyz
					#else
						v.normal
					#endif
					
				) / 2 + 0.5;
				return o;
			}
			
			fixed4 frag(v2f i) : SV_Target
			{
				// sample the texture
				fixed4 col = fixed4(i.normal, 1);

				return col;
			}
			ENDCG
		}
	}
}
