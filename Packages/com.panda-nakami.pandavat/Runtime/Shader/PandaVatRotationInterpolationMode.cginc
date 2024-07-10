#ifndef _PANDA_VAT_ROT_COMP_H_
#define _PANDA_VAT_ROT_COMP_H_

/******************************** type define ***************************/

struct VatBoneInfo
{
	float3 pos;
	float4 rotation;
	float3 scale;
};

/******************************** macro define ***************************/

/******************************** static variable ***************************/
static float _VatFrameCount = (_VatTex_TexelSize.w / 3) - 1; //テクスチャが持つ情報：フレーム数
static float _VatDeltaFrameRate = (1.0 / _VatFrameCount);//フレーム数の逆数
static float _VatDuration = (_VatFrameCount / _VatFps);//アニメーションの総時間[秒]

/******************************** prototype declaration ***************************/
//指定時間でのフレームの差分情報を取得する
inline VatBoneInfo _GetFrameAttribute(float vertRate, float frameRate);

//VATの前後フレームの差分情報を合成する
inline VatBoneInfo _MixVatAttribute(VatBoneInfo before, VatBoneInfo after, float frameRateAfterRate);

//Quaternionから回転行列に変換する
inline float3x3 _QuaternionToRotationMatrix(float4 q);

//テクスチャから取得した座標系からローカル座標に変換するための行列を取得する
inline float4x4 _GenerateLocalMatrix(VatBoneInfo boneInfo);

//回転角の補間用
inline float4 slerp(float4 q1, float4 q2, float t);

/******************************** public function ***************************/
// VATの情報を取得
inline void ApplyVatInfoRI(uint4 boneIndeces, float4 boneWeights,
inout float4 vertex
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

	//uvx取得:今はBone0のみ
	_GetXRate(boneIndeces[0], uvX);
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

	//前・次VATフレームのボーン情報を取得
	VatBoneInfo before = _GetFrameAttribute(uvX, frameRateBefore);
	VatBoneInfo after = _GetFrameAttribute(uvX, frameRateAfter);

	//前・次VATフレーム情報を補間する
	VatBoneInfo mixInfo = _MixVatAttribute(before, after, mixRate);

	float4x4 mat = _GenerateLocalMatrix(mixInfo);

	//行列計算してローカル座標取得
	vertex = mul(mat, vertex);

	#ifdef VAT_USE_NORMAL
		normal = mul((float3x3)mat, normal);
	#endif

	#ifdef VAT_USE_TANGENT
		tangent = float4(mul((float3x3)mat, tangent.xyz), baseTangent.w);
	#endif
}

/******************************** private function ***************************/

//指定時間でのフレームの差分情報を取得する
inline VatBoneInfo _GetFrameAttribute(float vertRate, float frameRate)
{
	VatBoneInfo o;
	
	///x,yからVAT位置情報取得
	float uvX = vertRate + _Dx;
	//フレームレート→テクセル高さ割合に変換
	//フレーム数Fに対し、テクセル高で割る
	float uvY = (frameRate * _VatFrameCount / _TexelHeight) + _Dy;
	float4 uv = float4(uvX, uvY, 0, 0);
	
	const float dy = 0.33333333333;

	//位置
	o.pos = tex2Dlod(_VatTex, uv).xyz;
	
	//回転
	uv.y += dy;
	o.rotation = tex2Dlod(_VatTex, uv);

	//サイズ
	uv.y += dy;
	o.scale = tex2Dlod(_VatTex, uv).xyz;

	return o;
}
#if VAT_OBJECT_ON_OFF_ENABLE
	static const VatBoneInfo ZERO_INFO = {
		float3(0, 0, 0),
		float4(1, 0, 0, 0),
		float3(0, 0, 0)
	};
#endif
//VATの前後フレームを線形補間する
inline VatBoneInfo _MixVatAttribute(VatBoneInfo before, VatBoneInfo after, float mixRate)
{
	VatBoneInfo o;
	o.pos = lerp(before.pos, after.pos, mixRate);
	o.rotation = slerp(before.rotation, after.rotation, mixRate);
	o.scale = lerp(before.scale, after.scale, mixRate);

	//Object ON/OFFアニメーション有効
	#if VAT_OBJECT_ON_OFF_ENABLE
		//次フレームがOFFであれば、前フレームの情報を採用
		float isAfterZero = IS_SIZE_ZERO(after.scale) ? 1 : 0;
		o.pos = lerp(o.pos, before.pos, isAfterZero);
		o.rotation = lerp(o.rotation, before.rotation, isAfterZero);
		o.scale = lerp(o.scale, before.scale, isAfterZero);

		//前フレームがOFFであれば、サイズ0(位置0)
		float isBeforeZero = IS_SIZE_ZERO(before.scale) ? 1 : 0;
		o.pos = lerp(o.pos, ZERO_INFO.pos, isBeforeZero);
		o.rotation = lerp(o.rotation, ZERO_INFO.rotation, isBeforeZero);
		o.scale = lerp(o.scale, ZERO_INFO.scale, isBeforeZero);

	#endif

	return o;
}

//テクスチャから取得した座標系からローカル座標に変換するための行列を取得する
inline float4x4 _GenerateLocalMatrix(VatBoneInfo boneInfo)
{
	float3x3 rotationMatrix = _QuaternionToRotationMatrix(boneInfo.rotation);
	float3x3 scale = float3x3(
		boneInfo.scale.x, 0, 0,
		0, boneInfo.scale.y, 0,
		0, 0, boneInfo.scale.z
	);
	// スケーリングと回転を組み合わせた行列を作成
	float3x3 tmp = mul(rotationMatrix, scale);

	return float4x4(
		tmp[0], boneInfo.pos.x,
		tmp[1], boneInfo.pos.y,
		tmp[2], boneInfo.pos.z,
		0, 0, 0, 1
	);
}

//Quaternionから回転行列に変換する
inline float3x3 _QuaternionToRotationMatrix(float4 q)
{
	// クオータニオンの成分を取得
	float w = q.w;
	float x = q.x;
	float y = q.y;
	float z = q.z;

	// 回転行列の成分を計算
	float m00 = 1 - 2 * y * y - 2 * z * z;
	float m01 = 2 * x * y - 2 * w * z;
	float m02 = 2 * x * z + 2 * w * y;

	float m10 = 2 * x * y + 2 * w * z;
	float m11 = 1 - 2 * x * x - 2 * z * z;
	float m12 = 2 * y * z - 2 * w * x;

	float m20 = 2 * x * z - 2 * w * y;
	float m21 = 2 * y * z + 2 * w * x;
	float m22 = 1 - 2 * x * x - 2 * y * y;

	// 3x3回転行列を作成
	return float3x3(
		m00, m01, m02,
		m10, m11, m12,
		m20, m21, m22
	);
}

//回転角の補間用
inline float4 slerp(float4 q1, float4 q2, float t)
{
	float _dot = dot(q1, q2);

	// 逆方向の場合、方向を反転
	float revVal = sign(_dot);
	q2 *= revVal;
	_dot *= revVal;

	bool isLittleAngle = _dot > 0.9995;

	// 角度 theta を計算
	float theta_0 = acos(_dot);
	float theta = theta_0 * t;
	float sin_theta = sin(theta);
	float sin_theta_0 = sin(theta_0);

	float s0 = cos(theta) - _dot * sin_theta / sin_theta_0;
	float s1 = sin_theta / sin_theta_0;

	return isLittleAngle ? lerp(q1, q2, t) : (s0 * q1) + (s1 * q2);
}

//shader_featureでOFFの場合のコンパイル通る用
#define ApplyVatInfo
#endif
