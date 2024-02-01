
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PandaScript.PandaVat
{

	public class PandaVatGenerator : EditorWindow
	{
		private const float VAT_SCALE = 1;

		private const float ANIM_FPS = 30;

		private const string DEFAULT_SAVE_POS = "Assets";

		/****************** GUI input *********************/

		private string _savePos;
		private string _rootFullPath => Directory.GetParent(Application.dataPath).FullName.Replace("\\", "/") + "/";
		private string _savePosFullPath => _rootFullPath + _savePos;
		private Animator _rootAnim;
		private AnimationClip _animClip;
		private Renderer _targetRenderer;
		private Shader _targetShader;

		/******************* field ********************/

		private GameObject _rootObj;
		private SkinnedMeshRenderer _targetSkinnedMeshRenderer = null;
		private MeshRenderer _targetMeshRenderer = null;
		private Mesh _baseMesh;

		/******************* method ********************/

		[MenuItem("�ς񂾃X�N���v�g/VatGenerator")]
		static void ShowWindow()
		{
			var window = GetWindow<PandaVatGenerator>("PandaVatGenerator");
			window.minSize = new Vector2(300, 300);
		}

		private void OnGUI()
		{
			if (_savePos == null) {
				_savePos = DEFAULT_SAVE_POS;
			}
			GUILayout.BeginHorizontal();
			GUILayout.Label("Save Pos", GUILayout.Width(148));
			_savePos = GUILayout.TextField(_savePos);
			if (GUILayout.Button("�I��", GUILayout.Width(40))) {
				string rootFullPath = _rootFullPath;

				string path = EditorUtility.OpenFolderPanel("Select a folder", rootFullPath + _savePos, "");
				if (!string.IsNullOrEmpty(path)) {
					if (path.StartsWith(rootFullPath + DEFAULT_SAVE_POS)) {
						_savePos = path.Replace(rootFullPath, "");//Assets�n�܂�ɂ���
					}
					else {
						Debug.LogWarning("Invalid save pos. Please specify Assets Directory.");
					}
				}
			}
			GUILayout.EndHorizontal();

			_rootAnim = (Animator)EditorGUILayout.ObjectField("Animation Root Object", _rootAnim, typeof(Animator), true);
			_targetRenderer = (Renderer)EditorGUILayout.ObjectField("Target Renderer Object", _targetRenderer, typeof(Renderer), true);
			_animClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", _animClip, typeof(AnimationClip), true);
			_targetShader = (Shader)EditorGUILayout.ObjectField("VAT Shader", _targetShader, typeof(Shader), true);

			if (GUILayout.Button("Generate")) {
				if (!_rootAnim || !_animClip || !_targetShader || !_targetRenderer) {
					Debug.LogWarning("Insufficient input.");
					return;
				}
				if (!Directory.Exists(_savePosFullPath)) {
					Debug.LogError("Error. Invalid save pos");
					return;
				}

				_targetSkinnedMeshRenderer = null;
				_targetMeshRenderer = null;
				_baseMesh = null;
				_rootObj = _rootAnim.gameObject;

				if (!_targetRenderer.transform.IsChildOf(_rootObj.transform)) {
					Debug.LogError("Error. Target Renderer is not child of Abimator.");
					return;
				}

				var isSkinnedMeshRenderer = true;

				if(_targetRenderer is SkinnedMeshRenderer) {
					_targetSkinnedMeshRenderer = _targetRenderer as SkinnedMeshRenderer;
					_baseMesh = _targetSkinnedMeshRenderer.sharedMesh;
				}
				else if(_targetRenderer is MeshRenderer) {
					isSkinnedMeshRenderer = false;
					_targetMeshRenderer = _targetRenderer as MeshRenderer;
					_baseMesh = _targetRenderer.GetComponent<MeshFilter>()?.sharedMesh;
				}
				else {
					Debug.LogError("Not supported. Target renderer object should be a MeshRenderer or SkinnedMeshRenderer.");
					return;
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
		}

		private void _CreateVat(bool isSkinedMeshRenderer)
		{
			var rendererName = _targetRenderer.name;

			var vertexCount = _baseMesh.vertexCount;    //���_��
			var duration = _animClip.length;  //�A�j���[�V�������ԁ@�b
			var frameCount = Mathf.Max((int)(duration * ANIM_FPS), 1);//�A�j���[�V�����t���[����

			//�e�N�X�`���p��
			var texture = new Texture2D(vertexCount, frameCount * 3, TextureFormat.RGBAHalf, false, false);
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Point;


			var rootT = _rootObj.transform;
			var renderT = _targetRenderer.transform;

			//�_�~�[���[�g�p��(���[�g���ό`����A�j���[�V�����̂Ƃ�������̂�
			var rootDummyObject = new GameObject();
			var rootDummyT = rootDummyObject.transform;
			rootDummyT.position = rootT.position;
			rootDummyT.rotation = rootT.rotation;
			rootDummyT.localScale = rootT.lossyScale;

			string dirName = _savePos;

			//�f�t�H���g���ێ�
			var defaultVertices = _baseMesh.vertices;
			var defaultNormals = _baseMesh.normals;
			var defaultTangents = _baseMesh.tangents;
			for (var i = 0; i < vertexCount; i++) {
				defaultVertices[i] = rootDummyT.InverseTransformPoint(renderT.TransformPoint(defaultVertices[i]));
				defaultNormals[i] = rootDummyT.InverseTransformDirection(renderT.TransformDirection(defaultNormals[i]));
				var tanXyz = rootDummyT.InverseTransformDirection(renderT.TransformDirection(defaultTangents[i]));
				defaultTangents[i].x = tanXyz.x;
				defaultTangents[i].y = tanXyz.y;
				defaultTangents[i].z = tanXyz.z;
			}

			//�e�N�X�`���Ɋi�[
			Mesh tmpMesh = new Mesh();
			for (int frameIndex = 0; frameIndex < frameCount; frameIndex++) {

				_animClip.SampleAnimation(_rootObj, ((float)frameIndex / (frameCount - 1)) * duration);
				if (isSkinedMeshRenderer) {
					_targetSkinnedMeshRenderer.BakeMesh(tmpMesh);
				}
				else {
					tmpMesh = _baseMesh;
				}


				Vector3[] vertices = tmpMesh.vertices;
				Vector3[] normals = tmpMesh.normals;
				Vector4[] tangents = tmpMesh.tangents;

				for (int vertIndex = 0; vertIndex < vertexCount; vertIndex++) {
					Vector3 position = vertices[vertIndex]; //�X�P�[�����������Ă���

					position = rootDummyT.InverseTransformPoint(renderT.TransformPoint(vertices[vertIndex]));

					position -= defaultVertices[vertIndex];

					position *= VAT_SCALE;
					Vector3 normal = rootDummyT.InverseTransformDirection(renderT.TransformDirection(normals[vertIndex])) - defaultNormals[vertIndex];
					var tangentXyz = rootDummyT.InverseTransformDirection(renderT.TransformDirection(tangents[vertIndex]));
					var tangent = new Vector4(tangentXyz.x, tangentXyz.y, tangentXyz.z, tangents[vertIndex].w) - defaultTangents[vertIndex];

					texture.SetPixel(vertIndex, frameIndex, GetColor(position));
					texture.SetPixel(vertIndex, frameIndex + frameCount, GetColor(normal));
					texture.SetPixel(vertIndex, frameIndex + frameCount * 2, GetColor(tangent));
				}
			}

			_animClip.SampleAnimation(_rootObj, 0);//�A�j���[�V�������Z�b�g

			texture.Apply();

			var data = ImageConversion.EncodeToEXR(texture, Texture2D.EXRFlags.None);
			Object.DestroyImmediate(texture);
			var texPass = $"{dirName}/{rendererName}_{_animClip.name}.exr";
			File.WriteAllBytes(texPass, data);
			AssetDatabase.Refresh();
			Debug.Log($"Create texture : {texPass}");

			//�摜�ݒ�
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

			//���b�V���Z�b�g
			Mesh newMesh;
			{
				newMesh = Instantiate(_baseMesh);
				var meshPass = $"{dirName}/{_baseMesh.name}_vat.asset";
				newMesh.vertices = defaultVertices;
				newMesh.normals = defaultNormals;
				newMesh.tangents = defaultTangents;

				//�{�[���ό`���폜
				newMesh.boneWeights = null;
				newMesh.bindposes = new Matrix4x4[0];

				//�u�����h�V�F�C�v�폜
				newMesh.ClearBlendShapes();

				AssetDatabase.CreateAsset(newMesh, meshPass);
				Debug.Log($"Create mesh : {meshPass}");

			}

			//��p�}�e���A������
			Material newMat;
			{
				var matPass = $"{dirName}/{rendererName}_{_animClip.name}.mat";
				newMat = (Material)AssetDatabase.LoadAssetAtPath(matPass, typeof(Material));
				var isCreate = false;
				if (!newMat) {
					newMat = new Material(_targetShader);
					isCreate = true;
				}

				newMat.SetTexture("_VatTex", (Texture)AssetDatabase.LoadAssetAtPath(texPass, typeof(Texture)));
				newMat.SetFloat("_VatFps", ANIM_FPS);
				newMat.SetFloat("_VatVertexCount", vertexCount);
				newMat.SetFloat("_VatFrameCount", frameCount);

				if (isCreate) {
					AssetDatabase.CreateAsset(newMat, matPass);
				}
				Debug.Log($"Create mesh : {matPass}");

			}

			//�\���X�V
			AssetDatabase.Refresh();
			Object.DestroyImmediate(rootDummyT.gameObject);
			//�V�[�����VAT�������I�u�W�F�N�g�𐶐�
			{
				var newObj = new GameObject(_rootObj.name + "_vat");
				var meshFilter = newObj.AddComponent<MeshFilter>();
				var meshRenderer = newObj.AddComponent<MeshRenderer>();
				meshFilter.mesh = newMesh;
				meshRenderer.sharedMaterial = newMat;

				//SkinnedMeshRenderer�̓��[�g�̃X�P�[�����mesh���ɓ����Ă��āA���]�Ȑ܂̖��A���ʂɃ��[�g�̃X�P�[���������Ă���B

				if (!isSkinedMeshRenderer) {
					//MeshRenderer�͌��ʂɃ��[�g�̃��[�J���ʒu�E�p�x�E�X�P�[���͓����Ă��Ȃ��B
					//�Ȃ̂Ō��I�u�W�F�N�g�Ɋp�x�E�X�P�[�������킹��B�ʒu�͌��̂ƌ������邽�ߌ��_�ɁB
					newObj.transform.localRotation = _rootObj.transform.rotation;
					newObj.transform.localScale = _rootObj.transform.lossyScale;
				}
				else {
					//SkinnedMeshRenderer�͌��ʂɃ��[�g�̃��[�J���ʒu�E�p�x�͓����Ă��Ȃ��B
					//SkinnedMeshRenderer�̓��[�g�̃X�P�[�����mesh���ɓ����Ă��āA���]�Ȑ܂̖��A���ʂɃ��[�g�̃X�P�[���������Ă���B
					//�Ȃ̂�SkinnedMeshRenderer�̏ꍇ�̓A�j���[�V�����Ń��[�g�̃X�P�[���ς����͔�Ή��B
					//���Ԃ�����B

					//���I�u�W�F�N�g�Ɋp�x�����킹��B�ʒu�͌��̂ƌ������邽�ߌ��_�ɁB
					newObj.transform.localRotation = _rootObj.transform.rotation;
				}
			}
		}

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
	}
}
