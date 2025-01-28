using UnityEngine;
using FogOfWar;

public class FogOfWarManager : MonoBehaviour
{
    [Header("Fog Data")]
    public RenderTexture fogTexture;
    readonly private static int[,] octants = {
        { 1, 0 },    // Right
        { 1, 1 },    // Bottom-right
        { 0, 1 },    // Bottom
        { -1, 1 },   // Bottom-left
        { -1, 0 },   // Left
        { -1, -1 },  // Top-left
        { 0, -1 },   // Top
        { 1, -1 }    // Top-right
    };

    [Header("Chunk Properties")]
    public float tileSize = 1.0f;
    public int chunkSize = 64;

    [Header("Chunk Data")]
    private TileData[,] lightMap;
    private Vector2Int chunkCenter;

    void Start() {
        // Initialize the grid of TileData
        lightMap = new TileData[chunkSize, chunkSize];
        InitGrid();

        // chunkCenter = new Vector2Int(chunkSize / 2, chunkSize / 2);
    }

    private float angle = 0.0f;

    void Update() {
        float radius = 10.0f;
        float speed = 2.0f;

        angle += speed * Time.deltaTime;

        float x = radius * Mathf.Cos(angle);
        float z = radius * Mathf.Sin(angle);

        Vector3 circularPosition = new Vector3(x, 0, z);

        ResetFog(0);
        RevealFog(circularPosition, 32);
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

    // Source: https://www.adammil.net/blog/v125_Roguelike_Vision_Algorithms.html#shadowcode
    public void RevealFog(Vector3 center, int radius) {
        int centerX = Mathf.FloorToInt(center.x / tileSize) + chunkSize / 2;
        int centerY = Mathf.FloorToInt(center.z / tileSize) + chunkSize / 2;

        if (!IsInBounds(centerX, centerY))
            return;

        // Reveal the center tile first
        lightMap[centerX, centerY].Visible = true;
        int centerHeight = lightMap[centerX, centerY].Height;

        Vector3Int origin = new Vector3Int(centerX, centerY, centerHeight);
        for (uint octant = 0; octant < 8; octant++) {
            CastLight(octant, ref origin, radius, 1 , new Slope(1, 1), new Slope(0, 1));
        }
    }

    /*
    * Shadow Casting
    */
    struct Slope {
        public int x, y;
        public Slope(int x, int y) {
            this.x = x;
            this.y = y;
        }
    };

    private void CastLight(uint octant, ref Vector3Int origin, int radius, int x, Slope top, Slope bottom) {
        for (; (uint)x <= radius; x++) {
            int topY = (top.x == 0 || top.x * 2 == 0) ? x : ((x * 2 + 1) * top.y + top.x - 1) / (top.x * 2);
            int bottomY = (bottom.x == 0 || bottom.x * 2 == 0) ? 0 : ((x * 2 - 1) * bottom.y + bottom.x) / (bottom.x * 2);

            int wasBlocked = -1;
            for (int y = topY; y >= bottomY; y--) {
                int tx = origin.x, ty = origin.y;
                switch(octant) {
                    case 0: tx += x; ty -= y; break;
                    case 1: tx += y; ty -= x; break;
                    case 2: tx -= y; ty -= x; break;
                    case 3: tx -= x; ty -= y; break;
                    case 4: tx -= x; ty += y; break;
                    case 5: tx -= y; ty += x; break;
                    case 6: tx += y; ty += x; break;
                    case 7: tx += x; ty += y; break;
                }

                int dx = tx - origin.x;
                int dy = ty - origin.y;
                int currDist = dx * dx + dy * dy;
                bool inRange = IsInBounds(tx, ty) && currDist <= radius * radius;
                if(inRange) {
                    // NOTE: use the next line instead if you want the algorithm to be symmetrical
                    // if(inRange && (y != topY || top.Y*x >= top.X*y) && (y != bottomY || bottom.Y*x <= bottom.X*y)) SetVisible(tx, ty);
                    lightMap[tx, ty].Visible = true;
                }
            }
        }
    }

    private bool IsInBounds(int x, int y) {
        return x >= 0 && x < chunkSize && y >= 0 && y < chunkSize;
    }

    // Reveals tiles in a circular radius around a center point (raycasting)
    // private void RevealFog(Vector3 center, int radius)
    // {
    //     // Convert world position to grid coordinates by factoring in the tile size
    //     int centerX = Mathf.FloorToInt(center.x / tileSize) + chunkSize / 2;
    //     int centerZ = Mathf.FloorToInt(center.z / tileSize) + chunkSize / 2;
    //     int maxHeight = lightMap[centerX, centerZ].Height;

    //     int sectors = 8;
    //     for (int i = 0; i < sectors; i++)
    //     {
    //         // Calculate the start and end angles for this sector
    //         float startAngle = Mathf.Deg2Rad * (i * (360 / sectors)); // each sector's starting angle
    //         float endAngle = Mathf.Deg2Rad * ((i + 1) * (360 / sectors)); // each sector's ending angle

    //         // Iterate through each tile in the sector range
    //         for (float angle = startAngle; angle < endAngle; angle += 0.05f) // granularity: step through each direction
    //         {
    //             float dx = Mathf.Cos(angle);
    //             float dz = Mathf.Sin(angle);

    //             for (int distance = 1; distance <= radius; distance++)
    //             {
    //                 // Calculate target tile position
    //                 int tileX = centerX + Mathf.FloorToInt(dx * distance);
    //                 int tileZ = centerZ + Mathf.FloorToInt(dz * distance);

    //                 // Ensure the tile is within bounds
    //                 if (tileX >= 0 && tileX < chunkSize && tileZ >= 0 && tileZ < chunkSize)
    //                 {
    //                     ref TileData targetTile = ref lightMap[tileX, tileZ];

    //                     // Only reveal tile if it's lower than or equal to the maxHeight encountered
    //                     if (targetTile.Height <= maxHeight)
    //                     {
    //                         targetTile.Visible = true;
    //                     }
    //                     else
    //                     {
    //                         // Block view if a taller tile is encountered
    //                         break;
    //                     }

    //                     // Update maxHeight with the current tile's height
    //                     maxHeight = Mathf.Max(maxHeight, targetTile.Height);
    //                 }
    //             }
    //         }
    //     }
    // }

    // Debug
    private void OnDrawGizmos()
    {
        if (lightMap == null) return;

        // Loop through all tiles in the chunk
        for (int x = 0; x < chunkSize; x++)
        {
            for (int z = 0; z < chunkSize; z++)
            {
                // Calculate the world position of each tile
                float height = lightMap[x, z].Height;
                Vector3 position = new Vector3((x - chunkSize / 2) * tileSize, height, (z - chunkSize / 2) * tileSize);

                // Set the Gizmos color based on visibility
                Gizmos.color = lightMap[x, z].Visible ? Color.clear : Color.green;
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
