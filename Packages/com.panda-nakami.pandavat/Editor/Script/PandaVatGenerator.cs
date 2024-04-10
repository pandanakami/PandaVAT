
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PandaScript.PandaVat
{

	public class PandaVatGenerator : EditorWindow
	{		
		/// <summary>
		/// 最大頂点数
		/// (動くなら変えていいよ。頂点数がテクスチャの横幅だよ。)
		/// </summary>
		private const int MAX_VERTEX_NUM = 8192;

		/// <summary>
		/// 最大フレーム数
		/// アニメクリップのduration * 本UIで指定したFPS
		/// (動くなら変えていいよ。フレーム数*3(場合によってさらに+3)がテクスチャの高さだよ。)
		/// </summary>
		private const int MAX_FRAME_NUM = (8192-1)/3;

		private const string DEFAULT_SAVE_POS = "Assets";

		/****************** GUI input *********************/

		private string _savePos;
		private string _rootFullPath => Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/") + "/";

		/// <summary>
		/// Animator
		/// </summary>
		private Animator _rootAnim;
		/// <summary>
		/// AnimationClip
		/// </summary>
		private AnimationClip _animClip;
		/// <summary>
		/// VAT対象レンダラー
		/// </summary>
		private Renderer _targetRenderer;
		/// <summary>
		/// VATシェーダー
		/// </summary>
		private Shader _targetShader;
		/// <summary>
		/// 出力FPS
		/// </summary>
		private int _animFps = 30;

		/// <summary>
		/// GameObject出力先
		/// </summary>
		private Transform _outputTransform = null;
		/// <summary>
		/// GameObject既にある場合上書き
		/// </summary>
		private bool _isOverwriteGameobject = true;
		/// <summary>
		/// Asset既にある場合上書き
		/// </summary>
		private bool _isOverwriteAsset = true;

		/******************* field ********************/

		/// <summary>
		/// AnimatorのGameObject
		/// </summary>
		private GameObject _rootObj;
		/// <summary>
		/// メッシュ
		/// </summary>
		private Mesh _baseMesh;

		/// <summary>
		/// Animator下にあるRenderer一覧
		/// </summary>
		private Renderer[] _renderers;
		/// <summary>
		/// Renderer一覧表示フラグ
		/// </summary>
		private bool _showMeshRenderers = false;
		/// <summary>
		/// Animatorが持つClip一覧
		/// </summary>
		private AnimationClip[] _animationClips;
		/// <summary>
		/// Clip一覧表示フラグ
		/// </summary>
		private bool _showAnimationClips = false;

		/// <summary>
		/// モード設定(通常：false, 回転補間モード : true)
		/// </summary>
		private bool _RotationInterpolationMode = false;
		/// <summary>
		/// 画面スクロールの情報
		/// </summary>
		private Vector2 scrollPosition;

		/******************* field Error情報 ********************/

		/// <summary>
		/// エラー情報：レンダラ―関係
		/// </summary>
		private bool _hasRendererError = false;
		/// <summary>
		/// エラー情報：アニメーション関係
		/// </summary>
		private bool _hasAnimationError = false;
		/// <summary>
		/// エラー情報：モード関係
		/// </summary>
		private bool _hasModeError = false;
		/// <summary>
		/// エラー情報：エラーメッセージ
		/// </summary>
		private string _ErrorCheckResult = null;


		/******************* method ********************/

		[MenuItem("ぱんだスクリプト/VatGenerator")]
		static void ShowWindow()
		{
			var window = GetWindow<PandaVatGenerator>("PandaVatGenerator");
			window.minSize = new Vector2(300, 300);
		}

		/// <summary>
		/// GUIメイン
		/// </summary>
		private void OnGUI()
		{
			// 縦方向のみスクロール可能なスクロールビューを開始
			scrollPosition = GUILayout.BeginScrollView(scrollPosition, false, true);

			DrawSeparator();

			if (_savePos == null) {
				_savePos = DEFAULT_SAVE_POS;
			}
			GUILayout.BeginHorizontal();
			GUILayout.Label("Asset保存場所", GUILayout.Width(148));
			_savePos = GUILayout.TextField(_savePos);
			if (GUILayout.Button("選択", GUILayout.Width(40))) {
				string rootFullPath = _rootFullPath;

				string path = EditorUtility.OpenFolderPanel("Select a folder", rootFullPath + _savePos, "");
				if (!string.IsNullOrEmpty(path)) {
					if (path.StartsWith(rootFullPath + DEFAULT_SAVE_POS)) {
						_savePos = path.Replace(rootFullPath, "");//Assets始まりにする
					}
					else {
						Debug.LogWarning("Invalid save pos. Please specify Assets Directory.");
					}
				}
			}
			GUILayout.EndHorizontal();
			_isOverwriteAsset= EditorGUILayout.Toggle("Asset既にあったら上書き", _isOverwriteAsset);

			DrawSeparator();

			_outputTransform = (Transform)EditorGUILayout.ObjectField("オブジェクト生成位置", _outputTransform, typeof(Transform), true);
			_isOverwriteGameobject = EditorGUILayout.Toggle("オブジェクト既にあったら上書き", _isOverwriteGameobject);
			DrawSeparator();

			//FPS
			EditorGUI.BeginChangeCheck();
			_animFps = EditorGUILayout.IntField("VAT Fps", _animFps);
			if (EditorGUI.EndChangeCheck()) {
				_CheckAnimClipEnable();
			}
			DrawSeparator();
			//RendererとAnimationClip
			_DispAnimatorAndRendererSelector();
			DrawSeparator();

			//Shader
			EditorGUI.BeginChangeCheck();
			_targetShader = (Shader)EditorGUILayout.ObjectField("VAT Shader", _targetShader, typeof(Shader), true);
			if (EditorGUI.EndChangeCheck()) {
				_CheckMode();
			}
			DrawSeparator();

			// スクロールビューの終了
			GUILayout.EndScrollView();

			var style = new GUIStyle();
			var txt = "";
			//入力エラーチェック
			var generateEnable = false;
			{
				if (!_animClip) {
					style.normal.textColor = Color.red;
					txt = "[Animator未設定]";
				}
				else if (!_animClip) {
					style.normal.textColor = Color.red;
					txt = "[AnimationClipなし]";
				}
				else if (!_targetRenderer) {
					style.normal.textColor = Color.red;
					txt = "[Rendererなし]";
				}
				else if (!_targetShader) {
					style.normal.textColor = Color.red;
					txt = "[Shader未設定]";
				}
				else if (_hasRendererError || _hasAnimationError || _hasModeError) {
					style.normal.textColor = Color.red;
					txt = _ErrorCheckResult;
				}
				else {
					generateEnable = true;
				}
			}

			if (generateEnable) {
				if (_RotationInterpolationMode) {
					style.normal.textColor = Color.green;
					if (_targetRenderer is SkinnedMeshRenderer) {
						txt = "[回転補間モード(SkinnedMeshRenderer)";
					}
					else if (_targetRenderer is MeshRenderer) {
						txt = "[回転補間モード(MeshRenderer)";
					}
				}
				else {
					style.normal.textColor = Color.cyan;
					if (_targetRenderer is SkinnedMeshRenderer) {
						txt = "[通常モード(SkinnedMeshRenderer)";
					}
					else if (_targetRenderer is MeshRenderer) {
						txt = "[通常モード(MeshRenderer)";
					}
				}
			}

			//モード表示 or エラー表示
			EditorGUILayout.LabelField(txt, style);

			GUI.enabled = generateEnable;

			//生成
			if (GUILayout.Button("Generate", GUILayout.Height(40))) {
				_Generate();
			}

			GUI.enabled = true;
		}

		/// <summary>
		/// 外部からの生成
		/// </summary>
		/// <param name="savePos"></param>
		/// <param name="createObjPos"></param>
		/// <param name="fps"></param>
		/// <param name="rootAnim"></param>
		/// <param name="targetRenderer"></param>
		/// <param name="clip"></param>
		/// <param name="shader"></param>
		/// <param name="isOverwriteAsset"></param>
		/// <param name="isOverwriteGameObject"></param>
		/// <exception cref="Exception"></exception>
		public void GenerateVatManual(
			string savePos, 
			Transform createObjPos, 
			int fps, 
			Animator rootAnim, 
			Renderer targetRenderer, 
			AnimationClip clip, 
			Shader shader,  
			bool isOverwriteAsset = true, 
			bool isOverwriteGameObject = true)
		{
			_savePos = savePos;
			_outputTransform = createObjPos;
			_isOverwriteAsset = isOverwriteAsset;
			_isOverwriteGameobject = isOverwriteGameObject;
			_animFps = fps;
			_rootAnim = rootAnim;

			_DispAnimatorAndRendererSelector(true);

			if (_renderers.Contains(targetRenderer)) {
				_targetRenderer = targetRenderer;
			}
			else {
				throw new Exception("RendererはAnimator配下から指定してください");
			}
			if (_animationClips.Contains(clip)) {
				_animClip = clip;
			}
			else {
				throw new Exception("AnimationClipはAnimatorが持つものから指定してください");
			}
			
			_targetShader = shader;

			_Generate();
		}

		/// <summary>
		/// 生成
		/// </summary>
		/// <exception cref="Exception"></exception>
		private void _Generate()
		{
			_savePos = CreateFoldersRecursivelyIfNotExist(_savePos);

			//セット後の変更エラーチェック
			_CheckRendererEnable();
			_CheckAnimClipEnable();
			_CheckMode();
			if (_hasRendererError || _hasAnimationError || _hasModeError) {
				throw new Exception(_ErrorCheckResult);
			}

			_baseMesh = null;
			_rootObj = _rootAnim.gameObject;

			var isSkinnedMeshRenderer = true;

			if (_targetRenderer is SkinnedMeshRenderer) {
				_baseMesh = ((SkinnedMeshRenderer)_targetRenderer).sharedMesh;
			}
			else if (_targetRenderer is MeshRenderer) {
				isSkinnedMeshRenderer = false;
				_baseMesh = _targetRenderer.GetComponent<MeshFilter>()?.sharedMesh;
			}

			if (!_baseMesh) {
				Debug.LogError("Error. Mesh is not set.");
				return;
			}
			if (_baseMesh.vertexCount > 8192) {
				Debug.LogError("Not supported. Mesh with up to 8192 vertices is supported.");
				return;
			}

			_CreateVat(isSkinnedMeshRenderer);
			Debug.Log($"Generate VAT Finish!!!");
		}

		/// <summary>
		/// アニメーターの指定とレンダラーとアニメーションクリップ選択
		/// </summary>
		private void _DispAnimatorAndRendererSelector(bool isManual = false)
		{
			EditorGUI.BeginChangeCheck();
			_rootAnim = (Animator)EditorGUILayout.ObjectField("対象のAnimator", _rootAnim, typeof(Animator), true);
			if (EditorGUI.EndChangeCheck() || isManual) {
				if (_rootAnim) {
					_renderers = _rootAnim.GetComponentsInChildren<Renderer>();
					if (_renderers.Length == 0 || !_renderers.Any(o=>o is SkinnedMeshRenderer || o is MeshRenderer)) {
						_renderers = null;
						_targetRenderer = null;
					}
					else {
						_targetRenderer = _renderers[0];
						_showMeshRenderers = true;
						_CheckRendererEnable();

					}

					var controller = _rootAnim.runtimeAnimatorController; // RuntimeAnimatorControllerを取得
					_animationClips = controller.animationClips.Distinct().ToArray();
					if(_animationClips.Length == 0) {
						_animationClips = null;
						_animClip = null;
					}
					else {
						_animClip = _animationClips[0];
						_showAnimationClips= true;
						_CheckAnimClipEnable();
					}


				}
				else {
					_renderers = null;
					_targetRenderer = null;

					_animationClips = null;
					_animClip = null;
				}

			}

			if (_renderers != null) {
				DrawSeparator();
				if (_targetRenderer != null) {
					GUI.enabled = false;
					EditorGUILayout.ObjectField("VAT対象のRenderer", _targetRenderer, typeof(MeshRenderer), true);
					GUI.enabled = true;
				}

				_showMeshRenderers = EditorGUILayout.Foldout(_showMeshRenderers, "Renderer一覧");

				if (_showMeshRenderers) {
					foreach (var renderer in _renderers) {
						if(renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) {
							continue;
						}

						EditorGUILayout.BeginHorizontal();
						if (EditorGUILayout.Toggle(renderer == _targetRenderer)) {
							var changeFlag = _targetRenderer != renderer;
							_targetRenderer = renderer;
							if (changeFlag) {
								_CheckRendererEnable();
							}
						}
						GUI.enabled = false;
						EditorGUILayout.ObjectField(renderer, typeof(MeshRenderer), true);
						GUI.enabled = true;
						EditorGUILayout.EndHorizontal();
					}
				}
			}


			if (_animationClips != null) {

				DrawSeparator();
				if (_animClip != null) {
					GUI.enabled = false;
					EditorGUILayout.ObjectField("対象のAnimationClip", _animClip, typeof(AnimationClip), true);
					GUI.enabled = true;
				}

				_showAnimationClips = EditorGUILayout.Foldout(_showAnimationClips, "AnimClip一覧");

				if (_showAnimationClips) {
					foreach (var animClip in _animationClips) {
						EditorGUILayout.BeginHorizontal();
						if (EditorGUILayout.Toggle(animClip == _animClip)) {
							var changeFlag = _animClip != animClip;
							_animClip = animClip;
							if (changeFlag) {
								_CheckAnimClipEnable();
							}
						}
						GUI.enabled = false;
						EditorGUILayout.ObjectField(animClip, typeof(MeshRenderer), true);
						GUI.enabled = true;
						EditorGUILayout.EndHorizontal();
					}
				}
			}
		}

		/// <summary>
		/// 選択したレンダラーがVAT化OKか調べる
		/// ダメならエラー情報入ってGUIメインでエラー処理
		/// </summary>
		private void _CheckRendererEnable()
		{
			if (!_targetRenderer) {
				_ErrorCheckResult = "[Rendererなし]";
				_hasRendererError = true;
				return;
			}

			bool isSkinnedMeshRenderer = false;
			if (_targetRenderer is SkinnedMeshRenderer) {
				isSkinnedMeshRenderer = true;
				_baseMesh = ((SkinnedMeshRenderer)_targetRenderer).sharedMesh;
			}
			else if (_targetRenderer is MeshRenderer) {
				_baseMesh = _targetRenderer.GetComponent<MeshFilter>()?.sharedMesh;
			}
			else {
				_ErrorCheckResult = "[非対応のRenderer]";
				_hasRendererError = true;
				return;
			}

			if (!_baseMesh) {
				if (isSkinnedMeshRenderer) {
					_ErrorCheckResult = "[Rendererにメッシュ未セット]";
					_hasRendererError = true;
				}
				else {
					_ErrorCheckResult = "[MeshFilterにメッシュ未セット]";
					_hasRendererError = true;
				}
			}
			else if (_baseMesh.vertexCount > MAX_VERTEX_NUM) {
				_ErrorCheckResult = $"[頂点数が({MAX_VERTEX_NUM})を超えています]";
				_hasRendererError = true;
			}
			else {
				//エラーなし
				_ErrorCheckResult = null;
				_hasRendererError = false;
			}
		}

		/// <summary>
		/// 選択したアニメーションクリップがVAT化OKか調べる
		/// </summary>
		private void _CheckAnimClipEnable()
		{
			//Rendererエラーを優先
			if (_hasRendererError) {
				return;
			}
			
			if (!_animClip) {
				_ErrorCheckResult = "[AnimationClipなし]";
				_hasAnimationError = true;
				return;
			}
			if (!(1 <= _animFps && _animFps <= 144)) {
				_ErrorCheckResult = "[FPS範囲外(1~144まで)]";
				_hasAnimationError = true;
				return;
			}

			var duration = _animClip.length;  //アニメーション時間　秒
			var frameCount = Mathf.Max((int)(duration * _animFps + 1), 1);//アニメーションフレーム数

			if(frameCount > MAX_FRAME_NUM) {
				_ErrorCheckResult = $"[Frame数(Fps加味)が{MAX_FRAME_NUM}を超えています]";
				_hasAnimationError = true;
			}
			else {
				_hasAnimationError = false;
				_ErrorCheckResult = null;
			}
		}

		/// <summary>
		/// モードのチェック
		/// </summary>
		private void _CheckMode()
		{
			//Renderer,Animエラーを優先
			if (_hasRendererError || _hasAnimationError) {
				return;
			}

			if (_targetShader) {

				if(_targetShader.FindPropertyIndex("_PandaVat") == -1) {
					_ErrorCheckResult = "[セットされているシェーダーはVAT対象外のシェーダーです]";
					_hasModeError = true;
					return;
				}

				var shaderIsRotationInterpolationMode = _targetShader.FindPropertyIndex("_RotationInterpolationMode") != -1;

				_RotationInterpolationMode = shaderIsRotationInterpolationMode;
			}

			_hasModeError = false;
			_ErrorCheckResult = null;

		}
		/************************************* VAT生成 ************************************************************/


		/// <summary>
		/// VAT生成
		/// </summary>
		/// <param name="isSkinedMeshRenderer"></param>
		private void _CreateVat(bool isSkinedMeshRenderer)
		{
			var rendererName = _targetRenderer.name;

			var vertexCount = _baseMesh.vertexCount;    //頂点数
			var duration = _animClip.length;  //アニメーション時間　秒
			var frameCount = Mathf.Max((int)(duration * _animFps + 1), 1);//アニメーションフレーム数

			//テクスチャ用意
			var frameCount_ = frameCount * 3;
			var texture = new Texture2D(vertexCount, frameCount_, TextureFormat.RGBAHalf, false, false);
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Point;


			var rootT = _rootObj.transform;
			var renderT = _targetRenderer.transform;

			//既にアニメーションモード中の場合解除する
			AnimationMode.StopAnimationMode();
			CreateTmpTransform();

			//デフォルト情報保持
			var defaultVertices = _baseMesh.vertices;
			var defaultNormals = _baseMesh.normals;
			var defaultTangents = _baseMesh.tangents;

			//回転補正モード
			if (_RotationInterpolationMode) {
				_GenerateVATRotationInterpolationMode(rootT, renderT, texture, vertexCount, frameCount, duration, isSkinedMeshRenderer,
					ref defaultVertices, ref defaultNormals, ref defaultTangents);
			}
			//通常モード
			else {

				//Renderのカスタム座標系を作る
				var customRenderT = CreateCustomTransform(renderT, rootT.parent);
				for (var i = 0; i < vertexCount; i++) {
					defaultVertices[i] = customRenderT.TransformPoint(defaultVertices[i]);
					defaultNormals[i] = customRenderT.TransformVector(defaultNormals[i]);
					var tanXyz = customRenderT.TransformVector(defaultTangents[i]);
					defaultTangents[i].x = tanXyz.x;
					defaultTangents[i].y = tanXyz.y;
					defaultTangents[i].z = tanXyz.z;
				}
				DestroyImmediate(customRenderT.gameObject);

				_GenerateVABasic(rootT, renderT, texture, vertexCount, frameCount, duration, 
					isSkinedMeshRenderer, defaultVertices, defaultNormals, defaultTangents);
			}

			texture.Apply();

			//各種保存
			_SaveAsset(texture, rendererName, defaultVertices, defaultNormals, defaultTangents);


			DestroyTmpTransform();
		}

		/// <summary>
		/// VAT生成(通常用)
		/// </summary>
		/// <param name="rootT">AnimatorのルートTransform</param>
		/// <param name="renderT">RendererのTransform</param>
		/// <param name="texture">書き込み対象テクスチャ</param>
		/// <param name="vertexCount">頂点の数</param>
		/// <param name="frameCount">フレーム数</param>
		/// <param name="duration">アニメーションの長さ(秒)</param>
		/// <param name="isSkinedMeshRenderer">レンダラーの種類</param>
		/// <param name="defaultVertices">デフォルトの頂点一覧</param>
		/// <param name="defaultNormals">デフォルトの法線一覧</param>
		/// <param name="defaultTangents">デフォルトの接線一覧</param>
		private void _GenerateVABasic(Transform rootT, Transform renderT, Texture2D texture, int vertexCount, int frameCount, float duration, 
			bool isSkinedMeshRenderer, Vector3[] defaultVertices, Vector3[] defaultNormals, Vector4[] defaultTangents)
		{
			var targetSkinnedMeshRenderer = _targetRenderer as SkinnedMeshRenderer;

			// エディタモードでのアニメーション制御を有効にする
			AnimationMode.StartAnimationMode();

			//テクスチャに格納
			Mesh tmpMesh = new Mesh();
			for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {

				AnimationMode.SampleAnimationClip(_rootObj, _animClip, ((float)frameIndex / (frameCount - 1)) * duration);

				if (isSkinedMeshRenderer) {
					targetSkinnedMeshRenderer.BakeMesh(tmpMesh, true);// useScale=trueだとルートのスケールが入らない
				}
				else {
					tmpMesh = _baseMesh;
				}

				Vector3[] vertices = tmpMesh.vertices;
				Vector3[] normals = tmpMesh.normals;
				Vector4[] tangents = tmpMesh.tangents;

				var customRendererT = CreateCustomTransform(renderT, rootT.parent);
				
				for (int vertIndex = 0; vertIndex < vertexCount; vertIndex++) {
					
					Vector3 positionDiff = customRendererT.TransformPoint(vertices[vertIndex]) - defaultVertices[vertIndex];
					Vector3 normalDiff = customRendererT.TransformDirection(normals[vertIndex]) - defaultNormals[vertIndex];
					var tangent = customRendererT.TransformDirection(tangents[vertIndex]);
					var tangentDiff = new Vector4(tangent.x, tangent.y, tangent.z, tangents[vertIndex].w) - defaultTangents[vertIndex];

					texture.SetPixel(vertIndex, frameIndex, GetColor(positionDiff));
					texture.SetPixel(vertIndex, frameIndex + frameCount, GetColor(normalDiff));
					texture.SetPixel(vertIndex, frameIndex + frameCount * 2, GetColor(tangentDiff));
				}

				DestroyImmediate(customRendererT.gameObject);
			}

			AnimationMode.StopAnimationMode();
		}

		/// <summary>
		/// VAT生成(回転補間用)
		/// </summary>
		/// <param name="rootT">AnimatorのルートTransform</param>
		/// <param name="renderT">RendererのTransform</param>
		/// <param name="texture">書き込み対象テクスチャ</param>
		/// <param name="vertexCount">頂点の数</param>
		/// <param name="frameCount">フレーム数</param>
		/// <param name="duration">アニメーションの長さ(秒)</param>
		/// <param name="isSkinnedMeshRendere">SkinnedMeshrendererか否か</param>
		private void _GenerateVATRotationInterpolationMode(Transform rootT, Transform renderT, Texture2D texture, int vertexCount, 
			int frameCount, float duration, bool isSkinnedMeshRendere,
			ref Vector3[] updateDefaultVerteces, ref Vector3[] updateDefaultNormals, ref Vector4[] updateDefaultTangents)
		{
			var targetSkinnedMeshRenderer = _targetRenderer as SkinnedMeshRenderer;

			Transform[] customBones;
			//デフォルトメッシュに対して。
			//各頂点に対して
			//属するボーンを探す。ボーン複数NG
			//ボーンと頂点の位置の差を取得
			//最終行に書く

			{
				BoneWeight[] boneWeights = _baseMesh.boneWeights;
				Transform[] bones = isSkinnedMeshRendere ? targetSkinnedMeshRenderer.bones : new Transform[1] { renderT };
				var defaultDiffPos = _baseMesh.vertices;
				var defaultDiffNormals = _baseMesh.normals;
				var defaultDiffTangents= _baseMesh.tangents;

				//ルートとの差を持つトランスフォーム
				customBones = new Transform[bones.Length];

				//デフォルトボーンをカスタム座標系にする
				for(var i = 0; i < bones.Length; i++) {
					customBones[i] = CreateCustomTransform(bones[i], rootT.parent);					
				}

				//レンダラーをカスタム座標系にする
				var customRendererT = CreateCustomTransform(renderT, rootT.parent);
				
				//カスタム座標系でのメッシュの頂点座標を取得
				for (var vertIndex = 0; vertIndex < vertexCount; vertIndex++) {
					defaultDiffPos[vertIndex] = customRendererT.TransformPoint(defaultDiffPos[vertIndex]);
					defaultDiffNormals[vertIndex] = customRendererT.TransformDirection(defaultDiffNormals[vertIndex]);
					defaultDiffTangents[vertIndex] = customRendererT.TransformDirection(defaultDiffTangents[vertIndex]);
				}
				DestroyImmediate(customRendererT.gameObject);

				//カスタム座標系で、メッシュの頂点座標からデフォルトボーンのトランスフォームを除いた座標をデフォルト座標として登録
				//VATでは各フレームでの移動したボーン情報を持ち、このデフォルト座標にそのボーントランスフォームをセットすることでアニメーション後の座標を取得できる
				for (var vertIndex = 0; vertIndex < vertexCount; vertIndex++) {
				
					//bone0が100%前提。
					var boneIndex = isSkinnedMeshRendere ?  boneWeights[vertIndex].boneIndex0 : 0;

					updateDefaultVerteces[vertIndex] = customBones[boneIndex].InverseTransformPoint(defaultDiffPos[vertIndex]);

					//normal
					updateDefaultNormals[vertIndex] = customBones[boneIndex].InverseTransformDirection(defaultDiffNormals[vertIndex]);
			
					//tangent
					float w = defaultDiffTangents[vertIndex].w;
					updateDefaultTangents[vertIndex] = customBones[boneIndex].InverseTransformDirection(defaultDiffTangents[vertIndex]);
					updateDefaultTangents[vertIndex].w = w;
				}
			}

			// エディタモードでのアニメーション制御を有効にする
			AnimationMode.StartAnimationMode();

			//各フレームで
			//各頂点に対して
			//属するボーンのスケール・回転・平行移動を取得
			//テクスチャに格納
			for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {

				//アニメーション位置をシミュレート
				AnimationMode.SampleAnimationClip(_rootObj, _animClip, ((float)frameIndex / (frameCount - 1)) * duration);

				Transform[] bones = isSkinnedMeshRendere ? targetSkinnedMeshRenderer.bones : new Transform[1] { renderT };
				Mesh mesh = isSkinnedMeshRendere ? targetSkinnedMeshRenderer.sharedMesh : _baseMesh;
				BoneWeight[] boneWeights = mesh.boneWeights;


				//現フレームのボーンをカスタム座標系にする
				for (var i = 0; i < bones.Length; i++) {
					customBones[i] = CreateCustomTransform(bones[i], rootT.parent, customBones[i]);
				}

				for (int vertIndex = 0; vertIndex < vertexCount; vertIndex++) {

					//bone0が100%前提。
					var boneIndex = isSkinnedMeshRendere ? boneWeights[vertIndex].boneIndex0 : 0;
					var boneT = customBones[boneIndex];

					var position = boneT.localPosition;
					var scale = boneT.localScale;
					var rotation = boneT.localRotation;

					texture.SetPixel(vertIndex, frameIndex, GetColor(position));
					texture.SetPixel(vertIndex, frameIndex + frameCount, GetColor(rotation));
					texture.SetPixel(vertIndex, frameIndex + frameCount * 2, GetColor(scale));
				}
			}

			for (var i = 0; i < customBones.Length; i++) {
				DestroyImmediate(customBones[i].gameObject);
			}

			AnimationMode.StopAnimationMode();
		}

		/// <summary>
		/// アセット保存
		/// </summary>
		/// <param name="texture"></param>
		/// <param name="rendererName"></param>
		/// <param name="vertices"></param>
		/// <param name="normals"></param>
		/// <param name="tangents"></param>
		private void _SaveAsset(Texture2D texture, string rendererName, Vector3[] vertices, Vector3[] normals, Vector4[] tangents)
		{

			string dirName = _savePos;

			var data = ImageConversion.EncodeToEXR(texture, Texture2D.EXRFlags.None);
			Object.DestroyImmediate(texture);
			var texPath = $"{dirName}/{rendererName}_{_animClip.name}.exr";
			if (!_isOverwriteAsset) {
				texPath = AssetDatabase.GenerateUniqueAssetPath(texPath);
			}
			File.WriteAllBytes(texPath, data);
			AssetDatabase.Refresh();
			Debug.Log($"Create texture : {texPath}");

			//画像設定
			{
				TextureImporter textureImporter = AssetImporter.GetAtPath(texPath) as TextureImporter;
				textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
				textureImporter.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
					overridden = true,
					format = TextureImporterFormat.RGBAHalf
				});
				textureImporter.npotScale = TextureImporterNPOTScale.None;
				textureImporter.filterMode = FilterMode.Point;
				textureImporter.wrapMode = TextureWrapMode.Repeat;
				textureImporter.mipmapEnabled = false;
				AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
			}

			//メッシュセット
			Mesh newMesh;
			{
				var meshPath = $"{dirName}/{_baseMesh.name}_vat.asset";

				var oldMeshAsset = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
				var isCreate = false;
				//上書き
				if (_isOverwriteAsset) {
					//既にある
					if (oldMeshAsset) {
						newMesh = oldMeshAsset;
						newMesh.Clear();
					}
					else {
						newMesh = Instantiate(_baseMesh);
						isCreate = true;
					}
				}
				//上書きしない
				else {
					newMesh = Instantiate(_baseMesh);
					meshPath = AssetDatabase.GenerateUniqueAssetPath(meshPath);
					isCreate = true;
				}
				
				newMesh.vertices = vertices;
				newMesh.normals = normals;
				newMesh.tangents = tangents;
				newMesh.triangles = _baseMesh.triangles;
				var uvs = new List<Vector2>();
				for (var i = 0; i < 8; i++) {
					_baseMesh.GetUVs(i, uvs);
					if (uvs.Count > 0) {
						newMesh.SetUVs(i, uvs);
					}
					uvs.Clear();
				}
				
				//ボーン変形情報削除
				newMesh.boneWeights = null;
				newMesh.bindposes = new Matrix4x4[0];

				//ブレンドシェイプ削除
				newMesh.ClearBlendShapes();

				if (isCreate) {
					AssetDatabase.CreateAsset(newMesh, meshPath);
				}
				Debug.Log($"Create mesh : {meshPath}");

			}

			//専用マテリアル生成
			Material newMat;
			{
				var matPath = $"{dirName}/{rendererName}_{_animClip.name}.mat";
				var oldMatAsset = (Material)AssetDatabase.LoadAssetAtPath(matPath, typeof(Material));
				var isCreate = false;
				//上書きする
				if (_isOverwriteAsset) {
					if (oldMatAsset) {
						newMat = oldMatAsset;
					}
					else {
						newMat = new Material(_targetRenderer.sharedMaterial);
						isCreate = true;
					}
					
				}
				//上書きしない
				else {
					matPath = AssetDatabase.GenerateUniqueAssetPath(matPath);
					newMat = new Material(_targetRenderer.sharedMaterial);
					isCreate = true;
				}
				newMat.shader = _targetShader;

				if (isCreate) {
					AssetDatabase.CreateAsset(newMat, matPath);
				}


				var newTex = (Texture)AssetDatabase.LoadAssetAtPath(texPath, typeof(Texture));
				newMat.SetTexture("_VatTex", newTex);
				newMat.SetFloat("_VatFps", _animFps);


				newMat.enableInstancing = true;
				if (_RotationInterpolationMode) {
					newMat.EnableKeyword("VAT_ROTATION_INTERPOLATION");
				}
				else {
					newMat.DisableKeyword("VAT_ROTATION_INTERPOLATION");
				}
				

				Debug.Log($"Create mesh : {matPath}");
			}

			//表示更新
			AssetDatabase.Refresh();

			//シーン上にVAT化したオブジェクトを生成
			{
				var objName = _rootObj.name + "_vat";
				GameObject oldObj = null;
				MeshRenderer oldRenderer = null;
				MeshFilter oldMeshFilter = null;
				//あれば上書きする(古いの削除する)
				if (_isOverwriteGameobject) {
					var t = _outputTransform ? _outputTransform.Find(objName) : GameObject.Find(objName)?.transform;
					if (t) {
						oldObj = t.gameObject;
						oldRenderer = t.GetComponent<MeshRenderer>();
						oldMeshFilter = t.GetComponent<MeshFilter>();
					}
				}
				//上書きしない
				else { 
					objName = GetUniqueObjectName(_outputTransform, objName);
				}

				var newObj = oldObj ? oldObj : new GameObject(objName);
				var meshFilter = oldMeshFilter ? oldMeshFilter : newObj.AddComponent<MeshFilter>();
				var meshRenderer = oldRenderer ? oldRenderer : newObj.AddComponent<MeshRenderer>();
				meshFilter.mesh = newMesh;
				meshRenderer.sharedMaterial = newMat;
				newObj.transform.parent = _outputTransform;
				newObj.transform.localPosition = Vector3.zero;
				newObj.transform.localScale = Vector3.one;
				newObj.transform.localRotation = Quaternion.identity;
			}
		}


		/****************** UTIL *********************/

		private Color GetColor(Vector3 data)
		{
			Color col;
			col.r = data.x;
			col.g = data.y;
			col.b = data.z;
			col.a = 1;
			return col;
		}
		private Color GetColor(Vector4 data)
		{
			Color col;
			col.r = data.x;
			col.g = data.y;
			col.b = data.z;
			col.a = data.w;
			return col;
		}
		private Color GetColor(Quaternion data)
		{
			Color col;
			col.r = data.x;
			col.g = data.y;
			col.b = data.z;
			col.a = data.w;
			return col;
		}

		private void DrawSeparator()
		{
			// 仕切り線のスタイルを設定
			GUIStyle separatorStyle = new GUIStyle(GUI.skin.box);
			separatorStyle.border.top = 1;
			separatorStyle.border.bottom = 1;
			separatorStyle.margin.top = 10;
			separatorStyle.margin.bottom = 10;
			separatorStyle.fixedHeight = 2;

			// 仕切り線を描画
			GUILayout.Box(GUIContent.none, separatorStyle, GUILayout.ExpandWidth(true), GUILayout.Height(1));
		}

		private string CreateFoldersRecursivelyIfNotExist(string path)
		{
			// 'Assets' から始まらないパスを正しい形に修正
			if (!path.StartsWith("Assets")) {
				path = "Assets/" + path;
			}

			// パスが既に存在するか確認
			if (!AssetDatabase.IsValidFolder(path)) {
				// 存在しない場合は、親フォルダをチェック
				string parentPath = Path.GetDirectoryName(path);

				// 親フォルダも再帰的に作成
				CreateFoldersRecursivelyIfNotExist(parentPath);

				// 親フォルダが作成されたので、目的のフォルダを作成
				string newFolderName = Path.GetFileName(path);
				AssetDatabase.CreateFolder(parentPath, newFolderName);
			}

			return path;
		}

		#region tmp transform
		private Transform _tmpT;
		private void CreateTmpTransform()
		{
			_tmpT = new GameObject("tmp").transform;
		}
		public void DestroyTmpTransform()
		{
			DestroyImmediate(_tmpT);
		}
		#endregion

		/// <summary>
		/// parentを原点とした場合の相対座標となるカスタムトランスフォームを作る
		/// 
		/// 例：
		/// parent(1,0,0)
		///  - anim_root(0,1,0)
		///   - render(0,0,1)
		/// 
		/// この時renderはparentから見て(0,1,1)にあり,
		/// 戻り値のrenderのカスタムトランスフォームは原点から見て(0,1,1)にあるような座標系になる。
		/// 
		/// 
		/// </summary>
		/// <param name="src"></param>
		/// <param name="parent"></param>
		/// <param name="reuseTransform"></param>
		/// <returns></returns>
		Transform CreateCustomTransform(Transform src, Transform parent, Transform reuseTransform = null)
		{
			var retT = reuseTransform;

			if (!retT) {
				retT = new GameObject(src.name).transform;
			}

			//入力Transform(コピー)をparentの子にすることで、コピーのローカル座標はparentと入力の差になる
			CopyTransform(src, _tmpT);
			_tmpT.parent = parent;

			//出力をシーン直下にし、コピーのローカル座標を指定することで、出力は意図した座標系になる
			retT.parent = null;
			retT.localPosition = _tmpT.localPosition;
			retT.localScale = _tmpT.localScale;
			retT.localRotation = _tmpT.localRotation;

			return retT;
		}

		/// <summary>
		/// Transformコピー
		/// </summary>
		/// <param name="src"></param>
		/// <param name="dst"></param>
		void CopyTransform(Transform src, Transform dst)
		{
			var bk = dst.parent;
			dst.parent = src;
			dst.localPosition = Vector3.zero;
			dst.localScale = Vector3.one;
			dst.localRotation = Quaternion.identity;
			dst.parent = bk;
		}

		/// <summary>
		/// Uniqueなゲームオブジェクト名を取得
		/// </summary>
		/// <param name="parent"></param>
		/// <param name="baseName"></param>
		/// <returns></returns>
		string GetUniqueObjectName(Transform parent, string baseName)
		{
			string newName = baseName;
			int counter = 1;

			if (parent) {
				// 同名のGameObjectが存在する場合は、名前にサフィックスを付ける
				while (parent.Find(newName)) {
					newName = baseName + "_" + counter++;
				}
			}
			else {
				while (GameObject.Find(newName)) {
					newName = baseName + "_" + counter++;
				}
			}
			
			return newName;
		}

	}
}
