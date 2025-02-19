using UnityEngine;
using FogOfWar;
using System.Collections.Generic;

public class FogManager : MonoBehaviour
{
    [Header("Chunk Properties")]
    public int tileSize = 1;
    public int maxHeight = 32;
    public int chunkSize = 64;
    private int _halfChunkSize;

    [Header("Chunk Data")]
    public TileData[,] lightMap;
    private RaycastHit[] _rayHits = new RaycastHit[3];

    [Header("Debugging")]
    public bool drawGrid = true;
    private float angle = 0.0f;
    private Vector3 circularPosition;

    [Header("Shadow Casting")]
    public float colBuffer = 0.2f;
    private int[,] _transforms = {
        { 0,  1,  1,  0 },  // North
        { 0, -1, -1,  0 },  // South
        { -1,  0,  0, -1 }, // West
        { 1,  0,  0,  1 }   // East
    };

    void Start() {
        // Initialize the grid of TileData
        _halfChunkSize = chunkSize / 2;
        lightMap = new TileData[chunkSize, chunkSize];
        CreateChunk(0, 0);

        // chunkCenter = new Vector2Int(chunkSize / 2, chunkSize / 2);
    }

    void Update() {
        // DEBUG
        float radius = 10.0f;
        float speed = 2.0f;

        angle += speed * Time.deltaTime;

        float x = radius * Mathf.Cos(angle);
        float z = radius * Mathf.Sin(angle);

        circularPosition = new Vector3(x, 0, z);
        // END OF DEBUG

        ResetFog(0);
        RevealFog(circularPosition, 50);
    }

    // TODO: not tested
    private Vector2Int WorldToTilePosition(Vector3 worldPosition) {
        // Convert the world position to tile coordinates by dividing by tileSize
        int x = Mathf.FloorToInt(worldPosition.x / tileSize) + chunkSize / 2;
        int z = Mathf.FloorToInt(worldPosition.z / tileSize) + chunkSize / 2;
        Debug.Log("WORLD POS:" + worldPosition);
        // Clamp the coordinates to ensure they are within the grid bounds
        x = Mathf.Clamp(x, 0, chunkSize - 1);
        z = Mathf.Clamp(z, 0, chunkSize - 1);

        Debug.Log("TILE POS:" + new Vector2Int(x,z));
        return new Vector2Int(x, z);
    }

    /*
    * Fog Chunk Management
    */
    private void CreateChunk(int chunkX, int chunkZ) {
        for (int x = 0; x < chunkSize; x++) {
            for (int z = 0; z < chunkSize; z++) {
                // Calculate global tile coordinates
                int globalX = chunkX * chunkSize + x;
                int globalZ = chunkZ * chunkSize + z;

                // Calculate tile center position
                Vector3 tilePos = new Vector3(
                    (globalX - _halfChunkSize + 0.5f) * tileSize,
                    maxHeight,
                    (globalZ - _halfChunkSize + 0.5f) * tileSize
                );

                // Calculate tile height
                byte tileHeight = 0;
                int hits = Physics.RaycastNonAlloc(tilePos, Vector3.down, _rayHits);
                if (hits > 0) {
                    float closestDistance = float.MaxValue;
                    float closestHeight = 0f;

                    for (int i = 0; i < hits; i++) {
                        if (_rayHits[i].distance < closestDistance) {
                            closestDistance = _rayHits[i].distance;
                            closestHeight = _rayHits[i].point.y;
                        }
                    }

                    tileHeight = (byte)Mathf.Clamp(closestHeight, 0f, maxHeight);
                }

                // Initialize tile data
                lightMap[x, z] = new TileData {
                    Visible = false,
                    Seen = false,
                    Height = tileHeight
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
        Stack<(int, float, float)> rows = new Stack<(int, float, float)>();
        rows.Push((1, -1f, 1f)); // Start row (depth = 1, slopes = [-1, 1])

        int xx = _transforms[quadrant, 0];
        int xy = _transforms[quadrant, 1];
        int yx = _transforms[quadrant, 2];
        int yy = _transforms[quadrant, 3];

        while (rows.Count > 0) {
            var (depth, startSlope, endSlope) = rows.Pop();
            int minCol = Mathf.CeilToInt(depth * startSlope);
            int maxCol = Mathf.FloorToInt(depth * endSlope);

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
                if (wasWall && !isWall) {
                    startSlope = GetSlope(depth, col - colBuffer);
                }
                if (!wasWall && isWall) {
                    rows.Push((depth + 1, startSlope, GetSlope(depth, col + colBuffer)));
                }

                prevTx = tx;
                prevTy = ty;
            }

            if (prevTx != -1 && prevTy != -1 && !IsBlocking(prevTx, prevTy, origin.z)) {
                rows.Push((depth + 1, startSlope, endSlope));
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
    * Debugging
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
                    // Visible
                    Gizmos.color = Color.clear;
                } else if (lightMap[x, z].Seen) {
                    // Visited but not visible
                    Gizmos.color = Color.gray;
                } else {
                    // Never seen
                    Gizmos.color = Color.black;
                }

                Gizmos.DrawWireCube(position, new Vector3(tileSize, 0.1f, tileSize));
            }
        }
    }
}
