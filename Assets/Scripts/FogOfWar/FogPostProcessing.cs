using UnityEngine;

namespace FogOfWar {
    public class FogPostProcessing : MonoBehaviour
    {
        public GameObject fogManagerObject;
        private Material _fogMaterial;
        private bool _isProcessing = false;

        private void Start() {
            if (fogManagerObject == null) {
                Debug.LogError("FogPostProcessing requires fogManager GameObject.");
                return;
            }

            FogRenderer renderer = fogManagerObject.GetComponent<FogRenderer>();
            if (renderer == null) {
                Debug.LogError("FogRenderer component not found on the assigned GameObject.");
                return;
            }

            _fogMaterial = renderer.fogMaterial;
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination) {
            if (_fogMaterial == null || source == null || _isProcessing) {
                Graphics.Blit(source, destination);
                return;
            }

            _isProcessing = true;
            _fogMaterial.SetTexture("_MainTex", source);
            Graphics.Blit(source, destination, _fogMaterial);
            _isProcessing = false;
        }
    }
}
