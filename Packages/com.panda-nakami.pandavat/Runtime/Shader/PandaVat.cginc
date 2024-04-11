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
	UNITY_DEFINE_INSTANCED_PROP(float, _VatStartTimeOffset)//開始前時間 秒
	UNITY_DEFINE_INSTANCED_PROP(float, _VatEndTimeOffset)//終了後時間 秒
#endif
UNITY_INSTANCING_BUFFER_END(VatProps)

/******************************** type define ***************************/

/******************************** macro define ***************************/

//ほとんど同じ
#define ALMOST_EQUAL(a, b) (length(a - b) < 1e-4)

/******************************** static variable ***************************/
static float _TexelWidth = _VatTex_TexelSize.z; //テクスチャが持つ情報：頂点数
static float _TexelHeight = _VatTex_TexelSize.w;

static float _VatDeltaSec = (1.0 / _VatFps);//1フレームの時間[秒]

static float _Dx = 0.5 / _TexelWidth; //VAT取得用の補正値X
static float _Dy = (0.5 / _TexelHeight);	//VAT取得用の補正値Y

//頂点シェーダー内で割合情報が必要な人用 X
#ifdef VAT_CACHE_RATE_X
	static float _VatCacheRateX;
#endif
//頂点シェーダー内で割合情報が必要な人用 Y
#ifdef VAT_CACHE_RATE_Y
	static float _VatCacheYBeforeRate;
	static float _VatCacheYAfterRate;
	static float _VatCacheMixRate;
#endif

/******************************** prototype ***************************/
inline void _GetXRate(uint texHorizonInfo, out float uvX);
inline void _GetYRate(out float frameRateBefore, out float frameRateAfter, out float mixRate);

#ifndef GET_VAT_Y_RATE_FUNCTION
	#define GET_VAT_Y_RATE_FUNCTION _GetYRate
#endif
/********************************  ***************************/

#if VAT_ROTATION_INTERPOLATION
	#include "Packages/com.panda-nakami.pandavat/Runtime/Shader/PandaVatRotationInterpolationMode.cginc"
#else
	#include "Packages/com.panda-nakami.pandavat/Runtime/Shader/PandaVatBasicMode.cginc"
#endif


//VAT座標用情報取得
// => 頂点/boneに対応するUV.x情報
inline void _GetXRate(uint texHorizonInfo, out float uvX)
{
	///頂点/bone位置をとる(x)
	uvX = float(texHorizonInfo) / _TexelWidth;
}

//VAT座標用情報取得
// => テクスチャ中のどのフレームをとるかの情報(前フレーム、次フレーム、今の時間での前次フレームの位置割合)
inline void _GetYRate(out float frameRateBefore, out float frameRateAfter, out float mixRate)
{
	///フレーム位置をとる(y)。
	#if VAT_CTRL_WITH_RATE
		//プロパティの割合を指定
		float frameRateRaw = UNITY_ACCESS_INSTANCED_PROP(VatProps, _VatRate);
	#else
		
		float speed = UNITY_ACCESS_INSTANCED_PROP(VatProps, _VatSpeed);
		
		//時間をとる
		float diffTimeSec = max(0, (_Time.y - UNITY_ACCESS_INSTANCED_PROP(VatProps, _VatStartTimeSec)) * speed);

		//  startOfst      _VatDuration     endOfst
		// |----------|------------------|----------|
		float startOffset = UNITY_ACCESS_INSTANCED_PROP(VatProps, _VatStartTimeOffset) ;
		float endOffset = UNITY_ACCESS_INSTANCED_PROP(VatProps, _VatEndTimeOffset) ;
		float totalDuration = _VatDuration + startOffset +endOffset;
		
		#if VAT_LOOP
			float posSec_ = fmod(diffTimeSec, totalDuration);
		#else
			float posSec_ = min(diffTimeSec, totalDuration);
		#endif
		//_VatDuration内の秒数に収める
		float posSec = min(_VatDuration, max(0, posSec_ - startOffset));

		//フレーム位置をとる(y)。
		float frameRateRaw = posSec / _VatDuration;

	#endif

	//前VATフレーム位置
	frameRateBefore = floor(frameRateRaw * _VatFrameCount) / _VatFrameCount;
	//次VATフレーム位置
	frameRateAfter = min(frameRateBefore + _VatDeltaFrameRate, 1);//最大割合時、0に戻ってしまうのを防ぐ

	//前VATフレームと次VATフレームで、今の時間がどれだけ次VATフレームに寄っているか割合
	mixRate = (frameRateRaw - frameRateBefore) / _VatDeltaFrameRate;
}

#endif
