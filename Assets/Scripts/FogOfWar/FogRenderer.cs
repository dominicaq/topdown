using UnityEngine;

namespace FogOfWar
{
    public class FogRenderer : MonoBehaviour
    {
        public int upscaleFactor = 4;
        public ComputeShader fogComputeShader;
        public RenderTexture fogTexture;

        private ComputeBuffer _lightMapBuffer;
        private int _chunkSize;
        private int _kernelHandle;
        private FogManager _fogManager;

        void Start() {
            _fogManager = GetComponent<FogManager>();
            _chunkSize = _fogManager.chunkSize;

            int upscaledSize = _chunkSize * upscaleFactor;

            // Create RenderTexture with transparency support
            fogTexture = new RenderTexture(upscaledSize, upscaledSize, 0, RenderTextureFormat.ARGB32);
            fogTexture.enableRandomWrite = true;
            fogTexture.Create();

            // Create ComputeBuffer for lightmap data (packed byte data)
            _lightMapBuffer = new ComputeBuffer(_chunkSize * _chunkSize, sizeof(byte));

            // Get kernel
            _kernelHandle = fogComputeShader.FindKernel("CSMain");

            // Set shader variables
            fogComputeShader.SetInt("_chunkSize", _chunkSize);
            fogComputeShader.SetInt("upscaleFactor", upscaleFactor);
            fogComputeShader.SetTexture(_kernelHandle, "Result", fogTexture);
        }

        private void Update() {
            // Convert lightMap to a flat byte array and send updated lightmap data to GPU
            byte[] lightMapData = new byte[_chunkSize * _chunkSize];
            for (int y = 0; y < _chunkSize; y++) {
                for (int x = 0; x < _chunkSize; x++) {
                    byte tileData = _fogManager.lightMap[x, y].GetPackedData();
                    lightMapData[y * _chunkSize + x] = tileData;
                }
            }

            _lightMapBuffer.SetData(lightMapData);
            fogComputeShader.SetBuffer(_kernelHandle, "LightMap", _lightMapBuffer);

            // Dispatch compute shader
            int threadGroupsX = Mathf.CeilToInt(fogTexture.width / 8.0f);
            int threadGroupsY = Mathf.CeilToInt(fogTexture.height / 8.0f);
            fogComputeShader.Dispatch(_kernelHandle, threadGroupsX, threadGroupsY, 1);
        }

        void OnDestroy() {
            // Release buffers
            _lightMapBuffer?.Release();
        }
    }
}
