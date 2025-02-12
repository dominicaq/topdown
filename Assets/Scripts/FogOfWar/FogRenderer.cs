using UnityEngine;

namespace FogOfWar
{
    public class FogRenderer : MonoBehaviour
    {
        [Header("Shader & Materials")]
        public Material fogMaterial;
        public ComputeShader fogCompute;

        [Header("Fog Settings")]
        public float lerpSpeed = 1.0f;
        public int upscaleFactor = 1;
        public float blurStrength = 3.0f;

        [Header("Compute Shader Buffers")]
        private ComputeBuffer _lightMapBuffer;
        private int _kernelMain;
        private int _kernelUpscale;
        private int _kernelHorizontalBlur;
        private int _kernelVerticalBlur;
        private int _kernelLerp;

        [Header("Texture Output")]
        public RenderTexture _fogTexture;
        public RenderTexture _prevFogTexture;
        private RenderTexture _baseTexture; // Original low res texture
        private RenderTexture _blurTemp;

        [Header("Dimensions")]
        private int _tileSize;
        private int _chunkSize;

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
        }

        private void InitProjectorCamera() {
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            _projector.enabled = true;
            _projector.orthographic = true;

            _projector.orthographicSize = _chunkSize * _tileSize * 0.5f;
            _projector.aspect = 1.0f;
            _projector.nearClipPlane = 0.3f;
            _projector.farClipPlane = 100f;

            _projector.targetTexture = new RenderTexture(1, 1, 0);
            _projector.cullingMask = 0;
            _projector.clearFlags = CameraClearFlags.Nothing;
        }

        private void InitFogTextures() {
            int baseSize = _chunkSize;
            int finalSize = baseSize * _tileSize * upscaleFactor;

            // Create base resolution texture (chunk-level)
            _baseTexture = new RenderTexture(baseSize, baseSize, 0, RenderTextureFormat.ARGB32);
            _baseTexture.enableRandomWrite = true;
            _baseTexture.Create();

            // Create upscaled texture (final resolution with tiles)
            _fogTexture = new RenderTexture(finalSize, finalSize, 0, RenderTextureFormat.ARGB32);
            _fogTexture.enableRandomWrite = true;
            _fogTexture.Create();

            // Create blur temporary texture at final resolution
            _blurTemp = new RenderTexture(finalSize, finalSize, 0, RenderTextureFormat.ARGB32);
            _blurTemp.enableRandomWrite = true;
            _blurTemp.Create();

            // Create previous fog texture at final resolution
            _prevFogTexture = new RenderTexture(finalSize, finalSize, 0, RenderTextureFormat.ARGB32);
            _prevFogTexture.enableRandomWrite = true;
            _prevFogTexture.Create();
        }

        private void InitComputeShader() {
            _lightMapBuffer = new ComputeBuffer(_chunkSize * _chunkSize / 4, sizeof(int));

            _kernelMain = fogCompute.FindKernel("CSMain");
            _kernelUpscale = fogCompute.FindKernel("UpscaleFogTex");
            _kernelHorizontalBlur = fogCompute.FindKernel("HorizontalBlur");
            _kernelVerticalBlur = fogCompute.FindKernel("VerticalBlur");
            _kernelLerp = fogCompute.FindKernel("LerpPass");

            fogCompute.SetInt("_chunkSize", _chunkSize);
            fogCompute.SetInt("_tileSize", _tileSize);
            fogCompute.SetTexture(_kernelMain, "FogTex", _baseTexture);

            // Calculate final dimensions including tile size
            int finalSize = _chunkSize * _tileSize * upscaleFactor;

            // Set upscale parameters using single values
            fogCompute.SetInt("_upscaleSize", finalSize);
            fogCompute.SetFloat("_scaleFactor", _tileSize * upscaleFactor);

            fogCompute.SetTexture(_kernelUpscale, "InputTex", _baseTexture);
            fogCompute.SetTexture(_kernelUpscale, "OutputTex", _fogTexture);

            // Setup blur passes
            fogCompute.SetTexture(_kernelHorizontalBlur, "OutputTex", _fogTexture);
            fogCompute.SetTexture(_kernelHorizontalBlur, "BlurTemp", _blurTemp);
            fogCompute.SetTexture(_kernelVerticalBlur, "OutputTex", _fogTexture);
            fogCompute.SetTexture(_kernelVerticalBlur, "BlurTemp", _blurTemp);
            fogCompute.SetFloat("_blurStrength", blurStrength);

            // Setup lerp pass
            fogCompute.SetTexture(_kernelLerp, "FogTex", _fogTexture);
            fogCompute.SetTexture(_kernelLerp, "PrevFogTex", _prevFogTexture);
            fogCompute.SetFloat("_lerpSpeed", lerpSpeed);
        }

        private void UpdateMaterialProperties() {
            Camera mainCam = Camera.main;
            if (!mainCam) return;

            Matrix4x4 mainVP = GL.GetGPUProjectionMatrix(mainCam.projectionMatrix, false) * mainCam.worldToCameraMatrix;
            fogMaterial.SetMatrix("_InvViewProjMatrix", mainVP.inverse);

            Matrix4x4 projectorVP = GL.GetGPUProjectionMatrix(_projector.projectionMatrix, false) * _projector.worldToCameraMatrix;
            fogMaterial.SetMatrix("_ProjectorVP", projectorVP);

            fogMaterial.SetTexture("_DepthTex", Shader.GetGlobalTexture("_CameraDepthTexture"));
            fogMaterial.SetTexture("_FogTex", _fogTexture);

            fogCompute.SetFloat("_deltaTime", Time.deltaTime);
            fogCompute.SetFloat("_lerpSpeed", lerpSpeed);
        }

        private void UpdateFogTexture() {
            // Store the current fog texture to the previous texture
            Graphics.Blit(_fogTexture, _prevFogTexture);

            int packedLen = _chunkSize * _chunkSize / 4;
            int[] lightMapData = new int[packedLen];

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
            fogCompute.SetBuffer(_kernelMain, "LightMap", _lightMapBuffer);

            // Dispatch main kernel to write to base texture
            int threadGroupsX = Mathf.CeilToInt(_baseTexture.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(_baseTexture.height / 8.0f);
            fogCompute.Dispatch(_kernelMain, threadGroupsX, threadGroupsY, 1);

            // Dispatch upscale kernel to write to final texture
            int upscaleThreadsX = Mathf.CeilToInt(_fogTexture.width / 8.0f);
            int upscaleThreadsY = Mathf.CeilToInt(_fogTexture.height / 8.0f);
            fogCompute.Dispatch(_kernelUpscale, upscaleThreadsX, upscaleThreadsY, 1);

            // Apply blur
            fogCompute.Dispatch(_kernelHorizontalBlur, Mathf.CeilToInt(_fogTexture.width / 256f), _fogTexture.height, 1);
            fogCompute.Dispatch(_kernelVerticalBlur, _fogTexture.width, Mathf.CeilToInt(_fogTexture.height / 256f), 1);

            // Dispatch the lerp kernel (blending current and previous fog textures)
            int lerpThreadsX = Mathf.CeilToInt(_fogTexture.width / 8.0f);
            int lerpThreadsY = Mathf.CeilToInt(_fogTexture.height / 8.0f);
            fogCompute.Dispatch(_kernelLerp, lerpThreadsX, lerpThreadsY, 1);
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
            if (_blurTemp != null) {
                _blurTemp.Release();
                _blurTemp = null;
            }
            if (_prevFogTexture != null) {
                _prevFogTexture.Release();
                _prevFogTexture = null;
            }
        }
    }
}
