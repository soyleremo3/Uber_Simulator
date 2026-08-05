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
    }
}
