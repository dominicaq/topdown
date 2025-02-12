using UnityEngine;
using FogOfWar;
using System.Collections.Generic;
using System.Diagnostics;

public class FogManager : MonoBehaviour
{
    [Header("Chunk Properties")]
    public int tileSize = 1;
    public int maxHeight = 32;
    public int chunkSize = 64;
    private float _halfChunkSize;

    [Header("Chunk Data")]
    public TileData[,] lightMap;
    private Vector2Int chunkCenter;

    [Header("Debugging")]
    public bool drawGrid = true;

    // TEMP Debug (circular moving agent)
    private float angle = 0.0f;
    private Vector3 circularPosition;

    [Header("Shadow Casting")]
    private int[,] _transforms = {
        { 0,  1,  1,  0 },  // North
        { 0, -1, -1,  0 },  // South
        { -1,  0,  0, -1 }, // West
        { 1,  0,  0,  1 }   // East
    };
    private Stack<(int, float, float)> rowStack;

    void Start() {
        _halfChunkSize = chunkSize * 0.5f;

        // Initialize the grid of TileData
        lightMap = new TileData[chunkSize, chunkSize];
        rowStack = new Stack<(int, float, float)>(64);
        InitGrid();

        // chunkCenter = new Vector2Int(chunkSize / 2, chunkSize / 2);
    }

    void Update() {
        float radius = 10.0f;
        float speed = 2.0f;

        angle += speed * Time.deltaTime;

        float x = radius * Mathf.Cos(angle);
        float z = radius * Mathf.Sin(angle);

        circularPosition = new Vector3(x, 0, z);

        ResetFog(0);
        for (int i = 0; i < 1; i++) {
            RevealFog(circularPosition, 25);
        }
    }

    /*
    * Fog Chunk Management
    */
    private void InitGrid() {
        int numChunks = 4;
        int chunkGridSize = chunkSize / numChunks;
        RaycastHit[] hits = new RaycastHit[1];

        for (int chunkX = 0; chunkX < numChunks; chunkX++) {
            for (int chunkZ = 0; chunkZ < numChunks; chunkZ++) {
                InitChunk(chunkX, chunkZ, chunkGridSize, hits);
            }
        }
    }

    private void InitChunk(int chunkX, int chunkZ, int chunkGridSize, RaycastHit[] hits) {
        for (int x = 0; x < chunkGridSize; x++) {
            int globalX = chunkX * chunkGridSize + x;
            float worldX = (globalX - _halfChunkSize + 0.5f) * tileSize;

            for (int z = 0; z < chunkGridSize; z++) {
                int globalZ = chunkZ * chunkGridSize + z;
                float worldZ = (globalZ - _halfChunkSize + 0.5f) * tileSize;

                byte tileHeight = 0;
                Vector3 origin = new Vector3(worldX, 100f, worldZ);
                if (Physics.RaycastNonAlloc(origin, Vector3.down, hits, 200f) > 0) {
                    tileHeight = (byte)Mathf.Clamp((int)hits[0].point.y, 0, maxHeight);
                }

                lightMap[globalX, globalZ] = new TileData {
                    Height = tileHeight,
                    Visible = false,
                    Seen = false
                };
            }
        }
    }

    /*
    * Fog User Side
    */
    public void ResetFog(int chunkIdx) {
        // TODO: Reset by chunk idx
        for (int x = 0; x < chunkSize; x++) {
            for (int z = 0; z < chunkSize; z++) {
                lightMap[x, z].Visible = false;
            }
        }
    }

    // Source: https://www.albertford.com/shadowcasting/
    public void RevealFog(Vector3 center, int radius) {
        int centerX = Mathf.FloorToInt(center.x / tileSize) + chunkSize / 2;
        int centerY = Mathf.FloorToInt(center.z / tileSize) + chunkSize / 2;

        if (!IsInBounds(centerX, centerY))
            return;

        lightMap[centerX, centerY].Visible = true;
        lightMap[centerX, centerY].Seen = true;
        int centerHeight = lightMap[centerX, centerY].Height;

        Vector3Int origin = new Vector3Int(centerX, centerY, centerHeight);
        for (int octant = 0; octant < 4; octant++) {
            CastLight(octant , ref origin, radius);
        }
    }

    /*
    * Shadow Casting
    */
    private void CastLight(int quadrant, ref Vector3Int origin, int radius) {
        rowStack.Push((1, -1f, 1f));

        int xx = _transforms[quadrant, 0];
        int xy = _transforms[quadrant, 1];
        int yx = _transforms[quadrant, 2];
        int yy = _transforms[quadrant, 3];

        while (rowStack.Count > 0) {
            var (depth, startSlope, endSlope) = rowStack.Pop();
            // Adjust the column calculations to prevent gaps
            int minCol = Mathf.FloorToInt(depth * startSlope);  // Changed from CeilToInt
            int maxCol = Mathf.CeilToInt(depth * endSlope);     // Changed from FloorToInt

            int prevTx = -1, prevTy = -1;

            for (int col = minCol; col <= maxCol; col++) {
                int dx = depth * xx + col * xy;
                int dy = depth * yx + col * yy;
                int tx = origin.x + dx;
                int ty = origin.y + dy;

                int distSquared = dx * dx + dy * dy;
                if (!IsInBounds(tx, ty) || (distSquared > radius * radius)) {
                    continue;
                }

                lightMap[tx, ty].Visible = true;
                lightMap[tx, ty].Seen = true;

                bool isWall = IsBlocking(tx, ty, origin.z);
                bool wasWall = prevTx != -1 && IsBlocking(prevTx, prevTy, origin.z);

                // Add small overlap in slope calculations to prevent gaps
                if (wasWall && !isWall) {
                    startSlope = GetSlope(depth, col - 0.1f);
                }
                if (!wasWall && isWall) {
                    rowStack.Push((depth + 1, startSlope, GetSlope(depth, col + 0.1f)));
                }

                prevTx = tx;
                prevTy = ty;
            }

            if (prevTx != -1 && prevTy != -1 && !IsBlocking(prevTx, prevTy, origin.z)) {
                rowStack.Push((depth + 1, startSlope, endSlope));
            }
        }
    }

    private float GetSlope(int depth, float col) {
        return (2f * col - 1) / (2f * depth);
    }

    private bool IsBlocking(int x, int y, int height) {
        return lightMap[x, y].Height > height;
    }

    private bool IsInBounds(int x, int y) {
        return x >= 0 && x < chunkSize && y >= 0 && y < chunkSize;
    }

    /*
    * Debug
    */
    private void OnDrawGizmos() {
        if (lightMap == null || !drawGrid) {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(circularPosition, 0.5f);
        for (int x = 0; x < chunkSize; x++) {
            for (int z = 0; z < chunkSize; z++) {
                float height = lightMap[x, z].Height;
                Vector3 position = new Vector3(
                    (x - chunkSize / 2 + 0.5f) * tileSize,
                    height,
                    (z - chunkSize / 2 + 0.5f) * tileSize
                );

                // Set the tile color based on visibility
                if (lightMap[x, z].Visible) {
                    Gizmos.color = Color.red;
                } else if (lightMap[x, z].Seen) {
                    Gizmos.color = Color.gray; // Previously seen but not currently visible
                } else {
                    Gizmos.color = Color.black; // Never seen
                }

                Gizmos.DrawWireCube(position, new Vector3(tileSize, 0.1f, tileSize));
            }
        }
    }
}



    // private Vector2Int WorldToTilePosition(Vector3 worldPosition)
    // {
    //     // Convert the world position to tile coordinates by dividing by tileSize
    //     int x = Mathf.FloorToInt(worldPosition.x / tileSize) + chunkSize / 2;
    //     int z = Mathf.FloorToInt(worldPosition.z / tileSize) + chunkSize / 2;
    //     Debug.Log("WORLD POS:" + worldPosition);
    //     // Clamp the coordinates to ensure they are within the grid bounds
    //     x = Mathf.Clamp(x, 0, chunkSize - 1);
    //     z = Mathf.Clamp(z, 0, chunkSize - 1);

    //     Debug.Log("TILE POS:" + new Vector2Int(x,z));
    //     return new Vector2Int(x, z);
    // }
