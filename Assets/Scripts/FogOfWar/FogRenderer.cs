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
        public RenderTexture depthTexture;

        [Header("Dimensions")]
        private int _tileSize;
        private int _chunkSize;
        private float _gridSize;

        [Header("Components")]
        private FogManager _fogManager;
        private Camera _depthCamera;

        void Start() {
            _fogManager = GetComponent<FogManager>();
            if (!_fogManager) {
                Debug.LogError("FogManager component missing from GameObject.");
                return;
            }

            _depthCamera = GetComponent<Camera>();
            if (!_depthCamera) {
                Debug.LogError("Fog Renderer requires an orthographic depth camera.");
                return;
            }

            _chunkSize = _fogManager.chunkSize;
            _tileSize = _fogManager.tileSize;

            // Compute grid size
            _gridSize = _chunkSize * _tileSize;

            InitDepthCamera();
            InitFogTexture();
            InitMaterialProperties();
        }

        private void InitDepthCamera() {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            // Basic camera setup
            _depthCamera.orthographic = true;
            _depthCamera.orthographicSize = _gridSize * 0.5f;
            _depthCamera.aspect = 1.0f;
            _depthCamera.nearClipPlane = 0.3f;
            _depthCamera.farClipPlane = 100f;

            // Depth texture setup
            _depthCamera.clearFlags = CameraClearFlags.SolidColor;
            _depthCamera.backgroundColor = Color.black;
            _depthCamera.depthTextureMode = DepthTextureMode.Depth;

            // Create and assign depth texture
            int textureSize = Mathf.RoundToInt(_gridSize);
            depthTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.Depth);
            depthTexture.Create();
            _depthCamera.targetTexture = depthTexture;
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

        private void InitMaterialProperties() {
            // Calculate world dimensions
            Vector4 gridSizeVec = new Vector4(_gridSize, _gridSize, 0, 0);
            Vector3 worldOrigin = transform.position;
            worldOrigin.y = 0;

            // Update shader properties
            fogMaterial.SetVector("_GridSize", gridSizeVec);
            fogMaterial.SetVector("_WorldOrigin", worldOrigin);
        }

        private void UpdateMaterialProperties() {
            if (_depthCamera == null || depthTexture == null) {
                Debug.LogError("Fog Render: Depth camera or texture not initialized!");
                return;
            }

            // Calculate and set view-projection inverse matrix
            Matrix4x4 viewMatrix = _depthCamera.worldToCameraMatrix;
            Matrix4x4 projectionMatrix = _depthCamera.projectionMatrix;
            Matrix4x4 viewProjectionMatrix = projectionMatrix * viewMatrix;
            Matrix4x4 invViewProjMatrix = viewProjectionMatrix.inverse;

            // Update shader properties
            fogMaterial.SetMatrix("_InvViewProjMatrix", invViewProjMatrix);
            fogMaterial.SetTexture("_DepthTex", depthTexture);
            fogMaterial.SetTexture("_FogTex", fogTexture);
        }

        private void UpdateFogTexture() {
            int packedLen = _chunkSize * _chunkSize / 4;
            int[] lightMapData = new int[packedLen];

            for (int i = 0; i < packedLen; i++)
            {
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

        void OnDestroy() {
            _lightMapBuffer?.Release();
        }
    }
}
