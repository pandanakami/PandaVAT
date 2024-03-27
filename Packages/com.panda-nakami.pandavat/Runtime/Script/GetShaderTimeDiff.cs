
using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Rendering;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace PandaScript.PandaVat
{

	/// <summary>
	/// シェーダーの_Time.yとスクリプトのTime.timeの差を取得する処理
	/// 
	/// VRC上だと_Time.yはワールド入ってからの時間で、Time.timeはVRC始まってからの時間
	/// CalculateDiff()にて呼び出し時のスクリプトの時間とシェーダーの時間の差を返す。
	/// </summary>
	[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
	public class GetShaderTimeDiff : UdonSharpBehaviour
	{
		[SerializeField] private Material _GetTimeMat;

		private RenderTexture _Rt;
		private Texture2D _Tex;
		private Rect _Rect = new Rect(0, 0, 1, 1);

		private Vector3 _Position;

		private float _Diff = 0;

		/// <summary>
		/// スクリプトとシェーダーの時間の差
		/// 予めCalculateDiffしておく必要あり
		/// </summary>
		public float Diff => _Diff;

		/**************************************************************************/

		/// <summary>
		/// Start
		/// </summary>
		private void Start()
		{
			_Rt = new RenderTexture(1, 1, 0, RenderTextureFormat.ARGBFloat, 0);
			_Rt.antiAliasing = 1;
			_Rt.Create();
			_Tex = new Texture2D(1, 1, TextureFormat.ARGB32, false, true);

			_Position = new Vector3(0, 0, 0);

		}

		/// <summary>
		/// 呼び出し時のスクリプトの時間とシェーダーの時間の差を返す
		/// </summary>
		/// <returns></returns>
		public float CalculateDiff()
		{

			VRCGraphics.Blit(null, _Rt, _GetTimeMat);
			_Tex.ReadPixels(_Rect, 0, 0);
			_Tex.Apply();

			var color = _Tex.GetPixels32()[0];

			//色デコード RGBは整数部、Aは小数部
			//memo: BitConverter.ToSingle()が使えるようになれば独自形式をやめる。
			uint uintVal = (((uint)color.r << 16) | ((uint)color.g << 8) | ((uint)color.b << 0));
			float shaderVal = uintVal + (color.a / 255.0f);

			//スクリプト側の現在時間との差を取得
			_Diff = Time.time - shaderVal;
			_Position.y = _Diff;

			//UdonでDiff呼ぶよりtransform.position.yを取った方が速いかもしれない。お試し。
			transform.position = _Position;

			Debug.Log($"aa:{_Diff}");

			return _Diff;
		}
	}
}
