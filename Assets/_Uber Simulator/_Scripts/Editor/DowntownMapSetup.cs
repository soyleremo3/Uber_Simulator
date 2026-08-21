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
        // Cloning the WHOLE cross repeatedly would either overlap (HouseSet3/4's arms
        // reach further than a naive 90m grid step allows) or look mechanically
        // identical every time. Instead: the original import stays a full cross
        // ("town square" landmark, all 4 HouseSets visible), and BuildCityArms grows
        // FOUR distinct avenues outward from it — north/south arms show only
        // HouseSet1/2 (matches their natural street-facing axis), east/west arms show
        // only HouseSet3/4 — so every arm is genuinely different building content, not
        // a rotated copy of the same thing, and each arm's buildings already face the
        // direction that street actually runs.
        private const float ArmSpacing = 140f; // clears the landmark's HouseSet3/4 arm reach (measured ~61m) with margin
        private const int ClonesPerArm = 2;
        private const float RoadsHalfX = 33.75f;
        private const float RoadsMinZ = -40.5f;
        private const float RoadsMaxZ = 45f;

        private enum ArmDirection { North, South, East, West }

        [MenuItem("DeliverySim/Setup/12 - Expand Downtown Map (Kollar Ekle)")]
        public static void ExpandDowntownMap()
        {
            GameObject original = GameObject.Find(MapRootName);
            if (original == null)
            {
                Debug.LogError("[Setup] '" + MapRootName + "' sahnede yok — önce '9 - Import Downtown Street Map' çalıştır.");
                return;
            }

            int built = 0;
            built += BuildArm(original, ArmDirection.North, new[] { "HouseSet3", "HouseSet4" });
            built += BuildArm(original, ArmDirection.South, new[] { "HouseSet3", "HouseSet4" });
            built += BuildArm(original, ArmDirection.East, new[] { "HouseSet1", "HouseSet2" });
            built += BuildArm(original, ArmDirection.West, new[] { "HouseSet1", "HouseSet2" });

            Debug.Log($"[Setup] Şehir kolları kuruldu: {built} yeni blok (kuzey/güney: mağazalı cadde, doğu/batı: sakin cadde), " +
                      "aralar '13 - Connect City Arms With Roads' ile döşenecek. Sahneyi kaydet (Ctrl+S).");
        }

        private static int BuildArm(GameObject original, ArmDirection direction, string[] hiddenHouseSets)
        {
            int builtCount = 0;
            for (int i = 1; i <= ClonesPerArm; i++)
            {
                string name = $"{MapRootName}_{direction}{i}";
                if (GameObject.Find(name) != null)
                {
                    continue;
                }

                Vector3 offset = ArmOffset(direction, i);
                GameObject clone = Object.Instantiate(original);
                clone.name = name;
                clone.transform.position = offset;
                clone.transform.rotation = Quaternion.identity; // HouseSets are already correctly oriented — no rotation needed
                Undo.RegisterCreatedObjectUndo(clone, "Expand Downtown Map");

                foreach (string hidden in hiddenHouseSets)
                {
                    Transform group = clone.transform.Find(hidden);
                    if (group != null)
                    {
                        group.gameObject.SetActive(false);
                    }
                }

                ExtendWaypointGraph(clone.transform, name);
                EditorUtility.SetDirty(clone);
                builtCount++;
            }

            return builtCount;
        }

        private static Vector3 ArmOffset(ArmDirection direction, int index)
        {
            float d = ArmSpacing * index;
            return direction switch
            {
                ArmDirection.North => new Vector3(0f, 0f, d),
                ArmDirection.South => new Vector3(0f, 0f, -d),
                ArmDirection.East => new Vector3(d, 0f, 0f),
                ArmDirection.West => new Vector3(-d, 0f, 0f),
                _ => Vector3.zero,
            };
        }

        // ------------------------------------------------------------------
        /// <summary>
        /// Fills the gap between the landmark and each arm's clones (and between
        /// consecutive clones in the same arm) with real driveable road tiles — without
        /// this, the arms would be visually disconnected islands separated by bare
        /// ground Plane. Reuses the same flat, direction-agnostic M02Road01_1 tile
        /// verified in TirgamesCitySetup (no rotation-correctness risk). Run this
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
            foreach (ArmDirection dir in new[] { ArmDirection.North, ArmDirection.South, ArmDirection.East, ArmDirection.West })
            {
                Vector3 prevCenter = Vector3.zero; // landmark
                for (int i = 1; i <= ClonesPerArm; i++)
                {
                    Vector3 thisCenter = ArmOffset(dir, i);
                    totalTiles += PlaceConnector(connectorsRoot.transform, prefab, prevCenter, thisCenter, dir, tile, roadLayer, $"{dir}{i}");
                    prevCenter = thisCenter;
                }
            }

            EditorUtility.SetDirty(connectorsRoot);
            Debug.Log($"[Setup] {totalTiles} bağlantı yol tile'ı döşendi (4 kol x {ClonesPerArm} segment). Sahneyi kaydet (Ctrl+S).");
        }

        private static int PlaceConnector(Transform parent, GameObject prefab, Vector3 fromCenter, Vector3 toCenter,
            ArmDirection dir, float tile, int roadLayer, string label)
        {
            // Every block (landmark and every clone) is an UNROTATED copy of the same
            // source, so "Roads" always spans the same local offsets from its own
            // pivot: X:[-RoadsHalfX,+RoadsHalfX], Z:[RoadsMinZ,RoadsMaxZ] — regardless
            // of which arm it's in. Explicit per-direction edges (not a clever signed
            // formula) so each case is directly checkable against those offsets.
            bool alongZ;
            float gapStart;
            float gapEnd;
            switch (dir)
            {
                case ArmDirection.North:
                    alongZ = true;
                    gapStart = fromCenter.z + RoadsMaxZ; // from's north edge
                    gapEnd = toCenter.z + RoadsMinZ;      // to's south edge
                    break;
                case ArmDirection.South:
                    alongZ = true;
                    gapStart = toCenter.z + RoadsMaxZ;    // to's north edge
                    gapEnd = fromCenter.z + RoadsMinZ;    // from's south edge
                    break;
                case ArmDirection.East:
                    alongZ = false;
                    gapStart = fromCenter.x + RoadsHalfX; // from's east edge
                    gapEnd = toCenter.x - RoadsHalfX;      // to's west edge
                    break;
                default: // West
                    alongZ = false;
                    gapStart = toCenter.x + RoadsHalfX;    // to's east edge
                    gapEnd = fromCenter.x - RoadsHalfX;    // from's west edge
                    break;
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
                    ? new Vector3(0f, meshPivotOffset, axisPos)
                    : new Vector3(axisPos, meshPivotOffset, 0f);

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                instance.name = $"Connector_{label}_{t}";
                instance.transform.position = pos;
                if (roadLayer >= 0)
                {
                    instance.layer = roadLayer;
                }

                BoxCollider collider = instance.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, -0.1f, 0f); // M02Road01_1's mesh sits 0.1m below its own pivot
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

            string prefix = "RW_" + slotName.Substring(slotName.Length - 1) + "_";
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
