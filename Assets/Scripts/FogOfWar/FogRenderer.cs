using UnityEngine;

namespace FogOfWar
{
    public class FogRenderer : MonoBehaviour
    {
        [Header("Properties")]
        public int upscaleFactor = 4;

        [Header("Compute Shader In/Out")]
        public ComputeShader fogComputeShader;
        public RenderTexture fogTexture;
        private int _kernelHandle;
        private ComputeBuffer _lightMapBuffer;

        [Header("Dimensons")]
        private int _tileSize;
        private int _chunkSize;
        private FogManager _fogManager;

        void Start() {
            _fogManager = GetComponent<FogManager>();
            _chunkSize = _fogManager.chunkSize;
            _tileSize = _fogManager.tileSize;

            // Adjust upscaled size based on dimensons
            int textureSize = _tileSize * upscaleFactor* _chunkSize;

            // Create RenderTexture with transparency support
            fogTexture = new RenderTexture(textureSize, textureSize, 0, RenderTextureFormat.ARGB32);
            fogTexture.enableRandomWrite = true;
            fogTexture.Create();

            // Create ComputeBuffer for packed lightmap data (storing 4 bytes per int)
            _lightMapBuffer = new ComputeBuffer(_chunkSize * _chunkSize / 4, sizeof(int));
            // Set shader variables
            fogComputeShader.SetInt("_chunkSize", _chunkSize);
            fogComputeShader.SetInt("upscaleFactor", upscaleFactor);
            fogComputeShader.SetInt("tileSize", _tileSize);

            _kernelHandle = fogComputeShader.FindKernel("CSMain");
            fogComputeShader.SetTexture(_kernelHandle, "Result", fogTexture);
        }

        private void Update() {
            // Each int holds 4 tiles
            int packedLen = (_chunkSize * _chunkSize) / 4;
            int[] lightMapData = new int[packedLen];

            for (int i = 0; i < packedLen; i++) {
                int baseIndex = i * 4;

                // Convert 1D index to 2D coordinates
                int x = baseIndex % _chunkSize;
                int y = baseIndex / _chunkSize;

                // Fetch bytes (no need for bounds checks since chunkSize is even)
                byte b1 = _fogManager.lightMap[x, y].GetPackedData();
                byte b2 = _fogManager.lightMap[x + 1, y].GetPackedData();
                byte b3 = _fogManager.lightMap[x + 2, y].GetPackedData();
                byte b4 = _fogManager.lightMap[x + 3, y].GetPackedData();

                // Pack into a single int
                lightMapData[i] = b1 | (b2 << 8) | (b3 << 16) | (b4 << 24);
            }

            _lightMapBuffer.SetData(lightMapData);
            fogComputeShader.SetBuffer(_kernelHandle, "LightMap", _lightMapBuffer);

            int threadGroupsX = Mathf.CeilToInt(fogTexture.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(fogTexture.height / 8.0f);
            fogComputeShader.Dispatch(_kernelHandle, threadGroupsX, threadGroupsY, 1);
        }

        void OnDestroy() {
            _lightMapBuffer?.Release();
        }
    }
}
