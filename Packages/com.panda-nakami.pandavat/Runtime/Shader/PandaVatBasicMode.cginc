#ifndef _PANDA_VAT_BASIC_H_
#define _PANDA_VAT_BASIC_H_

/******************************** type define ***************************/

//VAT差分情報構造体
struct VatDiffInfo
{
	float3 posDiff;		//位置差分
	
	#ifdef VAT_USE_NORMAL
		float3 normalDiff;	//法線差分
	#endif

	#ifdef VAT_USE_TANGENT
		float4 tangentDiff;	//接線差分
	#endif

	#if VAT_OBJECT_ON_OFF_ENABLE
		float isObjectOn;
	#endif
};

/******************************** macro define ***************************/

/******************************** static variable ***************************/
static float _VatFrameCount = (_VatTex_TexelSize.w / 3) - 1; //テクスチャが持つ情報：フレーム数
static float _VatDeltaFrameRate = (1.0 / _VatFrameCount);//フレーム数の逆数
static float _VatDuration = (_VatFrameCount / _VatFps);//アニメーションの総時間[秒]

/******************************** prototype declaration ***************************/
inline VatDiffInfo _GetFrameAttribute(float vertRate, float frameRate);//指定時間でのフレームの差分情報を取得する
inline VatDiffInfo _MixVatAttribute(VatDiffInfo before, VatDiffInfo after, float frameRateAfterRate);//VATの前後フレームの差分情報を合成する

/******************************** public function ***************************/

// VATの情報を取得
inline void ApplyVatInfo(uint vertexId, inout float4 vertex
#ifdef VAT_USE_NORMAL
	, inout float3 normal
#endif
#ifdef VAT_USE_TANGENT
	, inout float4 tangent
#endif
)
{
	float uvX;
	//前VATフレーム位置
	float frameRateBefore;
	//次VATフレーム位置
	float frameRateAfter;
	//前VATフレームと次VATフレームで、今の時間がどれだけ次VATフレームに寄っているか割合
	float mixRate;

	//uv.x取得
	_GetXRate(vertexId, uvX);
	//フレーム位置情報取得
	GET_VAT_Y_RATE_FUNCTION(frameRateBefore, frameRateAfter, mixRate);

	//頂点シェーダー内で割合情報が必要な人用
	#ifdef VAT_CACHE_RATE_X
		_VatCacheRateX = uvX;
	#endif
	#ifdef VAT_CACHE_RATE_Y
		_VatCacheYBeforeRate = frameRateBefore;
		_VatCacheYAfterRate = frameRateAfter;
		_VatCacheMixRate = mixRate;
	#endif

	//前・次VATフレームの差分情報を取得
	VatDiffInfo before = _GetFrameAttribute(uvX, frameRateBefore);
	VatDiffInfo after = _GetFrameAttribute(uvX, frameRateAfter);

	//前・次VATフレーム差分情報を線形補間する
	VatDiffInfo mixInfo = _MixVatAttribute(before, after, mixRate);

	//位置差分をローカル情報に加味
	vertex.xyz += mixInfo.posDiff;

	//Object ON/OFFアニメーション有効
	#if VAT_OBJECT_ON_OFF_ENABLE
		vertex.xyz = lerp(float3(0, 0, 0), vertex.xyz, mixInfo.isObjectOn);
	#endif

	#ifdef VAT_USE_NORMAL
		normal += mixInfo.normalDiff;
	#endif

	#ifdef VAT_USE_TANGENT
		tangent += mixInfo.tangentDiff;
	#endif
}

/******************************** private function ***************************/

//指定時間でのフレームの差分情報を取得する
inline VatDiffInfo _GetFrameAttribute(float vertRate, float frameRate)
{
	VatDiffInfo o;
	
	//x,yからVAT位置情報取得
	float uvX = vertRate + _Dx;
	//フレームレート→テクセル高さ割合に変換
	//フレーム数Fに対し、テクセル高で割る
	float uvY = (frameRate * _VatFrameCount / _TexelHeight) + _Dy;
	float4 uv = float4(uvX, uvY, 0, 0);
	
	const float dy = 0.33333333333;

	//位置
	
	//Object ON/OFFアニメーション有効
	#if !VAT_OBJECT_ON_OFF_ENABLE
		o.posDiff = tex2Dlod(_VatTex, uv);
	#else
		float4 pos = tex2Dlod(_VatTex, uv);
		o.posDiff = pos.xyz;
		o.isObjectOn = pos.w;
	#endif

	//Normal
	uv.y += dy;
	#ifdef VAT_USE_NORMAL
		o.normalDiff = tex2Dlod(_VatTex, uv);
	#endif

	//Tangent
	uv.y += dy;
	#ifdef VAT_USE_TANGENT
		o.tangentDiff = tex2Dlod(_VatTex, uv);
	#endif

	return o;
}

//VATの前後フレームを線形補間する
inline VatDiffInfo _MixVatAttribute(VatDiffInfo before, VatDiffInfo after, float mixRate)
{
	VatDiffInfo o;
	o.posDiff = lerp(before.posDiff, after.posDiff, mixRate);

	#ifdef VAT_USE_NORMAL
		o.normalDiff = lerp(before.normalDiff, after.normalDiff, mixRate);
	#endif

	#ifdef VAT_USE_TANGENT
		o.tangentDiff = lerp(before.tangentDiff, after.tangentDiff, mixRate);
	#endif

	//Object ON/OFFアニメーション有効
	#if VAT_OBJECT_ON_OFF_ENABLE
		o.isObjectOn = before.isObjectOn;
	#endif

	return o;
}

//shader_featureでOFFの場合のコンパイル通る用
#define ApplyVatInfoRI
#endif
