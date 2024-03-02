Shader "PandaShad/Vat/VatSample"
{
	Properties
	{
		[HDR] _VatTex ("VatTexture", 2D) = "black" { }
		_VatFps ("VatFps", float) = 30
		_VatStartTimeSec ("VatStartTimeSec", float) = 0
		_VatSpeed ("VatSpeed", float) = 1
		[Toggle(VAT_LOOP)] _VatLoop ("Is animation loop", Int) = 1
		[Toggle(VAT_CTRL_WITH_RATE)] _VatCtrlWithRate ("Is Vat control with rate", Int) = 0
		_VatRate ("VatRate", Range(0, 1)) = 0
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
			#pragma shader_feature _ VAT_CTRL_WITH_RATE VAT_LOOP
			
			#include "UnityCG.cginc"

			//シェーダーの用途によってOn/Off
			#define VAT_USE_NORMAL
			//#define VAT_USE_TANGENT
			#include "Packages/com.panda-nakami.pandavat/Runtime/Shader/PandaVat.cginc"
			
			struct appdata
			{
				float4 vertex: POSITION;
				float2 uv: TEXCOORD0;
				uint vid: SV_VertexID;
				float3 normal: NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};
			
			struct v2f
			{
				float4 vertex: SV_POSITION;
				float2 uv: TEXCOORD0;
				float3 normalColor: NORMAL;
				UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
			};
			
			v2f vert(appdata v)
			{
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
				
				//Vat差分取得
				VatDiffInfo info = GetVatDiff(v.vid);
				
				//位置差分をローカル情報に加味
				v.vertex.xyz += info.posDiff;
				v.normal.xyz += info.normalDiff;
				
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = v.uv;
				o.normalColor = UnityObjectToWorldDir(v.normal);
				return o;
			}
			
			fixed4 frag(v2f i): SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

				//Normalを色にする
				fixed4 col = fixed4(i.normalColor / 2 + 0.5, 1);
				return col;
			}
			ENDCG
			
		}
	}
}
