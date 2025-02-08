using UnityEngine;

namespace FogOfWar {
    public class FogPostProcess : MonoBehaviour
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
            if (_fogMaterial != null) {
                Graphics.Blit(source, destination, _fogMaterial);
            } else {
                Graphics.Blit(source, destination);
            }
        }
    }
}
