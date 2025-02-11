using UnityEngine;

namespace FogOfWar
{
    public class FogRenderer : MonoBehaviour
    {
        [Header("Shader & Materials")]
        public Material fogMaterial;
        public ComputeShader fogCompute;

        [Header("Fog Settings")]
        public int upscaleFactor = 4;

        [Header("Compute Shader Buffers")]
        private ComputeBuffer _lightMapBuffer;
        private int _kernelMain;
        private int _kernelUpscale;

        [Header("Texture Output")]
        public RenderTexture _fogTexture;
        private RenderTexture _baseTexture; // Added: Base resolution texture

        [Header("Dimensions")]
        private int _tileSize;
        private int _chunkSize;
        private float _gridSize;

        [Header("Components")]
        private FogManager _fogManager;
        private Camera _projector;

        private void Start() {
            InitComponents();
            InitProjectorCamera();
            InitFogTextures();
            InitComputeShader();
        }

        private void Update() {
            UpdateFogTexture();
            UpdateMaterialProperties();
        }

        private void InitComponents() {
            _fogManager = GetComponent<FogManager>();
            if (!_fogManager) {
                Debug.LogError("FogManager component missing from GameObject.");
                return;
            }

            _projector = GetComponent<Camera>();
            if (!_projector) {
                Debug.LogError("FogRenderer requires a Camera component.");
                return;
            }

            _chunkSize = _fogManager.chunkSize;
            _tileSize = _fogManager.tileSize;
            _gridSize = _chunkSize * _tileSize;
        }

        private void InitProjectorCamera() {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _projector.enabled = true;
            _projector.orthographic = true;
            _projector.orthographicSize = _gridSize * 0.5f;
            _projector.aspect = 1.0f;
            _projector.nearClipPlane = 0.3f;
            _projector.farClipPlane = 100f;

            _projector.targetTexture = new RenderTexture(1, 1, 0);
            _projector.cullingMask = 0;
            _projector.clearFlags = CameraClearFlags.Nothing;
        }

        private void InitFogTextures() {
            int baseSize = Mathf.RoundToInt(_gridSize);
            int upscaleSize = baseSize * upscaleFactor;

            // Create base resolution texture
            _baseTexture = new RenderTexture(baseSize, baseSize, 0, RenderTextureFormat.ARGB32);
            _baseTexture.enableRandomWrite = true;
            _baseTexture.Create();

            // Create upscaled texture
            _fogTexture = new RenderTexture(upscaleSize, upscaleSize, 0, RenderTextureFormat.ARGB32);
            _fogTexture.enableRandomWrite = true;
            _fogTexture.Create();
        }

        private void InitComputeShader() {
            _lightMapBuffer = new ComputeBuffer(_chunkSize * _chunkSize / 4, sizeof(int));

            _kernelMain = fogCompute.FindKernel("CSMain");
            _kernelUpscale = fogCompute.FindKernel("UpscaleFogTexture");

            fogCompute.SetInt("_chunkSize", _chunkSize);
            fogCompute.SetTexture(_kernelMain, "FogTex", _baseTexture);
            fogCompute.SetTexture(_kernelUpscale, "InputTex", _baseTexture);
            fogCompute.SetTexture(_kernelUpscale, "OutputTex", _fogTexture);

            int upscaleSize = _chunkSize * upscaleFactor;
            fogCompute.SetInts("_upscaleSize", upscaleSize, upscaleSize);
            fogCompute.SetFloats("_scaleFactor", upscaleFactor, upscaleFactor);
        }

        private void UpdateMaterialProperties()
        {
            Camera mainCam = Camera.main;
            if (!mainCam) return;

            Matrix4x4 mainVP = GL.GetGPUProjectionMatrix(mainCam.projectionMatrix, false) * mainCam.worldToCameraMatrix;
            fogMaterial.SetMatrix("_InvViewProjMatrix", mainVP.inverse);

            Matrix4x4 projectorVP = GL.GetGPUProjectionMatrix(_projector.projectionMatrix, false) * _projector.worldToCameraMatrix;
            fogMaterial.SetMatrix("_ProjectorVP", projectorVP);

            fogMaterial.SetTexture("_DepthTex", Shader.GetGlobalTexture("_CameraDepthTexture"));
            fogMaterial.SetTexture("_FogTex", _fogTexture);
        }

        private void UpdateFogTexture()
        {
            // Previous implementation remains the same for updating light map data
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
            fogCompute.SetBuffer(_kernelMain, "LightMap", _lightMapBuffer);

            // Dispatch main kernel to write to base texture
            int threadGroupsX = Mathf.CeilToInt(_baseTexture.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(_baseTexture.height / 8.0f);
            fogCompute.Dispatch(_kernelMain, threadGroupsX, threadGroupsY, 1);

            // Dispatch upscale kernel to write to final texture
            int upscaleThreadsX = Mathf.CeilToInt(_fogTexture.width / 8.0f);
            int upscaleThreadsY = Mathf.CeilToInt(_fogTexture.height / 8.0f);
            fogCompute.Dispatch(_kernelUpscale, upscaleThreadsX, upscaleThreadsY, 1);
        }

        private void OnDestroy() {
            _lightMapBuffer?.Release();
            if (_baseTexture != null) {
                _baseTexture.Release();
                _baseTexture = null;
            }
            if (_fogTexture != null) {
                _fogTexture.Release();
                _fogTexture = null;
            }
        }
    }
}
