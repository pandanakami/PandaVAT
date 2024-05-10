using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;


namespace PandaScript.PandaVat
{
	[RequireComponent(typeof(PandaTestHandler))]
	public class PandaTestButton : MonoBehaviour
	{
		private PandaTestHandler _handler;
		// Start is called before the first frame update
		void Start()
		{
			_handler = GetComponent<PandaTestHandler>();
		}

#if UNITY_EDITOR
		[CustomEditor(typeof(PandaTestButton))]
		private class MyComponentEditor : Editor
		{
			public override void OnInspectorGUI()
			{
				base.OnInspectorGUI();

				if (GUILayout.Button("My Button")) {
					var button = (PandaTestButton)target;
					button._handler.OnAction();
				}
			}
		}
#endif
	}

}
