using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

namespace PandaScript.PandaVat
{

	[CustomEditor(typeof(Material))]
	public class PandVatMaterialEditor : MaterialEditor
	{
		int VAT_MAIN_ID = -1;
		int VAT_ROTATION_IMTERPOLATION_ID = -1;
		int VAT_TEX_ID = -1;
		int VAT_FPS_ID = -1;
		int VAT_START_TIME_SEC_ID = -1;
		int VAT_SPEED_ID = -1;
		int VAT_LOOP_ID = -1;
		int VAT_START_TIME_OFFSET_ID = -1;
		int VAT_END_TIME_OFFSET_ID = -1;
		int VAT_CTRL_WITH_RATE_ID = -1;
		int VAT_RATE_ID = -1;

		Dictionary<int, string> TAG_DIC = new Dictionary<int, string>();

		public override void OnEnable()
		{
			base.OnEnable();

			VAT_MAIN_ID = Shader.PropertyToID("_PandaVat");
			TAG_DIC.Add(VAT_MAIN_ID, "VATのプロパティ表示");

			VAT_ROTATION_IMTERPOLATION_ID = Shader.PropertyToID("_RotationInterpolationMode");

			VAT_TEX_ID = Shader.PropertyToID("_VatTex");
			TAG_DIC.Add(VAT_TEX_ID, "VATテクスチャ");

			VAT_FPS_ID = Shader.PropertyToID("_VatFps");
			TAG_DIC.Add(VAT_FPS_ID, "VAT FPS");

			VAT_START_TIME_SEC_ID = Shader.PropertyToID("_VatStartTimeSec");
			TAG_DIC.Add(VAT_START_TIME_SEC_ID, "VAT開始時間 [秒]");

			VAT_SPEED_ID = Shader.PropertyToID("_VatSpeed");
			TAG_DIC.Add(VAT_SPEED_ID, "VATスピード");

			VAT_START_TIME_OFFSET_ID = Shader.PropertyToID("_VatStartTimeOffset");
			TAG_DIC.Add(VAT_START_TIME_OFFSET_ID, "開始前時間 [秒]");

			VAT_END_TIME_OFFSET_ID = Shader.PropertyToID("_VatEndTimeOffset");
			TAG_DIC.Add(VAT_END_TIME_OFFSET_ID, "開始後時間 [秒]");

			VAT_LOOP_ID = Shader.PropertyToID("_VatLoop");
			TAG_DIC.Add(VAT_LOOP_ID, "VATをループするか否か");

			VAT_CTRL_WITH_RATE_ID = Shader.PropertyToID("_VatCtrlWithRate");
			TAG_DIC.Add(VAT_CTRL_WITH_RATE_ID, "VAT制御方法 [時間/割合]");

			VAT_RATE_ID = Shader.PropertyToID("_VatRate");
			TAG_DIC.Add(VAT_RATE_ID, "VAT割合");
		}

		public override void OnInspectorGUI()
		{
			// 現在のマテリアルを取得
			Material material = target as Material;

			if (material.HasInt(VAT_MAIN_ID)) {
				if (!isVisible) {
					return;
				}

				//回転補間モード
				var isSet = material.IsKeywordEnabled("PANDA_VAT_IDENTIFY");
				if (material.HasInt(VAT_ROTATION_IMTERPOLATION_ID)) {
					if (!isSet) {
						material.EnableKeyword("VAT_ROTATION_INTERPOLATION");
					}
				}
				else {
					if (isSet) {
						material.DisableKeyword("VAT_ROTATION_INTERPOLATION");
					}
				}


				//表示設定
				var isDisplay = material.GetInt(VAT_MAIN_ID) == 1;
				EditorGUI.BeginChangeCheck();
				isDisplay = EditorGUILayout.Foldout(isDisplay, TAG_DIC[VAT_MAIN_ID]);
				if (EditorGUI.EndChangeCheck()) {
					material.SetInt(VAT_MAIN_ID, isDisplay ? 1 : 0);
				}
				if (isDisplay) {

					//テクスチャ
					{
						var id = VAT_TEX_ID;
						var val = material.GetTexture(id);
						EditorGUI.BeginChangeCheck();
						val = EditorGUILayout.ObjectField(TAG_DIC[id], val, typeof(Texture2D), true) as Texture;
						if (EditorGUI.EndChangeCheck()) {
							material.SetTexture(id, val);
						}
					}

					//FPS
					{
						var val = material.GetFloat(VAT_FPS_ID);
						EditorGUI.BeginChangeCheck();
						val = EditorGUILayout.FloatField(TAG_DIC[VAT_FPS_ID], val);
						var isChange = EditorGUI.EndChangeCheck();
						if (isChange) {
							var result = EditorUtility.DisplayDialog("注意", "この値はVATテクスチャとセットになります。\nこの値だけ変更すると壊れます。\n変更しますか?", "YES", "CANCEL");
							if (result) {
								material.SetFloat(VAT_FPS_ID, val);
							}
						}
					}

					bool isCtrlWithRateChange;
					var isCtrlWithRate = _DispToggleProp(material, VAT_CTRL_WITH_RATE_ID, "VAT_CTRL_WITH_RATE", out isCtrlWithRateChange, true);

					//割合制御
					if (isCtrlWithRate) {
						var style = new GUIStyle();
						style.normal.textColor = Color.green;
						EditorGUILayout.LabelField("[割合で制御する]", style);
						var id = VAT_RATE_ID;
						float val = material.GetFloat(id);
						EditorGUI.BeginChangeCheck();
						val = EditorGUILayout.Slider(TAG_DIC[id], val, 0.0f, 1.0f);
						if (EditorGUI.EndChangeCheck()) {
							material.SetFloat(id, val);
						}

						if (isCtrlWithRateChange) {
							_DisableToggleProp(material, VAT_LOOP_ID, "VAT_LOOP");
						}
					}
					//時間制御
					else {
						var style = new GUIStyle();
						style.normal.textColor = Color.cyan;
						EditorGUILayout.LabelField("[時間で制御する]", style);
						bool unuse;
						//ループか否か
						_DispToggleProp(material, VAT_LOOP_ID, "VAT_LOOP", out unuse);

						//開始時間
						_DisplayFloat(material, VAT_START_TIME_SEC_ID);
						//スピード
						_DisplayFloat(material, VAT_SPEED_ID);

						//開始前時間
						_DisplayFloat(material, VAT_START_TIME_OFFSET_ID);
						
						//開始後時間
						_DisplayFloat(material, VAT_END_TIME_OFFSET_ID);

						
					}
				}
				_DrawSeparator();
			}


			// ベースクラスのGUIを描画
			base.OnInspectorGUI();
		}

		/// <summary>
		/// Floatの入力フィールド表示
		/// 入力あればマテリアルにセットする
		/// </summary>
		/// <param name="material"></param>
		/// <param name="id"></param>
		private void _DisplayFloat(Material material, int id)
		{
			var val = material.GetFloat(id);
			EditorGUI.BeginChangeCheck();
			val = EditorGUILayout.FloatField(TAG_DIC[id], val);
			var isChange = EditorGUI.EndChangeCheck();
			if (isChange) {
				material.SetFloat(id, val);
			}
		}

		private bool _DispToggleProp(Material material, int id, string keyword, out bool isChange, bool uiIsReverse = false)
		{
			var val = material.IsKeywordEnabled(keyword);
			var uiVal = val ^ uiIsReverse;
			EditorGUI.BeginChangeCheck();
			uiVal = EditorGUILayout.Toggle(TAG_DIC[id], uiVal);
			val = uiVal ^ uiIsReverse;
			isChange = EditorGUI.EndChangeCheck();
			if (isChange) {

				material.SetInt(id, val ? 1 : 0);
				if (val) {
					material.EnableKeyword(keyword);
				}
				else {
					material.DisableKeyword(keyword);
				}

			}
			return val;
		}

		private void _DisableToggleProp(Material material, int id, string keyword)
		{
			material.SetInt(id, 0);
			material.DisableKeyword(keyword);
		}

		private void _DrawSeparator()
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
	}
}