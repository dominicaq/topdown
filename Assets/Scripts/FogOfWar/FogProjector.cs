using FogOfWar;
using UnityEngine;

public class FogProjector : MonoBehaviour
{
    public Shader projectionShader;
    public Texture2D projectionTexture; // The texture to project
    public Camera projectorCamera; // The camera used for projection
    private RenderTexture _renderTarget; // The RenderTexture to hold the projected result

    private Material _projectorMaterial; // Material to use for projection
    private FogRenderer _fogRenderer;

    private void Start()
    {
        // Find the FogRenderer component attached to the same GameObject
        _fogRenderer = GetComponent<FogRenderer>();
        if (_fogRenderer == null)
        {
            Debug.LogError("FogRenderer component not found on this GameObject.");
            enabled = false;
            return;
        }

        // Get the fogTexture from the FogRenderer
        _renderTarget = _fogRenderer.fogTexture;

        // Create a new material with the projector shader
        _projectorMaterial = new Material(projectionShader);
        _projectorMaterial.SetTexture("_MainTex", projectionTexture);
    }

    private void Update()
    {
        if (projectionTexture != null && projectorCamera != null && _renderTarget != null)
        {
            // Create the projector matrix using the camera's projection and world-to-camera matrices
            Matrix4x4 projectorMatrix = projectorCamera.projectionMatrix * projectorCamera.worldToCameraMatrix;

            // Set the projector matrix in the shader
            _projectorMaterial.SetMatrix("_ProjectorMatrix", projectorMatrix);

            // Render the texture using the projector camera into the RenderTexture
            projectorCamera.targetTexture = _renderTarget;
            projectorCamera.Render();
            projectorCamera.targetTexture = null;

            // The fogTexture is automatically updated, so no need to manually assign it
        }
    }

    private void OnRenderObject()
    {
        // Optionally apply the RenderTexture globally (e.g., for visual debugging or other purposes)
        if (_renderTarget != null)
        {
            Graphics.Blit(_renderTarget, null as RenderTexture);
        }
    }
}
