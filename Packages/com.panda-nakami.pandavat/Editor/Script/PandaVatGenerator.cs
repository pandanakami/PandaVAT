
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Analytics;
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
		/// (動くなら変えていいよ。フレーム数*3(場合によってさらに+1)がテクスチャの高さだよ。)
		/// </summary>
		private const int MAX_FRAME_NUM = 8192/3;

		private const string DEFAULT_SAVE_POS = "Assets";

		/****************** GUI input *********************/

		private string _savePos;
		private string _rootFullPath => Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/") + "/";
		private string _savePosFullPath => _rootFullPath + _savePos;
		private Animator _rootAnim;
		private AnimationClip _animClip;
		private Renderer _targetRenderer;
		private Shader _targetShader;


		private int _animFps = 30;

		private bool _RotationCompletionMode = false;

		private Vector2 scrollPosition;

		/******************* field ********************/

		private GameObject _rootObj;
		private SkinnedMeshRenderer _targetSkinnedMeshRenderer = null;
		private MeshRenderer _targetMeshRenderer = null;
		private Mesh _baseMesh;



		private Renderer[] _renderers;
		private bool _showMeshRenderers = false;
		private AnimationClip[] _animationClips;
		private bool _showAnimationClips = false;

		private bool _hasRendererError = false;
		private bool _hasAnimationError = false;
		private bool _hasModeError = false;
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
			GUILayout.Label("Save Pos", GUILayout.Width(148));
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

			//回転補間モード
			EditorGUI.BeginChangeCheck();
			_RotationCompletionMode = EditorGUILayout.Toggle("回転補間モード", _RotationCompletionMode);
			if (EditorGUI.EndChangeCheck()) {
				_CheckMode();
			}
			DrawSeparator();

			
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
				if (_RotationCompletionMode) {
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
				_savePos = CreateFoldersRecursivelyIfNotExist(_savePos);

				//セット後の変更エラーチェック
				_CheckRendererEnable();
				_CheckAnimClipEnable();
				_CheckMode();
				if (_hasRendererError || _hasAnimationError || _hasModeError) {
					throw new Exception(_ErrorCheckResult);
				}

				_targetSkinnedMeshRenderer = null;
				_targetMeshRenderer = null;
				_baseMesh = null;
				_rootObj = _rootAnim.gameObject;

				var isSkinnedMeshRenderer = true;

				if (_targetRenderer is SkinnedMeshRenderer) {
					_targetSkinnedMeshRenderer = _targetRenderer as SkinnedMeshRenderer;
					_baseMesh = _targetSkinnedMeshRenderer.sharedMesh;
				}
				else if (_targetRenderer is MeshRenderer) {
					isSkinnedMeshRenderer = false;
					_targetMeshRenderer = _targetRenderer as MeshRenderer;
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

			GUI.enabled = true;

			// スクロールビューの終了
			GUILayout.EndScrollView();
		}

		/// <summary>
		/// アニメーターの指定とレンダラーとアニメーションクリップ選択
		/// </summary>
		private void _DispAnimatorAndRendererSelector()
		{
			EditorGUI.BeginChangeCheck();
			_rootAnim = (Animator)EditorGUILayout.ObjectField("対象のAnimator", _rootAnim, typeof(Animator), true);
			if (EditorGUI.EndChangeCheck()) {
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
				_targetSkinnedMeshRenderer = _targetRenderer as SkinnedMeshRenderer;
				_baseMesh = _targetSkinnedMeshRenderer.sharedMesh;
				isSkinnedMeshRenderer = true;
			}
			else if (_targetRenderer is MeshRenderer) {
				_targetMeshRenderer = _targetRenderer as MeshRenderer;
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
				var shaderIsRotationCompletionMode = _targetShader.FindPropertyIndex("_RotationCompletionMode") != -1;

				if(shaderIsRotationCompletionMode != _RotationCompletionMode) {
					if (_RotationCompletionMode) {
						_ErrorCheckResult = "[セットされているシェーダーは回転補間非対応です]";
					}
					else {
						_ErrorCheckResult = "[セットされているシェーダーは回転補間用です]";
					}
					
					_hasModeError = true;
					return;
				}
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
			var frameCount_ = _RotationCompletionMode ? frameCount * 3 + 3 : frameCount * 3;
			var texture = new Texture2D(vertexCount, frameCount_, TextureFormat.RGBAHalf, false, false);
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Point;


			var rootT = _rootObj.transform;
			var renderT = _targetRenderer.transform;

			//デフォルト情報保持
			var defaultVertices = _baseMesh.vertices;
			var defaultNormals = _baseMesh.normals;
			var defaultTangents = _baseMesh.tangents;
			for (var i = 0; i < vertexCount; i++) {
				defaultVertices[i] = renderT.TransformPoint(defaultVertices[i]);
				defaultNormals[i] = renderT.TransformDirection(defaultNormals[i]);
				var tanXyz = renderT.TransformDirection(defaultTangents[i]);
				defaultTangents[i].x = tanXyz.x;
				defaultTangents[i].y = tanXyz.y;
				defaultTangents[i].z = tanXyz.z;
			}

			//回転補正モード
			if (_RotationCompletionMode) {
				if (isSkinedMeshRenderer) {
					_GenerateVATRotationSpecialSkinnedMeshRenderer(rootT, renderT, texture, vertexCount, frameCount, duration);
				}
				else {
					_GenerateVATRotationSpecialMeshRenderer(rootT, renderT, texture, vertexCount, frameCount, duration);
				}
				
			}
			//通常モード
			else {
				_GenerateVATCommon(rootT, renderT, texture, vertexCount, frameCount, duration, 
					isSkinedMeshRenderer, defaultVertices, defaultNormals, defaultTangents);
			}

			_animClip.SampleAnimation(_rootObj, 0);//アニメーションリセット

			texture.Apply();

			//各種保存
			_SaveAsset(texture, rendererName, defaultVertices, defaultNormals, defaultTangents);
			
		
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
		private void _GenerateVATCommon(Transform rootT, Transform renderT, Texture2D texture, int vertexCount, int frameCount, float duration, 
			bool isSkinedMeshRenderer, Vector3[] defaultVertices, Vector3[] defaultNormals, Vector4[] defaultTangents)
		{

			//テクスチャに格納
			Mesh tmpMesh = new Mesh();
			for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {

				_animClip.SampleAnimation(_rootObj, ((float)frameIndex / (frameCount - 1)) * duration);
				if (isSkinedMeshRenderer) {
					_targetSkinnedMeshRenderer.BakeMesh(tmpMesh, true);// useScale=trueだとルートのスケールが入らない
				}
				else {
					tmpMesh = _baseMesh;
				}

				Vector3[] vertices = tmpMesh.vertices;
				Vector3[] normals = tmpMesh.normals;
				Vector4[] tangents = tmpMesh.tangents;

				for (int vertIndex = 0; vertIndex < vertexCount; vertIndex++) {
					
					Vector3 positionDiff = renderT.TransformPoint(vertices[vertIndex]) - defaultVertices[vertIndex];
					Vector3 normalDiff = renderT.TransformDirection(normals[vertIndex]) - defaultNormals[vertIndex];
					var tangent = renderT.TransformDirection(tangents[vertIndex]);
					var tangentDiff = new Vector4(tangent.x, tangent.y, tangent.z, tangents[vertIndex].w) - defaultTangents[vertIndex];

					texture.SetPixel(vertIndex, frameIndex, GetColor(positionDiff));
					texture.SetPixel(vertIndex, frameIndex + frameCount, GetColor(normalDiff));
					texture.SetPixel(vertIndex, frameIndex + frameCount * 2, GetColor(tangentDiff));
				}
			}
		}

		/// <summary>
		/// VAT生成(回転補正用 SkinnedMeshRenderer)
		/// </summary>
		/// <param name="rootT">AnimatorのルートTransform</param>
		/// <param name="renderT">RendererのTransform</param>
		/// <param name="texture">書き込み対象テクスチャ</param>
		/// <param name="vertexCount">頂点の数</param>
		/// <param name="frameCount">フレーム数</param>
		/// <param name="duration">アニメーションの長さ(秒)</param>
		private void _GenerateVATRotationSpecialSkinnedMeshRenderer(Transform rootT, Transform renderT, Texture2D texture, int vertexCount, int frameCount, float duration)
		{

			//デフォルトメッシュに対して。
			//各頂点に対して
			//属するボーンを探す。ボーン複数NG
			//ボーンと頂点の位置の差を取得
			//最終行に書く
			{
				BoneWeight[] boneWeights = _baseMesh.boneWeights;
				Transform[] bones = _targetSkinnedMeshRenderer.bones;
				var defaultDiffPos = _baseMesh.vertices;
				var defaultDiffNormals = _baseMesh.normals;
				var defaultDiffTangents= _baseMesh.tangents;

				for (var vertIndex = 0; vertIndex < vertexCount; vertIndex++) {
					//メッシュの頂点 → ワールド座標 → ルートからのローカル座標へ
					defaultDiffPos[vertIndex] = rootT.InverseTransformPoint(renderT.TransformPoint(defaultDiffPos[vertIndex]));
					defaultDiffNormals[vertIndex] = rootT.InverseTransformDirection(renderT.TransformDirection(defaultDiffNormals[vertIndex]));
					defaultDiffTangents[vertIndex] = rootT.InverseTransformDirection(renderT.TransformDirection(defaultDiffTangents[vertIndex]));
				}

				for (var vertIndex = 0; vertIndex < vertexCount; vertIndex++) {
				

					//bone0が100%前提。
					var boneIndex = boneWeights[vertIndex].boneIndex0;
					var bonePos = rootT.InverseTransformPoint(bones[boneIndex].position);
					defaultDiffPos[vertIndex] -= bonePos; //ボーンとの差

					texture.SetPixel(vertIndex, frameCount * 3, GetColor(defaultDiffPos[vertIndex]));

					//normal
					
					texture.SetPixel(vertIndex, frameCount * 3 + 1, GetColor(defaultDiffNormals[vertIndex]));

					//tangent
					float w = defaultDiffTangents[vertIndex].w;
					
					defaultDiffTangents[vertIndex].w = w;
					texture.SetPixel(vertIndex, frameCount * 3 + 2, GetColor(defaultDiffTangents[vertIndex]));
				}
			}

			//各フレームで
			//各頂点に対して
			//属するボーンのスケール・回転・平行移動を取得

			//テクスチャに格納
			for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {

				_animClip.SampleAnimation(_rootObj, ((float)frameIndex / (frameCount - 1)) * duration);

				Transform[] bones = _targetSkinnedMeshRenderer.bones;
				Mesh mesh = _targetSkinnedMeshRenderer.sharedMesh;
				BoneWeight[] boneWeights = mesh.boneWeights;

				for (int vertIndex = 0; vertIndex < vertexCount; vertIndex++) {

					//bone0が100%前提。
					var boneIndex = boneWeights[vertIndex].boneIndex0;
					var boneT = bones[boneIndex];

					var position = boneT.position;
					var scale = boneT.lossyScale;
					var rotation = boneT.rotation;

					texture.SetPixel(vertIndex, frameIndex, GetColor(position));
					texture.SetPixel(vertIndex, frameIndex + frameCount, GetColor(rotation));
					texture.SetPixel(vertIndex, frameIndex + frameCount * 2, GetColor(scale));
				}
			}
		}

		/// <summary>
		/// VAT生成(回転補正用 MeshRenderer)
		/// </summary>
		/// <param name="rootT">AnimatorのルートTransform</param>
		/// <param name="renderT">RendererのTransform</param>
		/// <param name="texture">書き込み対象テクスチャ</param>
		/// <param name="vertexCount">頂点の数</param>
		/// <param name="frameCount">フレーム数</param>
		/// <param name="duration">アニメーションの長さ(秒)</param>
		private void _GenerateVATRotationSpecialMeshRenderer(Transform rootT, Transform renderT, Texture2D texture, int vertexCount, int frameCount, float duration)
		{

			//デフォルト位置
			//各頂点に対して
			//オブジェクト座標と頂点の位置の差を取得
			//最終行に書く
			{
				var defaultDiffPos = _baseMesh.vertices;
				var defaultDiffNormals = _baseMesh.normals;
				var defaultDiffTangents = _baseMesh.tangents;
				for (var vertIndex = 0; vertIndex < vertexCount; vertIndex++) {
					//メッシュの頂点 → ワールド座標 → ルートからのローカル座標へ
					defaultDiffPos[vertIndex] = rootT.InverseTransformPoint(renderT.TransformPoint(defaultDiffPos[vertIndex]));

					var renderPos = rootT.InverseTransformPoint(renderT.position);
					defaultDiffPos[vertIndex] -= renderPos; //オブジェクト座標との差

					texture.SetPixel(vertIndex, frameCount * 3, GetColor(defaultDiffPos[vertIndex]));

					//normal
					defaultDiffNormals[vertIndex] = rootT.InverseTransformDirection(renderT.TransformDirection(defaultDiffNormals[vertIndex]));
					texture.SetPixel(vertIndex, frameCount * 3 + 1, GetColor(defaultDiffNormals[vertIndex]));

					//tangent
					float w = defaultDiffTangents[vertIndex].w;
					defaultDiffTangents[vertIndex] = rootT.InverseTransformDirection(renderT.TransformDirection(defaultDiffTangents[vertIndex]));
					defaultDiffTangents[vertIndex].w = w;
					texture.SetPixel(vertIndex, frameCount * 3 + 2, GetColor(defaultDiffTangents[vertIndex]));
				}
			}

			//各フレームで
			//各頂点に対して
			//属するボーンのスケール・回転・平行移動を取得

			//テクスチャに格納
			for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {

				_animClip.SampleAnimation(_rootObj, ((float)frameIndex / (frameCount - 1)) * duration);

				for (int vertIndex = 0; vertIndex < vertexCount; vertIndex++) {

					var position = renderT.position;
					var scale = renderT.lossyScale;
					var rotation = renderT.rotation;

					texture.SetPixel(vertIndex, frameIndex, GetColor(position));
					texture.SetPixel(vertIndex, frameIndex + frameCount, GetColor(rotation));
					texture.SetPixel(vertIndex, frameIndex + frameCount * 2, GetColor(scale));
				}
			}
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
			var texPass = $"{dirName}/{rendererName}_{_animClip.name}.exr";
			File.WriteAllBytes(texPass, data);
			AssetDatabase.Refresh();
			Debug.Log($"Create texture : {texPass}");

			//画像設定
			{
				TextureImporter textureImporter = AssetImporter.GetAtPath(texPass) as TextureImporter;
				textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
				textureImporter.SetPlatformTextureSettings(new TextureImporterPlatformSettings {
					overridden = true,
					format = TextureImporterFormat.RGBAHalf
				});
				textureImporter.npotScale = TextureImporterNPOTScale.None;
				textureImporter.filterMode = FilterMode.Point;
				textureImporter.wrapMode = TextureWrapMode.Repeat;
				textureImporter.mipmapEnabled = false;
				AssetDatabase.ImportAsset(texPass, ImportAssetOptions.ForceUpdate);
			}

			//メッシュセット
			Mesh newMesh;
			{
				newMesh = Instantiate(_baseMesh);
				var meshPass = $"{dirName}/{_baseMesh.name}_vat.asset";
				newMesh.vertices = vertices;
				newMesh.normals = normals;
				newMesh.tangents = tangents;

				//ボーン変形情報削除
				newMesh.boneWeights = null;
				newMesh.bindposes = new Matrix4x4[0];

				//ブレンドシェイプ削除
				newMesh.ClearBlendShapes();

				AssetDatabase.CreateAsset(newMesh, meshPass);
				Debug.Log($"Create mesh : {meshPass}");

			}

			//専用マテリアル生成
			Material newMat;
			{
				var matPass = $"{dirName}/{rendererName}_{_animClip.name}.mat";
				newMat = (Material)AssetDatabase.LoadAssetAtPath(matPass, typeof(Material));
				var isCreate = false;
				if (!newMat) {
					newMat = new Material(_targetRenderer.material);
					newMat.shader = _targetShader;

					isCreate = true;
				}
				else {
					newMat.shader = _targetShader;
				}

				if (isCreate) {
					AssetDatabase.CreateAsset(newMat, matPass);
				}

				newMat.enableInstancing = true;

				var newTex = (Texture)AssetDatabase.LoadAssetAtPath(texPass, typeof(Texture));
				newMat.SetTexture("_VatTex", newTex);
				newMat.SetFloat("_VatFps", _animFps);
				Debug.Log($"Create mesh : {matPass}");
			}

			//表示更新
			AssetDatabase.Refresh();

			//シーン上にVAT化したオブジェクトを生成
			{
				var newObj = new GameObject(_rootObj.name + "_vat");
				var meshFilter = newObj.AddComponent<MeshFilter>();
				var meshRenderer = newObj.AddComponent<MeshRenderer>();
				meshFilter.mesh = newMesh;
				meshRenderer.sharedMaterial = newMat;
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

		
	}
}
