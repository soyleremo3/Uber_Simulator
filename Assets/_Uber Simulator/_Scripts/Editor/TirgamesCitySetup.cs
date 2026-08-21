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

            for (int row = 0; row < Rows; row++)
            {
                for (int col = 0; col < Columns; col++)
                {
                    Vector3 lotPosition = new Vector3(col * Spacing, 0f, row * Spacing);
                    int seed = row * 1000 + col;
                    TirgamesBuildingGenerator.BuildBuilding(buildingsRoot.transform, $"Bldg_{row}_{col}", lotPosition, seed);
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

        // Building lots chosen for spread across the 12x9 grid (id, anchor building).
        // Anchored to real placed buildings (Bldg_row_col, from BuildCity) so points
        // sit at an actual lot center — always within ~3.2m of an adjacent street
        // (every lot has a road cell on all 4 sides), well inside VehicleInteractor's
        // 6m interact radius.
        private static readonly (string objectName, string pointId, string anchorBuilding)[] CityPickups =
        {
            ("City_Pickup_RestaurantA", "pickup_restaurant_a", "Bldg_1_1"),
            ("City_Pickup_RestaurantB", "pickup_restaurant_b", "Bldg_7_9"),
            ("City_Pickup_DepotA", "pickup_depot_a", "Bldg_1_9"),
            ("City_Pickup_DepotB", "pickup_depot_b", "Bldg_7_1"),
        };

        private static readonly (string objectName, string pointId, string anchorBuilding)[] CityDeliveries =
        {
            ("City_Delivery_HouseA", "delivery_house_a", "Bldg_0_5"),
            ("City_Delivery_HouseB", "delivery_house_b", "Bldg_3_0"),
            ("City_Delivery_HouseC", "delivery_house_c", "Bldg_3_11"),
            ("City_Delivery_HouseD", "delivery_house_d", "Bldg_5_3"),
            ("City_Delivery_HouseE", "delivery_house_e", "Bldg_8_5"),
            ("City_Delivery_OfficeA", "delivery_office_a", "Bldg_4_5"),
            ("City_Delivery_OfficeB", "delivery_office_b", "Bldg_2_7"),
        };

        /// <summary>
        /// Adds 4 pickup + 7 delivery points + 1 fuel + 1 repair spread across the
        /// city, moves the player vehicle to a clean road intersection, and creates
        /// an order pool pairing them (picking the longest available spreads — this
        /// map's max corner-to-corner distance is ~135m, still short of
        /// OrderManager's ~198m distance-based-timer floor, but meaningfully longer
        /// than any single-block order, and real streets connect every pair now that
        /// it's one continuous grid instead of two separate districts).
        /// </summary>
        [MenuItem("DeliverySim/Setup/25 - Populate City Gameplay Content")]
        public static void PopulateGameplayContent()
        {
            GameObject cityRoot = GameObject.Find(CityRootName);
            if (cityRoot == null)
            {
                Debug.LogError("[Setup] '" + CityRootName + "' sahnede yok — önce '23 - Build Tirgames City' çalıştır.");
                return;
            }

            Transform buildingsRoot = cityRoot.transform.Find(BuildingsRootName);

            // Clean road intersection: aisle between building columns 2/3, south perimeter ring.
            Vector3 spawn = new Vector3(2 * Spacing + Lot / 2f + Lot, 0f, -Lot / 2f);
            MoveIfExists("PlayerVeichle Car", spawn);
            MoveIfExists("PlayerVehicle", spawn);

            DeliverySimSetup.EnsureFolder(DeliverySimSetup.DataFolderRoot);

            var pickupWorldPos = new Dictionary<string, Vector3>();
            var deliveryWorldPos = new Dictionary<string, Vector3>();

            foreach (var p in CityPickups)
            {
                if (TryGetLotCenter(buildingsRoot, p.anchorBuilding, out Vector3 pos))
                {
                    DeliverySimSetup.CreatePoint<PickupPoint>(p.objectName, p.pointId, pos);
                    pickupWorldPos[p.pointId] = pos;
                }
            }

            foreach (var d in CityDeliveries)
            {
                if (TryGetLotCenter(buildingsRoot, d.anchorBuilding, out Vector3 pos))
                {
                    DeliverySimSetup.CreatePoint<DeliveryPoint>(d.objectName, d.pointId, pos);
                    deliveryWorldPos[d.pointId] = pos;
                }
            }

            if (TryGetLotCenter(buildingsRoot, "Bldg_6_5", out Vector3 fuelPos))
            {
                DeliverySimSetup.CreateStation<FuelStation>("City_FuelStation", fuelPos);
            }

            if (TryGetLotCenter(buildingsRoot, "Bldg_2_3", out Vector3 repairPos))
            {
                DeliverySimSetup.CreateStation<RepairStation>("City_RepairStation", repairPos);
            }

            var orderSpecs = new (string id, string name, string pickup, string delivery, float payment, float time, CargoType cargo)[]
            {
                ("order_city_1", "Restoran A'dan Ev C'ye", "pickup_restaurant_a", "delivery_house_c", 280f, 130f, CargoType.Food),
                ("order_city_2", "Depo B'den Ev A'ya", "pickup_depot_b", "delivery_house_a", 270f, 125f, CargoType.Package),
                ("order_city_3", "Restoran B'den Ev B'ye", "pickup_restaurant_b", "delivery_house_b", 275f, 130f, CargoType.Food),
                ("order_city_4", "Depo A'dan Ofis A'ya", "pickup_depot_a", "delivery_office_a", 220f, 100f, CargoType.Fragile),
                ("order_city_5", "Restoran A'dan Ofis B'ye", "pickup_restaurant_a", "delivery_office_b", 210f, 95f, CargoType.Food),
                ("order_city_6", "Depo B'den Ev E'ye", "pickup_depot_b", "delivery_house_e", 215f, 100f, CargoType.Package),
                ("order_city_7", "Restoran B'den Ev D'ye", "pickup_restaurant_b", "delivery_house_d", 200f, 90f, CargoType.Food),
                ("order_city_8", "Depo A'dan Ev C'ye", "pickup_depot_a", "delivery_house_c", 160f, 60f, CargoType.Fragile),
                ("order_city_9", "Restoran A'dan Ev D'ye", "pickup_restaurant_a", "delivery_house_d", 230f, 105f, CargoType.Food),
                ("order_city_10", "Depo B'den Ofis B'ye", "pickup_depot_b", "delivery_office_b", 205f, 95f, CargoType.Package),
            };

            var orders = new List<OrderData>();
            foreach (var spec in orderSpecs)
            {
                orders.Add(DeliverySimSetup.CreateOrderAsset(spec.id, spec.name, spec.pickup, spec.delivery, spec.payment, spec.time, spec.cargo));
            }

            OrderManager orderManager = Object.FindFirstObjectByType<OrderManager>();
            if (orderManager != null)
            {
                var so = new SerializedObject(orderManager);
                SerializedProperty pool = so.FindProperty("orderPool");
                pool.ClearArray();
                for (int i = 0; i < orders.Count; i++)
                {
                    pool.InsertArrayElementAtIndex(i);
                    pool.GetArrayElementAtIndex(i).objectReferenceValue = orders[i];
                }

                so.ApplyModifiedProperties();
            }
            else
            {
                Debug.LogWarning("[Setup] Sahnede OrderManager yok — önce '1 - Create Managers' çalıştır, sonra bunu tekrarla.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Şehir gameplay içeriği hazır: {CityPickups.Length} alım + {CityDeliveries.Length} teslim noktası, " +
                      "1 yakıt, 1 tamir, 10 sipariş. Araç temiz bir kavşağa taşındı. Sahneyi kaydet (Ctrl+S).");
        }

        private static bool TryGetLotCenter(Transform buildingsRoot, string buildingName, out Vector3 worldPosition)
        {
            Transform building = buildingsRoot.Find(buildingName);
            if (building == null)
            {
                Debug.LogWarning($"[Setup] '{buildingName}' Buildings altında bulunamadı — o nokta atlandı.");
                worldPosition = Vector3.zero;
                return false;
            }

            // Building pivot is at the lot's corner; the lot center is +2.25 in both X/Z.
            worldPosition = building.position + new Vector3(-Lot / 2f, 0f, Lot / 2f);
            return true;
        }

        private static void MoveIfExists(string objectName, Vector3 position)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                return;
            }

            Undo.RecordObject(go.transform, "Move To City Spawn");
            Vector3 pos = go.transform.position;
            pos.x = position.x;
            pos.z = position.z;
            go.transform.position = pos;
            EditorUtility.SetDirty(go.transform);
        }
    }
}
