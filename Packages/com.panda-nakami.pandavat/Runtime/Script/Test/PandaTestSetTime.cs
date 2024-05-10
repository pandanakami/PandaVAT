using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PandaScript.PandaVat
{
    [RequireComponent(typeof(MeshRenderer))]
    public class PandaTestSetTime : MonoBehaviour, PandaTestHandler
    {
        private MaterialPropertyBlock _prop;
        private MeshRenderer _renderer;
        // Start is called before the first frame update
        void Start()
        {
            _prop = new MaterialPropertyBlock();
            _renderer = GetComponent<MeshRenderer>();
        }


        public void OnAction()
        {
            _prop.SetFloat("_VatStartTimeSec", Time.time);
            _renderer.SetPropertyBlock(_prop);
        }
    }
}
