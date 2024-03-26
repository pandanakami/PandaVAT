#ifndef _PANDA_VAT_H_
#define _PANDA_VAT_H_

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
};

/******************************** macro define ***************************/
#define VAT_SCALE (1)	//VAT位置情報受け渡し用補正値

/******************************** static variable ***************************/
static float _VatVertexCount = _VatTex_TexelSize.z; //頂点アニメーションテクスチャの頂点数
static float _VatFrameCount = (_VatTex_TexelSize.w / 3); //頂点アニメーションテクスチャのフレーム数

static float _VatDuration = (_VatFrameCount / _VatFps);//アニメーションクリップの総時間 秒
static float _VatDeltaSec = (1.0 / _VatFps);//頂点アニメーションテクスチャの1フレームの時間 秒

static float _Dx = 0.5 / _VatTex_TexelSize.z; //VAT取得用の補正値X
static float _Dy = (0.5 / _VatFrameCount);	//VAT取得用の補正値Y

/******************************** prototype declaration ***************************/
inline VatDiffInfo _GetFrameAttribute(float vertRate, float frameRate);//指定時間でのフレームの差分情報を取得する
inline VatDiffInfo _MixVatAttribute(VatDiffInfo before, VatDiffInfo after, float frameRateAfterRate);//VATの前後フレームの差分情報を合成する

/******************************** public function ***************************/
//VATの差分を取得
inline VatDiffInfo GetVatDiff(uint vertexId)
{
	///頂点位置をとる(x)
	float vertUvX = float(vertexId) / _VatVertexCount;
	
	///フレーム位置をとる(y)。
	#if VAT_CTRL_WITH_RATE
		//プロパティの割合を指定
		float frameRateRaw = UNITY_ACCESS_INSTANCED_PROP(VatProps, _VatRate);
	#else
		
		float speed = UNITY_ACCESS_INSTANCED_PROP(VatProps, _VatSpeed);
		
		//時間をとる
		float diffTimeSec = (_Time.y - UNITY_ACCESS_INSTANCED_PROP(VatProps, _VatStartTimeSec)) * speed;
		
		
		#if VAT_LOOP
			float posSec = fmod(diffTimeSec, _VatDuration);
		#else
			float posSec = max(0, min(diffTimeSec, _VatDuration));
			
		#endif
		//フレーム位置をとる(y)。
		float frameRateRaw = posSec / _VatDuration;

	#endif
	float maxRate = (_VatFps - 1) / float(_VatFps);
	frameRateRaw *= maxRate;//1のとき uvが(fps-1)/fpsになるよう
	float frameRateBefore = floor(frameRateRaw * _VatFps) / _VatFps;
	float frameRateAfter = min(frameRateBefore + _VatDeltaSec, maxRate);//最大割合時、0に戻ってしまうのを防ぐいらないとは思うけど。
	
	//前VATフレームと次VATフレームで、今の時間がどれだけ次VATフレームに寄っているか割合
	float afterRate = (frameRateRaw - frameRateBefore) / _VatDeltaSec;

	VatDiffInfo before = _GetFrameAttribute(vertUvX, frameRateBefore);
	VatDiffInfo after = _GetFrameAttribute(vertUvX, frameRateAfter);

	return _MixVatAttribute(before, after, afterRate);
}

/******************************** private function ***************************/

//指定時間でのフレームの差分情報を取得する
inline VatDiffInfo _GetFrameAttribute(float vertRate, float frameRate)
{
	VatDiffInfo o;
	
	//x,yからVAT位置情報取得
	float uvX = vertRate + _Dx;
	float uvY = (frameRate + _Dy) / 3;
	float4 uv = float4(uvX, uvY, 0, 0);
	
	//位置
	o.posDiff = tex2Dlod(_VatTex, uv) / VAT_SCALE;
	
	#ifdef VAT_USE_NORMAL
		//Normal
		uv.y += 0.33333333333;
		o.normalDiff = tex2Dlod(_VatTex, uv);
	#endif

	#ifdef VAT_USE_TANGENT
		//Tangent
		uv.y += 0.33333333333;
		o.tangentDiff = tex2Dlod(_VatTex, uv);
	#endif
	return o;
}

//VATの前後フレームを線形補完する
inline VatDiffInfo _MixVatAttribute(VatDiffInfo before, VatDiffInfo after, float afterRate)
{
	VatDiffInfo o;
	o.posDiff = lerp(before.posDiff, after.posDiff, afterRate);

	#ifdef VAT_USE_NORMAL
		o.normalDiff = lerp(before.normalDiff, after.normalDiff, afterRate);
	#endif

	#ifdef VAT_USE_TANGENT
		o.tangentDiff = lerp(after.tangentDiff, after.tangentDiff, afterRate);
	#endif
	return o;
}

#endif
