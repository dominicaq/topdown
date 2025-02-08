using UnityEngine;

namespace FogOfWar {
    public class FogPostProcessing : MonoBehaviour
    {
        public GameObject fogManagerObject;
        private Material _fogMaterial;

        void Start() {
            FogRenderer renderer = fogManagerObject.GetComponent<FogRenderer>();
            if (renderer == null) {
                Debug.LogError("FogRenderer component not found on the assigned GameObject.");
            }

            _fogMaterial = renderer.fogMaterial;

        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) {
            if (_fogMaterial == null) {
                Debug.LogError("Requesting FogPostProcessing but no FogMaterial found!");
            }

            _fogMaterial.SetTexture("_MainTex", source);
            Graphics.Blit(source, destination, _fogMaterial);
        }
    }
}
