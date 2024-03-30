#ifndef _PANDA_VAT_CMN_H_
#define _PANDA_VAT_CMN_H_

#pragma multi_compile PANDA_VAT_IDENTIFY
#pragma shader_feature _ VAT_ROTATION_INTERPOLATION
#pragma shader_feature _ VAT_CTRL_WITH_RATE VAT_LOOP
#pragma shader_feature _ VAT_LOOP

#if !PANDA_VAT

#endif

/******************************** property ***************************/
uniform sampler2D _VatTex;	//頂点アニメーションテクスチャ
uniform float4 _VatTex_TexelSize;

uniform float _VatFps;		//頂点アニメーションテクスチャのFPS

UNITY_INSTANCING_BUFFER_START(VatProps)
#if VAT_CTRL_WITH_RATE
	UNITY_DEFINE_INSTANCED_PROP(float, _VatRate)//頂点アニメーション時間割合
#else
	UNITY_DEFINE_INSTANCED_PROP(float, _VatStartTimeSec)//頂点アニメーション開始時間 秒
	UNITY_DEFINE_INSTANCED_PROP(float, _VatSpeed)//頂点アニメーションスピード
#endif
UNITY_INSTANCING_BUFFER_END(VatProps)

/******************************** type define ***************************/

/******************************** macro define ***************************/

//ほとんど同じ
#define ALMOST_EQUAL(a, b) (length(a - b) < 1e-4)

/******************************** static variable ***************************/
static float _VatVertexCount = _VatTex_TexelSize.z; //テクスチャが持つ情報：頂点数
static float _TexelHeight = _VatTex_TexelSize.w;

static float _VatDeltaSec = (1.0 / _VatFps);//1フレームの時間[秒]

static float _Dx = 0.5 / _VatVertexCount; //VAT取得用の補正値X
static float _Dy = (0.5 / _TexelHeight);	//VAT取得用の補正値Y


/********************************  ***************************/

#if VAT_ROTATION_INTERPOLATION
	#include "Packages/com.panda-nakami.pandavat/Runtime/Shader/PandaVatRotationInterpolatioinMode.cginc"
#else
	#include "Packages/com.panda-nakami.pandavat/Runtime/Shader/PandaVatBasicMode.cginc"
#endif


#endif
