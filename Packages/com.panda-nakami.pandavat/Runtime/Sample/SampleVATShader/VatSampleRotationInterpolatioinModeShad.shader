Shader "PandaShad/Vat/VatRotationInterpolationModeSample"
{
	Properties
	{
		//[> ここから新しいシェーダーにコピーしてね]
		[HideInInspector]_PandaVat ("PandaVat", int) = 1
		[HideInInspector][HDR] _VatTex ("VatTexture", 2D) = "black" { }
		[HideInInspector]_VatFps ("VatFps", float) = 30
		[HideInInspector]_VatCtrlWithRate ("Is Vat control with rate", Int) = 0
		[HideInInspector]_VatRate ("VatRate", Range(0, 1)) = 0
		[HideInInspector]_VatLoop ("Is animation loop", Int) = 1
		[HideInInspector]_VatStartTimeSec ("VatStartTimeSec", float) = 0
		[HideInInspector]_VatSpeed ("VatSpeed", float) = 1
		//回転補間用設定
		[HideInInspector]_RotationInterpolationMode ("RotationInterpolationMode", Int) = 1
		//[  ここまでコピーしてね <]

		//色にNormal表示するかTangent表示するか
		[Toggle(IS_DISP_TANGENT)] _IsDispTangent ("IsDispTangent", Int) = 0
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		
		Pass
		{
			CGPROGRAM
			
			#pragma target 3.5
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing

			#pragma shader_feature _ IS_DISP_TANGENT
			
			#include "UnityCG.cginc"

			//[> シェーダーの用途によってNormal, TangentをOn/Offしてね <]
			#define VAT_USE_NORMAL
			#if IS_DISP_TANGENT
				#define VAT_USE_TANGENT
			#endif
			
			//[> includeしてね <]
			#include "Packages/com.panda-nakami.pandavat/Runtime/Shader/PandaVat.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				uint vid : SV_VertexID;
				float3 normal : NORMAL;
				#if IS_DISP_TANGENT
					float4 tangent : TANGENT;
				#endif
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 vertex : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 normalColor : NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				//[> Vat座標取得 呼んでね <]
				ApplyVatInfo(v.vid, v.vertex, v.normal
				#if IS_DISP_TANGENT
					, v.tangent
				#endif
				);
				
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.normalColor = UnityObjectToWorldDir(
					#if IS_DISP_TANGENT
						v.tangent.xyz
					#else
						v.normal
					#endif
				);
				return o;
			}
			
			fixed4 frag(v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				//Normalを色にする
				fixed4 col = fixed4(normalize(i.normalColor) / 2 + 0.5, 1);
				return col;
			}
			ENDCG
		}
	}
}
