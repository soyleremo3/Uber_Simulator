using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeliverySim.EditorTools
{
    /// <summary>
    /// Builds one big, single, continuous city entirely out of Tirgames "StylizedWorld"
    /// (per the user's explicit direction: this asset should be the map's primary
    /// architecture — see the plan's Context). Buildings come from
    /// <see cref="TirgamesBuildingGenerator"/>; streets from the same
    /// <c>M02Road01_1</c> flat road tile probed and verified in "17 - Probe" (no
    /// rotation-correctness risk: the tile is direction-agnostic and perfectly flat,
    /// confirmed by spawning a real intersection and inspecting a screenshot — unlike
    /// Kenney's CityKitRoads, which needed per-tile rotation bookkeeping).
    ///
    /// Grid module: every building lot AND every street cell is exactly 4.5m — the
    /// architecture kit's own native module size — so lots and streets tile together
    /// with zero gap math or guessed spacing. A lot at grid (col,row) occupies
    /// X:[col*9, col*9+4.5] Z:[row*9, row*9+4.5]; the 4.5m gap on every side is a
    /// street cell, including a perimeter ring road around the whole grid.
    /// </summary>
    public static class TirgamesCitySetup
    {
        private const string RoadsFolder = "Assets/TirgamesAssets/StylizedWorld/Architecture/Prefabs";
        private const string CityRootName = "TirgamesCity";
        private const string BuildingsRootName = "Buildings";
        private const string RoadsRootName = "Roads";
        private const string WaypointRootName = "_CityWaypoints";
        // M02Road01_1's flat mesh sits 0.1m BELOW its own pivot (measured bounds:
        // min.y=-0.1, size.y=0) — placing the transform at y=0 alone puts the visible
        // surface underneath the shared ground Plane, invisible. RoadMeshPivotOffset
        // corrects for that; RoadYEpsilon then lifts it slightly above the Plane too.
        private const float RoadMeshPivotOffset = 0.1f;
        private const float RoadYEpsilon = 0.02f;

        private const float Lot = TirgamesBuildingGenerator.ModuleSize; // 4.5m
        private const float Spacing = Lot * 2f;                        // 9m: one lot + one street cell
        private const int Columns = 12;
        private const int Rows = 9;
        // 12x9 = 108 buildings — the "large, real-city-feel" size the user picked
        // over the medium (~2x previous) and small (~24-30) options.

        [MenuItem("DeliverySim/Setup/23 - Build Tirgames City (Büyük Şehir)")]
        public static void BuildCity()
        {
            if (GameObject.Find(CityRootName) != null)
            {
                Debug.Log("[Setup] '" + CityRootName + "' zaten sahnede — tekrar oluşturulmadı.");
                return;
            }

            int roadLayer = DowntownMapSetup.EnsureRoadLayerExists();
            if (roadLayer < 0)
            {
                Debug.LogError("[Setup] 'Road' layer'ı için boş bir Layer slotu bulunamadı — elle bir slot boşalt.");
                return;
            }

            GameObject cityRoot = new GameObject(CityRootName);
            Undo.RegisterCreatedObjectUndo(cityRoot, "Build Tirgames City");

            GameObject buildingsRoot = new GameObject(BuildingsRootName);
            buildingsRoot.transform.SetParent(cityRoot.transform, false);

            GameObject roadsRoot = new GameObject(RoadsRootName);
            roadsRoot.transform.SetParent(cityRoot.transform, false);

            var rng = new System.Random(20260821); // deterministic — re-running (after a manual delete) regenerates identically

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    Vector3 lotPosition = new Vector3(col * Spacing, 0f, row * Spacing);
                    float facing = 90f * rng.Next(4); // which side the door faces — pure visual variety, doesn't affect street access
                    int seed = row * 1000 + col;
                    TirgamesBuildingGenerator.BuildBuilding(buildingsRoot.transform, $"Bldg_{row}_{col}", lotPosition, facing, seed);
                }
            }

            int roadTileCount = PlaceRoadGrid(roadsRoot.transform, roadLayer);
            int waypointCount = BuildWaypointGraph(cityRoot.transform);

            Selection.activeGameObject = cityRoot;
            EditorUtility.SetDirty(cityRoot);
            Debug.Log($"[Setup] Tirgames şehri kuruldu: {Columns * Rows} bina ({Columns}x{Rows} ızgara), " +
                      $"{roadTileCount} yol tile'ı (Road layer + collider), {waypointCount} waypoint. " +
                      "Sahneyi kaydet (Ctrl+S).");
        }

        [MenuItem("DeliverySim/Setup/24 - Remove Tirgames City")]
        public static void RemoveCity()
        {
            GameObject root = GameObject.Find(CityRootName);
            if (root == null)
            {
                Debug.Log("[Setup] '" + CityRootName + "' sahnede yok — silinecek bir şey yok.");
                return;
            }

            Undo.DestroyObjectImmediate(root);
            Debug.Log("[Setup] '" + CityRootName + "' kaldırıldı. Sahneyi kaydet (Ctrl+S).");
        }

        /// <summary>
        /// Half-cell grid: building lots sit at EVEN (halfX,halfZ) — halfX=2*col,
        /// halfZ=2*row, matching their own corner-pivot placement exactly (world X =
        /// halfX*Lot). Every OTHER half-cell in the same range is a street cell. Since
        /// M02Road01_1 is CENTER-pivoted (measured bounds: min=(-2.25,..,-2.25), i.e.
        /// symmetric — unlike the corner-pivoted Architecture pieces), a street cell's
        /// tile center sits at halfX*Lot + Lot/2. This single pass naturally places one
        /// uniform flat tile on every straight run AND every intersection (both
        /// directions overlap on the same half-cells there), plus a perimeter ring
        /// road, with zero risk of duplicate/misaligned tiles from separate passes.
        /// </summary>
        private static int PlaceRoadGrid(Transform parent, int roadLayer)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoadsFolder}/M02Road01_1.prefab");
            if (prefab == null)
            {
                Debug.LogError($"[Setup] '{RoadsFolder}/M02Road01_1.prefab' bulunamadı — yol döşenemedi.");
                return 0;
            }

            int maxHalfX = 2 * Columns - 1; // one past the last building column's even index
            int maxHalfZ = 2 * Rows - 1;
            int count = 0;

            for (int halfZ = -1; halfZ <= maxHalfZ; halfZ++)
            {
                for (int halfX = -1; halfX <= maxHalfX; halfX++)
                {
                    bool isBuildingCell = halfX % 2 == 0 && halfZ % 2 == 0 && halfX >= 0 && halfZ >= 0;
                    if (isBuildingCell)
                    {
                        continue;
                    }

                    Vector3 pos = new Vector3(halfX * Lot + Lot / 2f, RoadMeshPivotOffset + RoadYEpsilon, halfZ * Lot + Lot / 2f);
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    instance.name = $"Road_{halfX}_{halfZ}";
                    instance.transform.localPosition = pos;
                    instance.layer = roadLayer;

                    BoxCollider collider = instance.AddComponent<BoxCollider>();
                    // tile is center-pivoted in X/Z; Y is offset down to the visual mesh surface (see RoadMeshPivotOffset)
                    collider.center = new Vector3(0f, -RoadMeshPivotOffset, 0f);
                    collider.size = new Vector3(Lot, 0.1f, Lot);

                    Undo.RegisterCreatedObjectUndo(instance, "Build Tirgames City Roads");
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// One waypoint per street intersection (every column-aisle x row-aisle
        /// crossing, including the perimeter ring) — matches the same aisle geometry
        /// PlaceRoadGrid used, so the graph always lines up with the real streets.
        /// Straight-run mid-points don't need their own nodes; RouteManager only needs
        /// graph connectivity between turns, not a node every 4.5m.
        /// </summary>
        private static int BuildWaypointGraph(Transform cityRoot)
        {
            GameObject root = new GameObject(WaypointRootName);
            root.transform.SetParent(cityRoot, false);

            var grid = new Waypoint[Rows + 2, Columns + 2]; // -1..Rows, -1..Columns, offset by 1 for array indexing
            for (int row = -1; row <= Rows; row++)
            {
                for (int col = -1; col <= Columns; col++)
                {
                    float x = col * Spacing - Lot / 2f;
                    float z = row * Spacing - Lot / 2f;
                    GameObject go = new GameObject($"CW_{row}_{col}");
                    go.transform.SetParent(root.transform, false);
                    go.transform.localPosition = new Vector3(x, 0.15f, z);
                    grid[row + 1, col + 1] = go.AddComponent<Waypoint>();
                }
            }

            int linkCount = 0;
            for (int row = 0; row <= Rows + 1; row++)
            {
                for (int col = 0; col <= Columns + 1; col++)
                {
                    if (grid[row, col] == null)
                    {
                        continue;
                    }

                    if (col + 1 <= Columns + 1 && grid[row, col + 1] != null)
                    {
                        grid[row, col].neighbors.Add(grid[row, col + 1]);
                        linkCount++;
                    }

                    if (row + 1 <= Rows + 1 && grid[row + 1, col] != null)
                    {
                        grid[row, col].neighbors.Add(grid[row + 1, col]);
                        linkCount++;
                    }
                }
            }

            EditorUtility.SetDirty(root);
            return (Rows + 2) * (Columns + 2);
        }
    }
}
