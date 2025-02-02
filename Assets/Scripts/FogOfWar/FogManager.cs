using UnityEngine;
using FogOfWar;
using System.Collections.Generic;
using System.Diagnostics;

public class FogManager : MonoBehaviour
{
    [Header("Chunk Properties")]
    public float tileSize = 1.0f;
    public int chunkSize = 64;

    [Header("Chunk Data")]
    public TileData[,] lightMap;
    private Vector2Int chunkCenter;

    void Start() {
        // Initialize the grid of TileData
        lightMap = new TileData[chunkSize, chunkSize];
        InitGrid();

        // Perfomance check (will cause the game to freeze for a bit)
        stopwatch = new Stopwatch();
        // TestPerformance();

        // chunkCenter = new Vector2Int(chunkSize / 2, chunkSize / 2);
    }

    // TEMP Debug
    private float angle = 0.0f;
    private Vector3 circularPosition;

    void Update() {
        float radius = 10.0f;
        float speed = 2.0f;

        angle += speed * Time.deltaTime;

        float x = radius * Mathf.Cos(angle);
        float z = radius * Mathf.Sin(angle);

        circularPosition = new Vector3(x, 0, z);

        ResetFog(0);
        RevealFog(circularPosition, 16);
    }

    /*
    * Performance Check
    */
    public int maxAgents = 1000;
    private Stopwatch stopwatch;
    void TestPerformance()
    {
        for (int agentCount = 1; agentCount <= maxAgents; agentCount++)
        {
            // Start the timer
            stopwatch.Reset();
            stopwatch.Start();

            // Simulate the agents calling the RevealFog function
            for (int i = 0; i < agentCount; i++)
            {
                RevealFog(circularPosition, 16);
            }

            // Stop the timer and log the result
            stopwatch.Stop();
            // UnityEngine.Debug.Log($"Tested {agentCount} agents. Time taken: {stopwatch.ElapsedMilliseconds} ms");

            // You can decide based on the elapsed time when performance degrades
            if (stopwatch.ElapsedMilliseconds > 100) // Example threshold for "substantial drop"
            {
                UnityEngine.Debug.LogWarning($"Performance drops significantly after {agentCount} agents.");
                break; // Stop testing after performance drops substantially
            }
        }
    }

    /*
    * Fog Chunk Management
    */
    private void InitGrid() {
        int numChunks = 4;
        int chunkGridSize = chunkSize / numChunks;

        for (int chunkX = 0; chunkX < numChunks; chunkX++) {
            for (int chunkZ = 0; chunkZ < numChunks; chunkZ++) {
                InitChunk(chunkX, chunkZ, chunkGridSize);
            }
        }
    }

    private void InitChunk(int chunkX, int chunkZ, int chunkGridSize) {
        for (int x = 0; x < chunkGridSize; x++) {
            for (int z = 0; z < chunkGridSize; z++) {
                // Calculate global tile coordinates
                int globalX = chunkX * chunkGridSize + x;
                int globalZ = chunkZ * chunkGridSize + z;

                byte tileHeight = 0;

                // Perform a raycast from the tile's center downwards to create a discrete heightmap
                Vector3 tileCenter = new Vector3((globalX - chunkSize / 2) * tileSize, 100f, (globalZ - chunkSize / 2) * tileSize);
                if (Physics.Raycast(tileCenter, Vector3.down, out RaycastHit hit)) {
                    float height = Mathf.Clamp(Mathf.Floor(hit.point.y), 0f, 32f);
                    tileHeight = (byte)height;
                }

                lightMap[globalX, globalZ] = new TileData {
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

        // Transformation matrix
        int[,] transforms = {
            {  0,  1,  1,  0 },  // North
            {  0, -1, -1,  0 },  // South
            {  -1,  0,  0, -1 }, // West
            {  1,  0,  0,  1 }   // East
        };

        int xx = transforms[quadrant, 0];
        int xy = transforms[quadrant, 1];
        int yx = transforms[quadrant, 2];
        int yy = transforms[quadrant, 3];

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
                if (!IsInBounds(tx, ty) || (distSquared > radius * radius))
                    continue;

                lightMap[tx, ty].Visible = true;
                lightMap[tx, ty].Seen = true;

                bool isWall = IsBlocking(tx, ty, origin.z);
                bool wasWall = (prevTx != -1 && IsBlocking(prevTx, prevTy, origin.z));
                if (wasWall && !isWall) {
                    startSlope = GetSlope(depth, col);
                }
                if (!wasWall && isWall) {
                    rows.Push((depth + 1, startSlope, GetSlope(depth, col)));
                }

                prevTx = tx;
                prevTy = ty;
            }

            if (prevTx != -1 && prevTy != -1 && !IsBlocking(prevTx, prevTy, origin.z)) {
                rows.Push((depth + 1, startSlope, endSlope));
            }
        }
    }

    private float GetSlope(int depth, int col) {
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
        if (lightMap == null) {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawSphere(circularPosition, 0.5f);
        for (int x = 0; x < chunkSize; x++) {
            for (int z = 0; z < chunkSize; z++) {
                float height = lightMap[x, z].Height;
                Vector3 position = new Vector3((x - chunkSize / 2) * tileSize, height, (z - chunkSize / 2) * tileSize);

                // Set the Gizmos color based on visibility
                if (lightMap[x, z].Visible) {
                    Gizmos.color = Color.clear;
                } else if (lightMap[x, z].Seen) {
                    Gizmos.color = Color.gray; // Previously seen but not currently visible
                } else {
                    Gizmos.color = Color.green; // Never seen
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
