using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliverySim.EditorTools
{
    /// <summary>
    /// Imports the "Stylized Downtown Street" asset's own hand-built street block
    /// (roads, pavement, buildings, props — the "WalkingStreet" root from its hero
    /// demo scene) into our MainScene, then lays a Waypoint road-graph on top of the
    /// real road tile positions so RouteManager's GPS line follows the streets
    /// instead of cutting diagonally across the map. Menu items are idempotent like
    /// the rest of DeliverySimSetup — safe to re-run.
    /// </summary>
    public static class DowntownMapSetup
    {
        private const string StreetScenePath = "Assets/TirgamesAssets/StylizedWorld/Locations/Urban/Scenes/StylizedStreet.unity";
        private const string MapRootName = "DowntownStreet";
        private const string WaypointRootName = "_RoadWaypoints";
        private const string RoadLayerName = "Road";

        // Local positions of every road tile (straight + wide "cross" segments) in the
        // vendor's own "WalkingStreet" demo, extracted from that scene's Roads group.
        // This is the exact, already-correctly-socketed road network — reused as-is so
        // the waypoint graph lines up with the real road meshes with zero guesswork.
        private static readonly Vector3[] RoadTilePositions =
        {
            new Vector3(11.25f, 0f, -36f),
            new Vector3(2.25f, 0f, -36f),
            new Vector3(27f, 0f, 15.75f),
            new Vector3(-15.75f, 0f, -36f),
            new Vector3(29.25f, 0f, -27f),
            new Vector3(29.25f, 0f, -18f),
            new Vector3(29.25f, 0f, 22.5f),
            new Vector3(29.25f, 0f, 9f),
            new Vector3(29.25f, 0f, 0f),
            new Vector3(-6.75f, 0f, -36f),
            new Vector3(20.25f, 0f, -36f),
            new Vector3(29.25f, 0f, -9f),
            new Vector3(29.25f, 0f, -36f),
            new Vector3(29.25f, 0f, 31.5f),
            new Vector3(31.5f, 0f, 15.75f),
            new Vector3(-22.5f, 0f, -33.75f),
            new Vector3(-22.5f, 0f, -38.25f),
            new Vector3(-29.25f, 0f, -36f),
            new Vector3(-31.5f, 0f, 15.75f),
            new Vector3(-29.25f, 0f, -27f),
            new Vector3(-29.25f, 0f, -18f),
            new Vector3(-29.25f, 0f, 22.5f),
            new Vector3(-29.25f, 0f, 9f),
            new Vector3(-29.25f, 0f, 0f),
            new Vector3(-29.25f, 0f, -9f),
            new Vector3(-29.25f, 0f, 31.5f),
            new Vector3(-27f, 0f, 15.75f),
            new Vector3(11.25f, 0f, 40.5f),
            new Vector3(2.25f, 0f, 40.5f),
            new Vector3(-15.75f, 0f, 40.5f),
            new Vector3(-6.75f, 0f, 40.5f),
            new Vector3(20.25f, 0f, 40.5f),
            new Vector3(-22.5f, 0f, 42.75f),
            new Vector3(-22.5f, 0f, 38.25f),
            new Vector3(-29.25f, 0f, 40.5f),
            new Vector3(29.25f, 0f, 40.5f),
        };

        // Tiles are 4.5m (straight) or 9m (cross) square, edge-to-edge on a 9m grid at
        // the loop; 9.6 catches every real neighbor without jumping across the block.
        private const float NeighborLinkDistance = 9.6f;
        private const float WaypointHeight = 0.15f;

        // ------------------------------------------------------------------
        /// <summary>
        /// Full reset for a from-scratch map rebuild: removes every map-related object
        /// (both the Tirgames Downtown clones and the Kenney district from the previous
        /// iteration, plus their road/waypoint graphs), every gameplay point (pickup/
        /// delivery/fuel/repair — they'd be orphaned anyway once their host districts are
        /// gone), clears OrderManager's pool (nothing left for those orders to resolve
        /// against), and deletes the now-meaningless OrderData assets from disk. Leaves
        /// _Managers/_Gameplay/_UI, the player vehicle, cameras, and the ground Plane
        /// untouched — those aren't "map content".
        /// </summary>
        [MenuItem("DeliverySim/Setup/22 - Clear Entire Map (Sıfırdan Başla)")]
        public static void ClearEntireMap()
        {
            string[] rootsToDelete =
            {
                "DowntownStreet", "DowntownStreet_2", "DowntownStreet_3", "DowntownStreet_4",
                "KennyDistrict", "_RoadWaypoints", "_KennyRoadWaypoints", "_KennyRoadNetwork",
                "_KennyRoadProbe",
            };

            int destroyedRoots = 0;
            foreach (string name in rootsToDelete)
            {
                GameObject go = GameObject.Find(name);
                if (go != null)
                {
                    Undo.DestroyObjectImmediate(go);
                    destroyedRoots++;
                }
            }

            int destroyedPoints = 0;
            foreach (InteractionPoint point in Object.FindObjectsByType<InteractionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(point.gameObject);
                destroyedPoints++;
            }

            foreach (FuelStation station in Object.FindObjectsByType<FuelStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(station.gameObject);
                destroyedPoints++;
            }

            foreach (RepairStation station in Object.FindObjectsByType<RepairStation>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(station.gameObject);
                destroyedPoints++;
            }

            OrderManager orderManager = Object.FindFirstObjectByType<OrderManager>();
            if (orderManager != null)
            {
                var so = new SerializedObject(orderManager);
                SerializedProperty pool = so.FindProperty("orderPool");
                pool.ClearArray();
                so.ApplyModifiedProperties();
            }

            int deletedOrderAssets = 0;
            string[] orderGuids = AssetDatabase.FindAssets("t:OrderData", new[] { "Assets/_Uber Simulator/_Data/Orders" });
            foreach (string guid in orderGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.DeleteAsset(path))
                {
                    deletedOrderAssets++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] Harita sıfırlandı: {destroyedRoots} kök obje, {destroyedPoints} gameplay noktası, " +
                      $"{deletedOrderAssets} sipariş asset'i silindi, OrderManager.orderPool boşaltıldı. Yer/kamera/araç/manager'lara dokunulmadı. " +
                      "Sahneyi kaydet (Ctrl+S).");
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/9 - Import Downtown Street Map")]
        public static void ImportDowntownMap()
        {
            if (GameObject.Find(MapRootName) != null)
            {
                Debug.Log("[Setup] Downtown harita zaten sahnede — tekrar içeri aktarılmadı.");
                return;
            }

            Scene mainScene = SceneManager.GetActiveScene();
            Scene sourceScene = EditorSceneManager.OpenScene(StreetScenePath, OpenSceneMode.Additive);

            GameObject sourceRoot = sourceScene.GetRootGameObjects()
                .FirstOrDefault(go => go.name == "WalkingStreet");

            if (sourceRoot == null)
            {
                Debug.LogError("[Setup] Kaynak sahnede 'WalkingStreet' kök objesi bulunamadı — asset yapısı değişmiş olabilir.");
                EditorSceneManager.CloseScene(sourceScene, true);
                return;
            }

            SceneManager.MoveGameObjectToScene(sourceRoot, mainScene);
            sourceRoot.name = MapRootName;
            sourceRoot.transform.position = Vector3.zero;
            sourceRoot.transform.rotation = Quaternion.identity;

            EditorSceneManager.CloseScene(sourceScene, true);

            Undo.RegisterCreatedObjectUndo(sourceRoot, "Import Downtown Map");
            Selection.activeGameObject = sourceRoot;
            EditorSceneManager.MarkSceneDirty(mainScene);
            Debug.Log("[Setup] 'Stylized Downtown Street' haritası içeri aktarıldı: Roads, Pavement, StreetProps, HouseSet1-4, StreetLights, ReflectionProbes. Sahneyi kaydet (Ctrl+S).");
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/10 - Build Road Waypoint Graph")]
        public static void BuildRoadWaypoints()
        {
            if (GameObject.Find(WaypointRootName) != null)
            {
                Debug.Log("[Setup] Yol waypoint ağı zaten var — tekrar oluşturulmadı.");
                return;
            }

            GameObject root = new GameObject(WaypointRootName);
            Undo.RegisterCreatedObjectUndo(root, "Create Road Waypoints");

            var nodes = new List<Waypoint>(RoadTilePositions.Length);
            for (int i = 0; i < RoadTilePositions.Length; i++)
            {
                GameObject go = new GameObject("RW_" + i);
                go.transform.SetParent(root.transform, false);
                go.transform.position = RoadTilePositions[i] + Vector3.up * WaypointHeight;
                nodes.Add(go.AddComponent<Waypoint>());
            }

            int linkCount = 0;
            for (int i = 0; i < nodes.Count; i++)
            {
                for (int j = i + 1; j < nodes.Count; j++)
                {
                    float dist = Vector3.Distance(nodes[i].transform.position, nodes[j].transform.position);
                    if (dist <= NeighborLinkDistance)
                    {
                        nodes[i].neighbors.Add(nodes[j]);
                        linkCount++;
                    }
                }
            }

            EditorUtility.SetDirty(root);
            Selection.activeGameObject = root;
            Debug.Log($"[Setup] {nodes.Count} yol waypoint'i, {linkCount} komşuluk bağlantısıyla oluşturuldu. RouteManager artık mavi rotayı yola göre çizecek (çapraz kestirme yok). Sahneyi kaydet (Ctrl+S).");
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/11 - Place Gameplay Points On Downtown Map")]
        public static void PlacePointsOnMap()
        {
            MoveVehicle("PlayerVeichle Car", 29.25f, 0f);
            MoveVehicle("PlayerVehicle", 29.25f, 0f);

            MovePoint("Pickup_Restaurant", new Vector3(22f, 0f, -18f));
            MovePoint("Pickup_Depot", new Vector3(-22f, 0f, 9f));
            MovePoint("Delivery_HouseA", new Vector3(20f, 0f, 33.5f));
            MovePoint("Delivery_HouseB", new Vector3(-22f, 0f, -9f));
            MovePoint("Delivery_Office", new Vector3(-22f, 0f, 22.5f));
            MovePoint("FuelStation_Main", new Vector3(11f, 0f, -29f));
            MovePoint("RepairStation_Main", new Vector3(-16f, 0f, 33.5f));

            Debug.Log("[Setup] Araç ve sipariş/istasyon noktaları yeni Downtown haritasındaki kaldırımlara taşındı. Sahneyi kaydet (Ctrl+S).");
        }

        private static void MovePoint(string objectName, Vector3 position)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                Debug.LogWarning($"[Setup] '{objectName}' sahnede bulunamadı — taşınamadı.");
                return;
            }

            Undo.RecordObject(go.transform, "Place On Downtown Map");
            go.transform.position = position;
            EditorUtility.SetDirty(go.transform);
        }

        private static void MoveVehicle(string objectName, float x, float z)
        {
            GameObject go = GameObject.Find(objectName);
            if (go == null)
            {
                return; // PlayerVehicle/PlayerVeichle Car — only one of the two is expected active.
            }

            Undo.RecordObject(go.transform, "Place On Downtown Map");
            Vector3 pos = go.transform.position;
            pos.x = x;
            pos.z = z;
            go.transform.position = pos;
            EditorUtility.SetDirty(go.transform);
        }

        // ------------------------------------------------------------------
        // WalkingStreet is actually a CROSS-shaped intersection, not a plain straight
        // block: HouseSet1/2 flank a north-south street (they run along Z), HouseSet3/4
        // flank an east-west street (they run along X) — confirmed by reading every
        // HouseSet's actual placed bounds and its child buildings' roof-piece
        // rotations directly out of the vendor's own StylizedStreet.unity. Measured
        // "Roads" child bounds (the real drivable/walkable footprint, smaller than the
        // full building extent): X:[-33.75,33.75] Z:[-40.5,45].
        //
        // Cloning the WHOLE cross repeatedly in a symmetric star (equal spacing, every
        // block hanging directly off the landmark) read as mechanical copy-paste — a
        // real screenshot review called this out directly, correctly. A real city
        // doesn't grow as a perfectly mirrored 4-point star: streets branch off OTHER
        // streets, not always back to one center; block spacing varies; some streets
        // have secondary hubs. CityBlocks/CityLinks below is a hand-placed irregular
        // TREE, not a formula loop: most links connect two NON-landmark blocks (e.g.
        // "E1"->"NE1" branches a residential side-street off the commercial east
        // avenue), spacing varies per link (130-170m, not one constant), and "S1" is a
        // second small full-cross hub (not every block hides HouseSet3/4) so the map
        // doesn't read as "one center, N clones." Every block still shows the HouseSet
        // pair matching ITS OWN street direction (see BlockKind), same reasoning as
        // before — only the LAYOUT changed.
        private const float RoadsHalfX = 33.75f;
        private const float RoadsMinZ = -40.5f;
        private const float RoadsMaxZ = 45f;

        private const string WalkingStreetPrefabPath = "Assets/_Uber Simulator/Prefabs/WalkingStreetBlock.prefab";

        // Which HouseSet pair a block shows: NorthSouth (1/2, hides 3/4), EastWest
        // (3/4, hides 1/2), or Both (a secondary hub — needed wherever a block has
        // links running BOTH axes, e.g. S1 branches west to SW1 while also continuing
        // the landmark's own north-south avenue).
        private enum BlockKind { NorthSouth, EastWest, Both }

        private readonly struct BlockSpec
        {
            public readonly string Name;
            public readonly Vector3 Position;
            public readonly BlockKind Kind;

            public BlockSpec(string name, Vector3 position, BlockKind kind)
            {
                Name = name;
                Position = position;
                Kind = kind;
            }
        }

        private readonly struct LinkSpec
        {
            public readonly string From;
            public readonly string To;

            public LinkSpec(string from, string to)
            {
                From = from;
                To = to;
            }
        }

        // Irregular tree, hand-placed (not a spacing formula): landmark has 4 direct
        // neighbors, but two of THOSE (E1, S1) branch further on their own instead of
        // every block reporting straight back to the landmark — an actual network,
        // not a star.
        private static readonly BlockSpec[] CityBlocks =
        {
            new BlockSpec("E1", new Vector3(150f, 0f, 0f), BlockKind.EastWest),
            new BlockSpec("E2", new Vector3(280f, 0f, 0f), BlockKind.EastWest),
            new BlockSpec("NE1", new Vector3(150f, 0f, 140f), BlockKind.NorthSouth),
            new BlockSpec("W1", new Vector3(-160f, 0f, 0f), BlockKind.EastWest),
            new BlockSpec("S1", new Vector3(0f, 0f, -140f), BlockKind.Both), // secondary hub
            new BlockSpec("S2", new Vector3(0f, 0f, -290f), BlockKind.NorthSouth),
            new BlockSpec("SW1", new Vector3(-130f, 0f, -140f), BlockKind.EastWest),
            new BlockSpec("N1", new Vector3(0f, 0f, 170f), BlockKind.NorthSouth),
        };

        private static readonly LinkSpec[] CityLinks =
        {
            new LinkSpec(MapRootName, "E1"),
            new LinkSpec("E1", "E2"),
            new LinkSpec("E1", "NE1"),   // branches off E1, not the landmark
            new LinkSpec(MapRootName, "W1"),
            new LinkSpec(MapRootName, "S1"),
            new LinkSpec("S1", "S2"),
            new LinkSpec("S1", "SW1"),   // branches off S1, not the landmark
            new LinkSpec(MapRootName, "N1"),
        };

        [MenuItem("DeliverySim/Setup/12 - Expand Downtown Map (Kollar Ekle)")]
        public static void ExpandDowntownMap()
        {
            GameObject landmark = GameObject.Find(MapRootName);
            if (landmark == null)
            {
                Debug.LogError("[Setup] '" + MapRootName + "' sahnede yok — önce '9 - Import Downtown Street Map' çalıştır.");
                return;
            }

            // First run: turn the imported WalkingStreet into a real Prefab ASSET, and
            // reconnect the landmark to it. This is not just tidiness — instantiating
            // clones with Object.Instantiate on a plain scene hierarchy serialized
            // every one of WalkingStreet's ~10,000+ individual pieces into
            // MainScene.unity again per clone (hit 162 MB, over GitHub's 100 MB
            // limit, with the old 8-clone version). PrefabInstances store only the
            // prefab reference + any overrides, not the full hierarchy again.
            GameObject prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(WalkingStreetPrefabPath);
            if (prefabAsset == null)
            {
                prefabAsset = PrefabUtility.SaveAsPrefabAsset(landmark, WalkingStreetPrefabPath, out bool saved);
                if (!saved || prefabAsset == null)
                {
                    Debug.LogError("[Setup] WalkingStreet prefab'ı oluşturulamadı.");
                    return;
                }

                Debug.Log("[Setup] '" + WalkingStreetPrefabPath + "' prefab'ı oluşturuldu (landmark otomatik buna bağlandı).");
            }

            int built = 0;
            foreach (BlockSpec spec in CityBlocks)
            {
                string name = $"{MapRootName}_{spec.Name}";
                if (GameObject.Find(name) != null)
                {
                    continue;
                }

                GameObject clone = (GameObject)PrefabUtility.InstantiatePrefab(prefabAsset);
                clone.name = name;
                clone.transform.position = spec.Position;
                clone.transform.rotation = Quaternion.identity;
                Undo.RegisterCreatedObjectUndo(clone, "Expand Downtown Map");

                string[] hidden = spec.Kind switch
                {
                    BlockKind.NorthSouth => new[] { "HouseSet3", "HouseSet4" },
                    BlockKind.EastWest => new[] { "HouseSet1", "HouseSet2" },
                    _ => System.Array.Empty<string>(), // Both: full cross, hide nothing
                };

                foreach (string h in hidden)
                {
                    Transform group = clone.transform.Find(h);
                    if (group != null)
                    {
                        group.gameObject.SetActive(false);
                    }
                }

                ExtendWaypointGraph(clone.transform, spec.Name);
                EditorUtility.SetDirty(clone);
                built++;
            }

            Debug.Log($"[Setup] Şehir ağı kuruldu: {built} yeni blok, düzensiz dallanan sokak ağı (tek merkezden değil, " +
                      "bloktan bloğa). Aralar '26 - Connect City Arms With Roads' ile döşenecek. Sahneyi kaydet (Ctrl+S).");
        }

        private static Vector3 ResolveBlockPosition(string name)
        {
            if (name == MapRootName)
            {
                return Vector3.zero; // landmark sits at the origin
            }

            foreach (BlockSpec spec in CityBlocks)
            {
                if (spec.Name == name)
                {
                    return spec.Position;
                }
            }

            Debug.LogError($"[Setup] '{name}' CityBlocks içinde tanımlı değil.");
            return Vector3.zero;
        }

        // ------------------------------------------------------------------
        /// <summary>
        /// Fills the gap on every CityLinks pair with real driveable road tiles —
        /// without this, the network would be visually disconnected islands separated
        /// by bare ground Plane. Reuses the same flat, direction-agnostic M02Road01_1
        /// tile verified in TirgamesCitySetup (no rotation-correctness risk). Run this
        /// AFTER '12 - Expand Downtown Map'.
        /// </summary>
        [MenuItem("DeliverySim/Setup/26 - Connect City Arms With Roads")]
        public static void ConnectCityArmsWithRoads()
        {
            const string connectorRoadPrefab = "Assets/TirgamesAssets/StylizedWorld/Architecture/Prefabs/M02Road01_1.prefab";
            const float tile = 4.5f;

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(connectorRoadPrefab);
            if (prefab == null)
            {
                Debug.LogError("[Setup] '" + connectorRoadPrefab + "' bulunamadı — bağlantı yolları döşenemedi.");
                return;
            }

            int roadLayer = EnsureRoadLayerExists();

            GameObject connectorsRoot = GameObject.Find("_ArmConnectors");
            if (connectorsRoot != null)
            {
                Debug.Log("[Setup] '_ArmConnectors' zaten var — tekrar oluşturulmadı. Değiştirmek için önce elle sil.");
                return;
            }

            connectorsRoot = new GameObject("_ArmConnectors");
            Undo.RegisterCreatedObjectUndo(connectorsRoot, "Connect City Arms");

            int totalTiles = 0;
            foreach (LinkSpec link in CityLinks)
            {
                Vector3 fromCenter = ResolveBlockPosition(link.From);
                Vector3 toCenter = ResolveBlockPosition(link.To);
                totalTiles += PlaceConnector(connectorsRoot.transform, prefab, fromCenter, toCenter, tile, roadLayer, $"{link.From}_{link.To}");
            }

            EditorUtility.SetDirty(connectorsRoot);
            Debug.Log($"[Setup] {totalTiles} bağlantı yol tile'ı döşendi ({CityLinks.Length} bağlantı, düzensiz uzunluklarda). Sahneyi kaydet (Ctrl+S).");
        }

        private static int PlaceConnector(Transform parent, GameObject prefab, Vector3 fromCenter, Vector3 toCenter,
            float tile, int roadLayer, string label)
        {
            // Every block (landmark and every clone) is an UNROTATED copy of the same
            // source, so "Roads" always spans the same local offsets from its own
            // pivot: X:[-RoadsHalfX,+RoadsHalfX], Z:[RoadsMinZ,RoadsMaxZ]. Every link in
            // CityLinks shares exactly one axis coordinate by construction — detect
            // which one instead of hardcoding a direction per link, so CityLinks stays
            // pure data (position pairs), no separate "which way does this run" field
            // to keep in sync.
            bool alongZ = Mathf.Approximately(fromCenter.x, toCenter.x);

            float gapStart;
            float gapEnd;
            float fixedCoord;
            if (alongZ)
            {
                fixedCoord = fromCenter.x;
                Vector3 south = fromCenter.z < toCenter.z ? fromCenter : toCenter;
                Vector3 north = fromCenter.z < toCenter.z ? toCenter : fromCenter;
                gapStart = south.z + RoadsMaxZ; // south block's own north edge
                gapEnd = north.z + RoadsMinZ;    // north block's own south edge
            }
            else
            {
                fixedCoord = fromCenter.z;
                Vector3 west = fromCenter.x < toCenter.x ? fromCenter : toCenter;
                Vector3 east = fromCenter.x < toCenter.x ? toCenter : fromCenter;
                gapStart = west.x + RoadsHalfX;  // west block's own east edge
                gapEnd = east.x - RoadsHalfX;     // east block's own west edge
            }

            float length = gapEnd - gapStart;
            if (length <= 0f)
            {
                Debug.LogWarning($"[Setup] '{label}' bağlantısı için boşluk yok (gapStart={gapStart:F1}, gapEnd={gapEnd:F1}) — atlandı.");
                return 0;
            }

            int tileCount = Mathf.CeilToInt(length / tile);
            int placed = 0;
            for (int t = 0; t < tileCount; t++)
            {
                float axisPos = gapStart + tile * 0.5f + t * tile;
                // M02Road01_1's mesh sits 0.1m below its own pivot (learned the hard way
                // in TirgamesCitySetup) — +0.1 lifts the visible surface back to the
                // ground Plane's level instead of leaving it invisible underneath.
                const float meshPivotOffset = 0.1f;
                Vector3 pos = alongZ
                    ? new Vector3(fixedCoord, meshPivotOffset, axisPos)
                    : new Vector3(axisPos, meshPivotOffset, fixedCoord);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = $"Connector_{label}_{t}";
                instance.transform.position = pos;
                if (roadLayer >= 0)
                {
                    instance.layer = roadLayer;
                }

                BoxCollider collider = instance.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, -0.1f, 0f); // matches the -0.1 mesh offset above
                collider.size = new Vector3(tile, 0.1f, tile);

                Undo.RegisterCreatedObjectUndo(instance, "Connect City Arms");
                placed++;
            }

            return placed;
        }

        private static void ExtendWaypointGraph(Transform districtTransform, string slotName)
        {
            GameObject root = GameObject.Find(WaypointRootName);
            if (root == null)
            {
                Debug.LogWarning("[Setup] '" + WaypointRootName + "' yok — önce '10 - Build Road Waypoint Graph' çalıştır, yeni bölgenin yol ağı eklenemedi.");
                return;
            }

            // Mevcut tüm node'ları köprü bağlantısı için topla (sadece bu bölgeye ait olanlar hariç, henüz yok zaten).
            var existingNodes = new List<Waypoint>(root.GetComponentsInChildren<Waypoint>());

            // Full slot name, not just its last character — "E1" and "NE1" used to
            // collide on a shared "RW_1_*" prefix (Substring(Length-1) grabbed only
            // the trailing digit), silently merging two blocks' waypoint IDs.
            string prefix = "RW_" + slotName + "_";
            var newNodes = new List<Waypoint>(RoadTilePositions.Length);
            for (int i = 0; i < RoadTilePositions.Length; i++)
            {
                GameObject go = new GameObject(prefix + i);
                go.transform.SetParent(root.transform, false);
                // TransformPoint (not a raw offset add) so this stays correct when the
                // district is rotated 180° for visual variety — a flat vector add would
                // place the waypoints at the un-rotated positions, off the real roads.
                go.transform.position = districtTransform.TransformPoint(RoadTilePositions[i]) + Vector3.up * WaypointHeight;
                newNodes.Add(go.AddComponent<Waypoint>());
            }

            // Bölge içi komşuluk (orijinal '10' adımıyla aynı mantık).
            for (int i = 0; i < newNodes.Count; i++)
            {
                for (int j = i + 1; j < newNodes.Count; j++)
                {
                    float dist = Vector3.Distance(newNodes[i].transform.position, newNodes[j].transform.position);
                    if (dist <= NeighborLinkDistance)
                    {
                        newNodes[i].neighbors.Add(newNodes[j]);
                    }
                }
            }

            // Köprü bağlantısı: yeni bölgeyi eski ağa en yakın node çiftinden bağla, GPS rotası
            // iki bölge arasında da çalışsın.
            Waypoint bestOld = null;
            Waypoint bestNew = null;
            float bestDist = float.MaxValue;
            foreach (Waypoint oldNode in existingNodes)
            {
                foreach (Waypoint newNode in newNodes)
                {
                    float dist = Vector3.Distance(oldNode.transform.position, newNode.transform.position);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestOld = oldNode;
                        bestNew = newNode;
                    }
                }
            }

            if (bestOld != null && bestNew != null)
            {
                bestOld.neighbors.Add(bestNew);
                bestNew.neighbors.Add(bestOld);
                Debug.Log($"[Setup] Yeni bölge yol ağı eski ağa bağlandı: '{bestOld.name}' <-> '{bestNew.name}' (mesafe {bestDist:F1}m).");
            }

            EditorUtility.SetDirty(root);
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/16 - Assign Road Physics Layer (Tüm Harita)")]
        public static void AssignRoadLayer()
        {
            int roadLayer = EnsureRoadLayerExists();
            if (roadLayer < 0)
            {
                Debug.LogError("[Setup] '" + RoadLayerName + "' layer'ı için boş bir Layer slotu bulunamadı (0-31 dolu) — elle bir slot boşalt.");
                return;
            }

            int taggedCount = 0;

            // Downtown: every "Roads" group under every district clone (there can be up
            // to 4 after '12 - Expand Downtown Map'). Without this, RouteManager's
            // ground-snap raycast (which used to scan every layer) could hit a
            // building/prop collider before the road and snap the GPS line onto a
            // rooftop instead of the street.
            foreach (Transform roadsGroup in FindAllRoadsGroups())
            {
                foreach (Collider col in roadsGroup.GetComponentsInChildren<Collider>(true))
                {
                    GameObject tileGo = col.gameObject;
                    if (tileGo.layer != roadLayer)
                    {
                        Undo.RecordObject(tileGo, "Assign Road Layer");
                        tileGo.layer = roadLayer;
                        EditorUtility.SetDirty(tileGo);
                        taggedCount++;
                    }
                }
            }

            // Kenney: the whole district drives on one shared ground Plane (it has no
            // discrete road meshes — see KennyDistrictSetup's doc comment).
            GameObject plane = GameObject.Find("Plane");
            if (plane != null && plane.GetComponent<Collider>() != null)
            {
                if (plane.layer != roadLayer)
                {
                    Undo.RecordObject(plane, "Assign Road Layer");
                    plane.layer = roadLayer;
                    EditorUtility.SetDirty(plane);
                    taggedCount++;
                }
            }
            else
            {
                Debug.LogWarning("[Setup] Sahnede collider'lı bir 'Plane' bulunamadı — Kenney zemini Road layer'ına alınamadı.");
            }

            Debug.Log($"[Setup] '{RoadLayerName}' layer'ı (index {roadLayer}) hazır, {taggedCount} obje bu layer'a taşındı. " +
                      "RouteManager artık sadece yol/zeminden zıplayacak, binalardan değil (RouteManager kendi groundLayerMask'ını " +
                      "bu layer'a otomatik daraltır). Sahneyi kaydet (Ctrl+S).");
        }

        private static IEnumerable<Transform> FindAllRoadsGroups()
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t.name == "Roads" && t.parent != null && t.parent.name.StartsWith(MapRootName))
                {
                    yield return t;
                }
            }
        }

        /// <summary>
        /// Creates the "Road" Tag Manager layer via SerializedObject (the same
        /// mechanism the Editor's own Layer inspector uses) instead of hand-editing
        /// ProjectSettings/TagManager.asset directly — no risk of corrupting or
        /// reordering the existing 32-slot layer list, and safe to call repeatedly
        /// (idempotent: returns the existing index if the layer is already there).
        /// </summary>
        internal static int EnsureRoadLayerExists()
        {
            int existing = LayerMask.NameToLayer(RoadLayerName);
            if (existing >= 0)
            {
                return existing;
            }

            Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManagerAssets.Length == 0)
            {
                return -1;
            }

            var tagManager = new SerializedObject(tagManagerAssets[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");
            if (layers == null)
            {
                return -1;
            }

            for (int i = 0; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(slot.stringValue))
                {
                    slot.stringValue = RoadLayerName;
                    tagManager.ApplyModifiedProperties();
                    AssetDatabase.SaveAssets();
                    return i;
                }
            }

            return -1;
        }
    }
}
