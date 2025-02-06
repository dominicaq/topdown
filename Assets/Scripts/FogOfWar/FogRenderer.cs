using UnityEngine;
using UnityEngine.Rendering;

namespace FogOfWar
{
    public class FogRenderer : MonoBehaviour
    {
        [Header("Properties")]
        public int upscaleFactor = 4;
        public Material fogMaterial;

        [Header("Compute Shader In/Out")]
        public ComputeShader fogComputeShader;
        public RenderTexture fogTexture;
        public RenderTexture depthTexture;
        private int _kernelHandle;
        private ComputeBuffer _lightMapBuffer;

        [Header("Dimensions")]
        private int _tileSize;
        private int _chunkSize;

        [Header("Components")]
        private FogManager _fogManager;
        private Camera _depthCamera;

        void Start()
        {
            _fogManager = GetComponent<FogManager>();
            if (!_fogManager) {
                Debug.LogError("FogManager component missing from GameObject.");
                return;
            }

            _chunkSize = _fogManager.chunkSize;
            _tileSize = _fogManager.tileSize;

            // Compute grid size
            float gridSize = _chunkSize * _tileSize;
            InitDepthTexture(gridSize);
            InitFogTexture(gridSize);

            // Set up material properties
            UpdateMaterialProperties();
        }

        private void InitFogTexture(float gridSize) {
            int textureSize = Mathf.RoundToInt(gridSize);
            fogTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            fogTexture.enableRandomWrite = true;
            fogTexture.Create();

            // Create compute shader variables and buffer
            _lightMapBuffer = new ComputeBuffer(_chunkSize * _chunkSize / 4, sizeof(int));
            fogComputeShader.SetInt("_chunkSize", _chunkSize);
            fogComputeShader.SetInt("upscaleFactor", upscaleFactor);
            fogComputeShader.SetInt("tileSize", _tileSize);

            _kernelHandle = fogComputeShader.FindKernel("CSMain");
            fogComputeShader.SetTexture(_kernelHandle, "Result", fogTexture);
        }

        private void InitDepthTexture(float gridSize) {
            _depthCamera = GetComponent<Camera>();
            if (!_depthCamera) {
                Debug.LogWarning("Fog Renderer requires an orthographic depth camera.");
                return;
            }

            // Enable depth texture for world position reconstruction
            _depthCamera.clearFlags = CameraClearFlags.SolidColor;
            _depthCamera.backgroundColor = Color.black;
            _depthCamera.depthTextureMode = DepthTextureMode.Depth;

            // Ensure the RenderTexture resolution matches the grid exactly
            int textureSize = Mathf.RoundToInt(gridSize);
            depthTexture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.Depth);
            _depthCamera.targetTexture = depthTexture;

            // Correct orthographic size
            _depthCamera.orthographicSize = gridSize * 0.5f;
            _depthCamera.aspect = 1.0f;
        }

        private void UpdateMaterialProperties()
        {
            // Calculate world dimensions
            Vector2 chunkSize = new Vector2(_chunkSize * _tileSize, _chunkSize * _tileSize);
            Vector3 worldOrigin = transform.position;
            worldOrigin.y = 0;

            // Update shader properties
            fogMaterial.SetVector("_ChunkSize", chunkSize);
            fogMaterial.SetVector("_WorldOrigin", worldOrigin);

            // Calculate and set view-projection inverse matrix
            Matrix4x4 viewMatrix = _depthCamera.worldToCameraMatrix;
            Matrix4x4 projectionMatrix = _depthCamera.projectionMatrix;
            Matrix4x4 viewProjectionMatrix = projectionMatrix * viewMatrix;

            fogMaterial.SetMatrix("unity_MatrixInvVP", viewProjectionMatrix.inverse);
            fogMaterial.SetTexture("_DepthTex", depthTexture);
            fogMaterial.SetTexture("_FogTex", fogTexture);
        }

        private void Update()
        {
            // Update material properties every frame to handle camera movement
            UpdateMaterialProperties();

            // Update fog texture
            UpdateFogTexture();
        }

        void UpdateFogTexture()
        {
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
            fogComputeShader.SetBuffer(_kernelHandle, "LightMap", _lightMapBuffer);

            int threadGroupsX = Mathf.CeilToInt(fogTexture.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(fogTexture.height / 8.0f);
            fogComputeShader.Dispatch(_kernelHandle, threadGroupsX, threadGroupsY, 1);
        }

        void OnDestroy()
        {
            _lightMapBuffer?.Release();
        }
    }
}
