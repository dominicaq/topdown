using UnityEngine;

namespace FogOfWar
{
    public class FogRenderer : MonoBehaviour
    {
        [Header("Properties")]
        public int upscaleFactor = 4;
        public Material fogMaterial;

        [Header("Compute Shader")]
        public ComputeShader fogCompute;
        private int _kernelHandle;
        private ComputeBuffer _lightMapBuffer;

        [Header("Texture Output")]
        public RenderTexture fogTexture;

        [Header("Dimensions")]
        private int _tileSize;
        private int _chunkSize;
        private float _gridSize;

        [Header("Components")]
        private FogManager _fogManager;
        private Camera _projector;

        private void Start() {
            _fogManager = GetComponent<FogManager>();
            if (!_fogManager) {
                Debug.LogError("FogManager component missing from GameObject.");
                return;
            }

            _projector = GetComponent<Camera>();
            if (!_projector) {
                Debug.LogError("Fog Renderer requires a camera.");
                return;
            }

            _chunkSize = _fogManager.chunkSize;
            _tileSize = _fogManager.tileSize;

            // Compute grid size (for upscaling / mapping)
            _gridSize = _chunkSize * _tileSize;

            InitProjectorCamera();
            InitFogTexture();
        }

        private void InitProjectorCamera() {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Basic camera setup
            _projector.enabled = true;
            _projector.orthographic = true;
            _projector.orthographicSize = _gridSize * 0.5f;
            _projector.aspect = 1.0f;
            _projector.nearClipPlane = 0.3f;
            _projector.farClipPlane = 100f;

            // Prevent rendering t/he projector camera by creating a dummy target texture
            _projector.targetTexture = new RenderTexture(1, 1, 0);
            _projector.cullingMask = 0;
            _projector.clearFlags = CameraClearFlags.Nothing;
        }

        private void InitFogTexture() {
            int textureSize = Mathf.RoundToInt(_gridSize);
            fogTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            fogTexture.enableRandomWrite = true;
            fogTexture.Create();

            // Create compute shader variables and buffer
            _lightMapBuffer = new ComputeBuffer(_chunkSize * _chunkSize / 4, sizeof(int));
            fogCompute.SetInt("_chunkSize", _chunkSize);
            fogCompute.SetInt("upscaleFactor", upscaleFactor);
            fogCompute.SetInt("tileSize", _tileSize);

            _kernelHandle = fogCompute.FindKernel("CSMain");
            fogCompute.SetTexture(_kernelHandle, "Result", fogTexture);
        }

        private void UpdateMaterialProperties() {
            // Get the main camera (the one doing the final post-process)
            Camera mainCam = Camera.main;
            if (mainCam == null) {
                Debug.LogError("Main camera not found.");
                return;
            }

            // Compute main camera's inverse view-projection matrix.
            Matrix4x4 mainViewMatrix = mainCam.worldToCameraMatrix;
            Matrix4x4 mainProjMatrix = GL.GetGPUProjectionMatrix(mainCam.projectionMatrix, false);
            Matrix4x4 mainVP = mainProjMatrix * mainViewMatrix;
            Matrix4x4 invMainVP = mainVP.inverse;
            fogMaterial.SetMatrix("_InvViewProjMatrix", invMainVP);

            // Compute the projector's view-projection matrix using the depth camera.
            Matrix4x4 projectorViewMatrix = _projector.worldToCameraMatrix;
            Matrix4x4 projectorProjMatrix = GL.GetGPUProjectionMatrix(_projector.projectionMatrix, false);
            Matrix4x4 projectorVP = projectorProjMatrix * projectorViewMatrix;
            fogMaterial.SetMatrix("_ProjectorVP", projectorVP);

            // Get main cameras depth texture
            fogMaterial.SetTexture("_DepthTex", Shader.GetGlobalTexture("_CameraDepthTexture"));
            fogMaterial.SetTexture("_FogTex", fogTexture);
        }

        private void UpdateFogTexture() {
            int packedLen = _chunkSize * _chunkSize / 4;
            int[] lightMapData = new int[packedLen];

            // Pack 4 bytes into a single int
            for (int i = 0; i < packedLen; i++) {
                int baseIndex = i * 4;
                int x = baseIndex % _chunkSize;
                int y = baseIndex / _chunkSize;

                byte b1 = _fogManager.lightMap[x, y].GetPackedData();
                byte b2 = _fogManager.lightMap[x + 1, y].GetPackedData();
                byte b3 = _fogManager.lightMap[x + 2, y].GetPackedData();
                byte b4 = _fogManager.lightMap[x + 3, y].GetPackedData();

                lightMapData[i] = b1 | (b2 << 8) | (b3 << 16) | (b4 << 24);
            }

            _lightMapBuffer.SetData(lightMapData);
            fogCompute.SetBuffer(_kernelHandle, "LightMap", _lightMapBuffer);

            int threadGroupsX = Mathf.CeilToInt(fogTexture.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(fogTexture.height / 8.0f);
            fogCompute.Dispatch(_kernelHandle, threadGroupsX, threadGroupsY, 1);
        }

        private void Update() {
            UpdateMaterialProperties();
            UpdateFogTexture();
        }

        private void OnDestroy() {
            _lightMapBuffer?.Release();
        }
    }
}
