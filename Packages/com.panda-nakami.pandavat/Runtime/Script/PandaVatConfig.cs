using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PandaScript.PandaVat
{
	[RequireComponent(typeof(Animator))]
	public class PandaVatConfig : MonoBehaviour
	{
		/// <summary>
		/// 保存場所
		/// </summary>
		public string _savePos;

		/// <summary>
		/// Asset既にある場合上書き
		/// </summary>
		public bool _isOverwriteAsset = true;

		/// <summary>
		/// GameObject出力先
		/// </summary>
		public Transform _outputTransform = null;

		/// <summary>
		/// GameObject既にある場合上書き
		/// </summary>
		public bool _isOverwriteGameobject = true;

		/// <summary>
		/// FPS
		/// </summary>
		public int _animFps = 30;

		/// <summary>
		/// AnimationClip
		/// </summary>
		public AnimationClip _animClip;

		/// <summary>
		/// VAT対象レンダラーたち
		/// </summary>
		public List<Renderer> _targetRenderers = new List<Renderer>();

		/// <summary>
		/// VATシェーダー
		/// </summary>
		public Shader _targetShader;
	}
}