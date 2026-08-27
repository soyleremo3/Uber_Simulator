using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliverySim.EditorTools
{
    /// <summary>
    /// Large open-world city map builder — the successor to the district-by-district
    /// experiments in DowntownMapSetup / KennyDistrictSetup / TirgamesCitySetup.
    ///
    /// Design (approved plan):
    ///  - 2 km x 2 km drivable bound, ~1.4 km fully-built core + dressed outskirts.
    ///  - Non-grid road hierarchy: chamfered Ring beltway + curved cross arterials +
    ///    per-district secondary loops + roundabouts.
    ///  - Six districts: Downtown (Tirgames), Commercial / Residential / Suburban /
    ///    Industrial / Parks (Kenney CC0).
    ///
    /// Stage v1 (this file) = BLOCKOUT only: ground, the whole road network as real
    /// driveable geometry, empty district parents, placeholder landmarks, a single
    /// bridged Waypoint graph matching the roads, player spawn, and a TEMP order for
    /// a GPS-routing smoke test. Buildings / props / vegetation / polish come in
    /// later stages (menu items 20+).
    ///
    /// Every menu item is idempotent (safe to re-run) and every placed art object is
    /// a PrefabUtility.InstantiatePrefab instance — never a deep Object.Instantiate —
    /// because deep-instancing big hierarchies serialised full copies into the .unity
    /// file and blew past GitHub's 100 MB limit last time (see DowntownMapSetup:375).
    /// </summary>
    public static class CityMapBuilder
    {
        // ---------------------------------------------------------------- constants
        private const string CityRootName = "CITY";
        private const string WaypointGroupName = "_Waypoints";
        private const string RoadLayerName = "Road";

        private const string RoadsKit = "Assets/_Uber Simulator/Art/Assets/Kenny/CityKitRoads";
        private const string StraightTile = RoadsKit + "/road-straight.fbx";
        private const string RoundaboutTile = RoadsKit + "/road-roundabout.fbx";
        private const string MaterialsFolder = "Assets/_Uber Simulator/Art/Materials";

        private const string ManagersPrefab = "Assets/_Uber Simulator/Prefabs/_Managers.prefab";
        private const string VehiclePrefab = "Assets/_Uber Simulator/Prefabs/car (1).prefab";

        // Kenney road FBX import at 1 unit / tile — everything is scaled up from that.
        private const float RoadY = 0.08f;            // visible road surface height above the ground plane
        private const float ColliderThickness = 0.30f;
        private const float ArterialWidth = 16f;
        private const float RingWidth = 16f;
        private const float SecondaryWidth = 11f;

        private const float WaypointY = 0.15f;
        private const float WaypointSpacing = 45f;    // node every ~45 m along a run
        private const float JunctionSnap = 26f;       // cross-lane nodes closer than this get linked (road width ~16)

        private const float GroundBound = 1050f;      // half-extent of the ground plane (covers the 2 km drivable bound)

        private static readonly Vector3 SpawnPos = new Vector3(70f, 1.5f, 4f);
        private static readonly Vector3 SpawnEuler = new Vector3(0f, 90f, 0f); // face +X, east down Main St toward Downtown

        // ---------------------------------------------------------------- layout data
        // Every road is a polyline in world XZ (y ignored). Gentle kinks on purpose —
        // nothing is a perfect infinite straight (plan / spec §5).
        private readonly struct Lane
        {
            public readonly string Name;
            public readonly float Width;
            public readonly bool Loop;
            public readonly Vector3[] Points;

            public Lane(string name, float width, bool loop, params Vector2[] pts)
            {
                Name = name;
                Width = width;
                Loop = loop;
                Points = pts.Select(p => new Vector3(p.x, 0f, p.y)).ToArray();
            }
        }

        private static Lane[] PrimaryLanes() => new[]
        {
            // Chamfered rectangle beltway ~ +/-650 (spec §4 primary ring).
            new Lane("Ring", RingWidth, true,
                new Vector2(-650f, -540f), new Vector2(-540f, -650f),
                new Vector2( 540f, -650f), new Vector2( 650f, -540f),
                new Vector2( 650f,  540f), new Vector2( 540f,  650f),
                new Vector2(-540f,  650f), new Vector2(-650f,  540f)),

            // Main Street, E-W through Downtown: Residential -> Downtown -> Commercial.
            new Lane("MainStreet", ArterialWidth, false,
                new Vector2(-980f, -15f), new Vector2(-500f, -8f), new Vector2(-140f, -24f),
                new Vector2( 140f,  20f), new Vector2( 520f, 10f), new Vector2( 980f,  24f)),

            // Central Avenue, N-S through Downtown: Parks -> Downtown -> Industrial.
            new Lane("CentralAvenue", ArterialWidth, false,
                new Vector2( 10f, -980f), new Vector2( 25f, -520f), new Vector2(-15f, -150f),
                new Vector2( 20f,  150f), new Vector2(-25f,  520f), new Vector2(-10f,  980f)),

            // Diagonal NW->SE: Suburban -> Downtown (breaks the grid, spec §4/§5).
            new Lane("DiagonalNW", 14f, false,
                new Vector2(-800f, 720f), new Vector2(-560f, 540f), new Vector2(-360f, 360f),
                new Vector2(-180f, 150f), new Vector2(-55f, 55f)),

            // Diagonal E->NE: Commercial -> Industrial (long logistics route).
            new Lane("DiagonalNE", 14f, false,
                new Vector2( 720f, 55f), new Vector2( 560f, 180f), new Vector2( 420f, 340f),
                new Vector2( 300f, 520f), new Vector2( 235f, 720f)),
        };

        // Per-district secondary loop (irregular rectangle) + a short stub linking it
        // back to the arterial/ring nearest it. Local streets come in a later stage.
        private static Lane[] SecondaryLanes() => new[]
        {
            new Lane("Sec_Downtown", SecondaryWidth, true,
                new Vector2(-150f, -140f), new Vector2(150f, -150f),
                new Vector2( 160f, 150f), new Vector2(-140f, 160f)),

            new Lane("Sec_Commercial", SecondaryWidth, true,
                new Vector2( 330f, -180f), new Vector2(700f, -160f),
                new Vector2( 710f, 190f), new Vector2( 340f, 175f)),
            new Lane("Sec_CommercialStub", SecondaryWidth, false,
                new Vector2( 520f, 10f), new Vector2(520f, 175f)),

            new Lane("Sec_Residential", SecondaryWidth, true,
                new Vector2(-780f, -420f), new Vector2(-300f, -440f),
                new Vector2(-280f, 90f), new Vector2(-800f, 70f)),
            new Lane("Sec_ResidentialStub", SecondaryWidth, false,
                new Vector2(-500f, -8f), new Vector2(-540f, 70f)),

            new Lane("Sec_Suburban", SecondaryWidth, true,
                new Vector2(-720f, 250f), new Vector2(-250f, 300f),
                new Vector2(-230f, 690f), new Vector2(-700f, 700f)),
            new Lane("Sec_SuburbanStub", SecondaryWidth, false,
                new Vector2(-360f, 360f), new Vector2(-470f, 320f)),

            new Lane("Sec_Industrial", SecondaryWidth, true,
                new Vector2( 30f, 330f), new Vector2(470f, 350f),
                new Vector2( 500f, 700f), new Vector2( 20f, 690f)),
            new Lane("Sec_IndustrialStub", SecondaryWidth, false,
                new Vector2( 20f, 520f), new Vector2(260f, 520f)),

            new Lane("Sec_Parks", SecondaryWidth, true,
                new Vector2(-250f, -760f), new Vector2(250f, -770f),
                new Vector2( 240f, -360f), new Vector2(-240f, -350f)),
        };

        private readonly struct RoundaboutSpec
        {
            public readonly string Name;
            public readonly Vector3 Pos;
            public readonly float Radius;

            public RoundaboutSpec(string name, Vector2 pos, float radius)
            {
                Name = name;
                Pos = new Vector3(pos.x, 0f, pos.y);
                Radius = radius;
            }
        }

        private static readonly RoundaboutSpec[] Roundabouts =
        {
            new RoundaboutSpec("RA_DowntownNorth", new Vector2(5f, 200f), 26f),
            new RoundaboutSpec("RA_RingSouth", new Vector2(15f, -650f), 34f),
            new RoundaboutSpec("RA_RingNorth", new Vector2(-8f, 650f), 34f),
            new RoundaboutSpec("RA_RingWest", new Vector2(-650f, 5f), 34f),
            new RoundaboutSpec("RA_RingEast", new Vector2(650f, 12f), 34f),
            new RoundaboutSpec("RA_CommercialHub", new Vector2(520f, 10f), 30f),
        };

        private readonly struct DistrictSpec
        {
            public readonly string Name;
            public readonly Vector3 Center;
            public readonly Vector2 Size;
            public readonly Color Color;

            public DistrictSpec(string name, Vector2 center, Vector2 size, Color color)
            {
                Name = name;
                Center = new Vector3(center.x, 0f, center.y);
                Size = size;
                Color = color;
            }
        }

        private static readonly DistrictSpec[] DistrictList =
        {
            new DistrictSpec("Downtown", new Vector2(0f, 0f), new Vector2(360f, 360f), new Color(0.75f, 0.55f, 0.25f)),
            new DistrictSpec("Commercial", new Vector2(520f, 0f), new Vector2(520f, 420f), new Color(0.30f, 0.55f, 0.85f)),
            new DistrictSpec("Residential", new Vector2(-520f, -170f), new Vector2(620f, 520f), new Color(0.35f, 0.75f, 0.45f)),
            new DistrictSpec("Suburban", new Vector2(-470f, 470f), new Vector2(560f, 520f), new Color(0.55f, 0.80f, 0.35f)),
            new DistrictSpec("Industrial", new Vector2(260f, 540f), new Vector2(560f, 420f), new Color(0.70f, 0.45f, 0.40f)),
            new DistrictSpec("Parks", new Vector2(0f, -560f), new Vector2(520f, 460f), new Color(0.25f, 0.65f, 0.30f)),
        };

        private readonly struct LandmarkSpec
        {
            public readonly string Name;
            public readonly Vector3 Pos;
            public readonly Vector3 Scale;
            public readonly Color Color;

            public LandmarkSpec(string name, Vector3 pos, Vector3 scale, Color color)
            {
                Name = name;
                Pos = pos;
                Scale = scale;
                Color = color;
            }
        }

        private static readonly LandmarkSpec[] Landmarks =
        {
            new LandmarkSpec("LM_CityPlaza", new Vector3(0f, 2f, 45f), new Vector3(70f, 4f, 70f), new Color(0.80f, 0.75f, 0.55f)),
            new LandmarkSpec("LM_SkylineTower", new Vector3(120f, 45f, 95f), new Vector3(24f, 90f, 24f), new Color(0.55f, 0.60f, 0.70f)),
            new LandmarkSpec("LM_Supermarket", new Vector3(560f, 7f, -45f), new Vector3(90f, 14f, 64f), new Color(0.85f, 0.45f, 0.30f)),
            new LandmarkSpec("LM_GasStation", new Vector3(470f, 4f, 75f), new Vector3(28f, 8f, 42f), new Color(0.90f, 0.80f, 0.20f)),
            new LandmarkSpec("LM_CentralPark", new Vector3(0f, 1f, -560f), new Vector3(210f, 2f, 190f), new Color(0.30f, 0.60f, 0.30f)),
            new LandmarkSpec("LM_WarehouseComplex", new Vector3(260f, 10f, 540f), new Vector3(150f, 20f, 110f), new Color(0.60f, 0.60f, 0.62f)),
            new LandmarkSpec("LM_BigApartments", new Vector3(-520f, 20f, -170f), new Vector3(64f, 40f, 130f), new Color(0.70f, 0.72f, 0.60f)),
            new LandmarkSpec("LM_Hospital", new Vector3(-170f, 16f, -360f), new Vector3(64f, 32f, 52f), new Color(0.92f, 0.92f, 0.95f)),
            new LandmarkSpec("LM_School", new Vector3(-470f, 8f, 300f), new Vector3(72f, 14f, 52f), new Color(0.85f, 0.65f, 0.45f)),
        };

        // TEMP routing smoke-test content — deleted/replaced by the v2 gameplay pass.
        private const string TempPickupId = "temp_city_pickup";
        private const string TempDeliveryId = "temp_city_delivery";
        private static readonly Vector3 TempPickupPos = new Vector3(-560f, 0f, -8f);
        private static readonly Vector3 TempDeliveryPos = new Vector3(240f, 0f, 690f);

        // ================================================================ menu items

        [MenuItem("DeliverySim/City/00 - Clear City")]
        public static void ClearCity()
        {
            int removed = 0;

            GameObject cityRoot = GameObject.Find(CityRootName);
            if (cityRoot != null)
            {
                // Re-parent WalkingStreet back to the scene root so it survives the wipe.
                Transform ws = FindDeep(cityRoot.transform, "WalkingStreet");
                if (ws != null)
                {
                    ws.SetParent(null, true);
                }

                Undo.DestroyObjectImmediate(cityRoot);
                removed++;
            }

            GameObject strayWaypoints = GameObject.Find(WaypointGroupName);
            if (strayWaypoints != null)
            {
                Undo.DestroyObjectImmediate(strayWaypoints);
                removed++;
            }

            foreach (string tempName in new[] { "TEMP_Pickup", "TEMP_Delivery" })
            {
                GameObject go = GameObject.Find(tempName);
                if (go != null)
                {
                    Undo.DestroyObjectImmediate(go);
                    removed++;
                }
            }

            OrderData tempOrder = AssetDatabase.LoadAssetAtPath<OrderData>(
                DeliverySimSetup.OrdersFolder + "/temp_city_route_check.asset");
            if (tempOrder != null)
            {
                RemoveFromOrderPool(tempOrder);
                AssetDatabase.DeleteAsset(DeliverySimSetup.OrdersFolder + "/temp_city_route_check.asset");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[City] Şehir temizlendi ({removed} kök obje). Manager'lar / araç / kamera / ışık korundu. Sahneyi kaydet (Ctrl+S).");
        }

        [MenuItem("DeliverySim/City/01 - Scene Prereqs (Managers + Vehicle + Layer)")]
        public static void ScenePrereqs()
        {
            int roadLayer = DowntownMapSetup.EnsureRoadLayerExists();
            if (roadLayer < 0)
            {
                Debug.LogError("[City] Boş bir Layer slotu bulunamadı — '" + RoadLayerName + "' oluşturulamadı.");
                return;
            }

            // Managers: reuse the established one-click setup (idempotent).
            DeliverySimSetup.CreateManagers();

            // Player vehicle placeholder for the blockout drive test. The real rig
            // (VehicleController + camera + UI) is wired in the v2 gameplay pass.
            GameObject vehicle = GameObject.Find("PlayerVehicle");
            if (vehicle == null && FindComponentInScene("DeliverySim.VehicleController") == null)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(VehiclePrefab);
                if (prefab != null)
                {
                    vehicle = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    vehicle.name = "PlayerVehicle";
                    Undo.RegisterCreatedObjectUndo(vehicle, "Create PlayerVehicle");
                }
                else
                {
                    Debug.LogWarning("[City] '" + VehiclePrefab + "' bulunamadı — araç placeholder atlandı.");
                }
            }

            if (vehicle != null)
            {
                vehicle.transform.position = SpawnPos;
                vehicle.transform.rotation = Quaternion.Euler(SpawnEuler);
                EditorUtility.SetDirty(vehicle);
            }

            WireRouteStart(vehicle);
            ParentWalkingStreetIntoDowntown();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[City] Ön koşullar hazır: '" + RoadLayerName + "' layer (index " + roadLayer +
                      "), _Managers/_Gameplay/_UI, PlayerVehicle spawn'da. Sahneyi kaydet (Ctrl+S).");
        }

        [MenuItem("DeliverySim/City/10 - Blockout: Ground")]
        public static void BuildGround()
        {
            Transform groundRoot = CityChild("_Ground");
            if (groundRoot.childCount > 0)
            {
                Debug.Log("[City] Zemin zaten var — atlandı ('00 - Clear City' ile sıfırla).");
                return;
            }

            Material grass = EnsureMat("CityGround", new Color(0.30f, 0.42f, 0.22f));

            // One big Unity plane (10x10 units) scaled to cover the drivable bound.
            // Kept on the DEFAULT layer (not Road): the vehicle's suspension raycast
            // needs a floor it can actually hit, and RouteManager only needs the Road
            // layer on the road tiles the GPS line runs along (fallbackGroundY = 0
            // covers any off-road gap, which is exactly this plane's height).
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
            plane.name = "GroundPlane";
            plane.transform.SetParent(groundRoot, false);
            plane.transform.localPosition = Vector3.zero;
            plane.transform.localScale = new Vector3(GroundBound / 5f, 1f, GroundBound / 5f);
            plane.isStatic = true;
            plane.GetComponent<MeshRenderer>().sharedMaterial = grass;
            Undo.RegisterCreatedObjectUndo(plane, "Build Ground");

            // District colour pads — pure blockout readability, sit 2 cm under the roads.
            Material asphalt = EnsureMat("CityDistrictPad", new Color(0.20f, 0.20f, 0.22f));
            foreach (DistrictSpec d in DistrictList)
            {
                GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                pad.name = "Pad_" + d.Name;
                Object.DestroyImmediate(pad.GetComponent<Collider>());
                pad.transform.SetParent(groundRoot, false);
                pad.transform.position = d.Center + Vector3.up * 0.03f;
                pad.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                pad.transform.localScale = new Vector3(d.Size.x, d.Size.y, 1f);
                pad.isStatic = true;
                Material pm = EnsureMat("CityPad_" + d.Name, d.Color * 0.5f);
                pad.GetComponent<MeshRenderer>().sharedMaterial = pm;
                Undo.RegisterCreatedObjectUndo(pad, "Build Ground");
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[City] Zemin döşendi: " + (GroundBound * 2f) + " m plane (Default layer + MeshCollider) + 6 bölge rengi. Sahneyi kaydet (Ctrl+S).");
        }

        [MenuItem("DeliverySim/City/11 - Blockout: Primary Roads")]
        public static void BuildPrimaryRoads()
        {
            BuildLaneGroup(CityChild("Roads/MainRoads"), PrimaryLanes(), "Primary");
            BuildRoundabouts(CityChild("Roads/MainRoads"));
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("DeliverySim/City/12 - Blockout: Secondary Roads")]
        public static void BuildSecondaryRoads()
        {
            BuildLaneGroup(CityChild("Roads/SecondaryRoads"), SecondaryLanes(), "Secondary");
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        [MenuItem("DeliverySim/City/13 - Blockout: District Markers + Landmarks")]
        public static void BuildDistrictsAndLandmarks()
        {
            Transform districts = CityChild("Districts");
            foreach (DistrictSpec d in DistrictList)
            {
                Transform dt = CityChild("Districts/" + d.Name);
                dt.position = d.Center;
            }

            Transform lmRoot = CityChild("Landmarks");
            if (lmRoot.childCount == 0)
            {
                foreach (LandmarkSpec lm in Landmarks)
                {
                    GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    box.name = lm.Name;
                    box.transform.SetParent(lmRoot, false);
                    box.transform.position = lm.Pos;
                    box.transform.localScale = lm.Scale;
                    box.isStatic = true;
                    box.GetComponent<MeshRenderer>().sharedMaterial = EnsureMat(lm.Name + "_Mat", lm.Color);
                    Undo.RegisterCreatedObjectUndo(box, "Build Landmarks");
                }
            }

            // Boundary markers: 4 corner posts of the drivable bound so the edge of
            // the world is legible in the blockout.
            Transform bounds = CityChild("Boundaries");
            if (bounds.childCount == 0)
            {
                float b = 1000f;
                Vector2[] corners = { new Vector2(-b, -b), new Vector2(b, -b), new Vector2(b, b), new Vector2(-b, b) };
                for (int i = 0; i < corners.Length; i++)
                {
                    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = "BoundPost_" + i;
                    post.transform.SetParent(bounds, false);
                    post.transform.position = new Vector3(corners[i].x, 15f, corners[i].y);
                    post.transform.localScale = new Vector3(8f, 30f, 8f);
                    post.GetComponent<MeshRenderer>().sharedMaterial = EnsureMat("BoundPost_Mat", new Color(0.9f, 0.2f, 0.2f));
                    Undo.RegisterCreatedObjectUndo(post, "Build Boundaries");
                }
            }

            EditorUtility.SetDirty(districts.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[City] Districts/ (6 boş ebeveyn), Landmarks/ (" + Landmarks.Length + " yer imi kutusu), Boundaries/ (4 köşe) hazır. Sahneyi kaydet (Ctrl+S).");
        }

        [MenuItem("DeliverySim/City/14 - Blockout: Waypoint Graph + Spawn + Smoke Test")]
        public static void BuildWaypointGraphAndSpawn()
        {
            Transform wpRoot = CityChild(WaypointGroupName);
            if (wpRoot.childCount > 0)
            {
                Debug.Log("[City] Waypoint ağı zaten var — atlandı ('00 - Clear City' ile sıfırla).");
                return;
            }

            var allLanes = new List<Lane>();
            allLanes.AddRange(PrimaryLanes());
            allLanes.AddRange(SecondaryLanes());

            // 1) nodes + intra-lane links
            var laneNodes = new List<List<Waypoint>>();
            int nodeCount = 0;
            foreach (Lane lane in allLanes)
            {
                Transform laneT = new GameObject("WP_" + lane.Name).transform;
                laneT.SetParent(wpRoot, false);
                Undo.RegisterCreatedObjectUndo(laneT.gameObject, "Waypoint Graph");

                List<Vector3> pts = SampleLane(lane);
                var nodes = new List<Waypoint>(pts.Count);
                for (int i = 0; i < pts.Count; i++)
                {
                    var go = new GameObject(lane.Name + "_" + i);
                    go.transform.SetParent(laneT, false);
                    go.transform.position = new Vector3(pts[i].x, WaypointY, pts[i].z);
                    nodes.Add(go.AddComponent<Waypoint>());
                }

                for (int i = 0; i < nodes.Count - 1; i++)
                {
                    Link(nodes[i], nodes[i + 1]);
                }

                if (lane.Loop && nodes.Count > 2)
                {
                    Link(nodes[nodes.Count - 1], nodes[0]);
                }

                laneNodes.Add(nodes);
                nodeCount += nodes.Count;
            }

            // roundabouts: an 8-node ring, treated as its own lane for cross-linking
            foreach (RoundaboutSpec ra in Roundabouts)
            {
                Transform raT = new GameObject("WP_" + ra.Name).transform;
                raT.SetParent(wpRoot, false);
                Undo.RegisterCreatedObjectUndo(raT.gameObject, "Waypoint Graph");

                var ring = new List<Waypoint>(8);
                for (int i = 0; i < 8; i++)
                {
                    float ang = i / 8f * Mathf.PI * 2f;
                    Vector3 p = ra.Pos + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ra.Radius;
                    var go = new GameObject(ra.Name + "_" + i);
                    go.transform.SetParent(raT, false);
                    go.transform.position = new Vector3(p.x, WaypointY, p.z);
                    ring.Add(go.AddComponent<Waypoint>());
                }

                for (int i = 0; i < 8; i++)
                {
                    Link(ring[i], ring[(i + 1) % 8]);
                }

                laneNodes.Add(ring);
                nodeCount += ring.Count;
            }

            // 2) cross-lane links: any two nodes from different lanes closer than
            //    JunctionSnap are welded — handles crossings, T-junctions and
            //    roundabout tangencies uniformly (same idea as the nearest-pair
            //    bridge in DowntownMapSetup/KennyDistrictSetup, generalised).
            int crossLinks = 0;
            for (int a = 0; a < laneNodes.Count; a++)
            {
                for (int b = a + 1; b < laneNodes.Count; b++)
                {
                    foreach (Waypoint na in laneNodes[a])
                    {
                        foreach (Waypoint nb in laneNodes[b])
                        {
                            if (FlatDist(na.transform.position, nb.transform.position) <= JunctionSnap)
                            {
                                if (Link(na, nb))
                                {
                                    crossLinks++;
                                }
                            }
                        }
                    }
                }
            }

            foreach (Waypoint w in wpRoot.GetComponentsInChildren<Waypoint>())
            {
                EditorUtility.SetDirty(w);
            }

            // 3) spawn + TEMP routing smoke test
            GameObject vehicle = GameObject.Find("PlayerVehicle") ?? FindComponentInScene("DeliverySim.VehicleController")?.gameObject;
            if (vehicle != null)
            {
                vehicle.transform.position = SpawnPos;
                vehicle.transform.rotation = Quaternion.Euler(SpawnEuler);
            }

            DeliverySimSetup.EnsureFolder(DeliverySimSetup.OrdersFolder);
            DeliverySimSetup.CreatePoint<PickupPoint>("TEMP_Pickup", TempPickupId, TempPickupPos);
            DeliverySimSetup.CreatePoint<DeliveryPoint>("TEMP_Delivery", TempDeliveryId, TempDeliveryPos);
            OrderData temp = DeliverySimSetup.CreateOrderAsset(
                "temp_city_route_check", "TEMP Rota Kontrolü", TempPickupId, TempDeliveryId, 100f, 240f, CargoType.Package);
            AddToOrderPool(temp);

            bool connected = PathExists(wpRoot, TempPickupPos, TempDeliveryPos);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[City] Waypoint agi: {nodeCount} node, {crossLinks} kavsak baglantisi. " +
                      $"TEMP alim/teslim + siparis eklendi. Rota baglantisi (BFS): {(connected ? "VAR - OK" : "YOK - HATA")}. " +
                      "Sahneyi kaydet (Ctrl+S).");
        }

        [MenuItem("DeliverySim/City/19 - Blockout: RUN ALL (10-14)")]
        public static void RunAllBlockout()
        {
            ScenePrereqs();
            BuildGround();
            BuildPrimaryRoads();
            BuildSecondaryRoads();
            BuildDistrictsAndLandmarks();
            BuildWaypointGraphAndSpawn();
            Debug.Log("[City] BLOCKOUT v1 tamam (01 + 10-14). Sahneyi kaydet (Ctrl+S), sonra commit.");
        }

        // ================================================================ road building

        private static void BuildLaneGroup(Transform parent, IEnumerable<Lane> lanes, string label)
        {
            if (parent.childCount > 0)
            {
                Debug.Log("[City] '" + parent.name + "' zaten dolu — " + label + " yolları atlandı ('00 - Clear City' ile sıfırla).");
                return;
            }

            GameObject straight = AssetDatabase.LoadAssetAtPath<GameObject>(StraightTile);
            if (straight == null)
            {
                Debug.LogError("[City] '" + StraightTile + "' bulunamadı — yollar döşenemedi.");
                return;
            }

            int roadLayer = LayerMask.NameToLayer(RoadLayerName);
            Material mat = EnsureRoadMat();
            int runs = 0;
            int joints = 0;

            foreach (Lane lane in lanes)
            {
                Transform laneT = new GameObject(lane.Name).transform;
                laneT.SetParent(parent, false);
                Undo.RegisterCreatedObjectUndo(laneT.gameObject, "Build Roads");

                int segCount = lane.Loop ? lane.Points.Length : lane.Points.Length - 1;
                for (int i = 0; i < segCount; i++)
                {
                    Vector3 a = lane.Points[i];
                    Vector3 b = lane.Points[(i + 1) % lane.Points.Length];
                    PlaceRun(laneT, straight, mat, roadLayer, a, b, lane.Width, lane.Name + "_seg" + i);
                    runs++;
                }

                // A square patch at every polyline corner so the angled slabs don't
                // leave a wedge-shaped gap the car can drop through / that reads as a
                // broken road. Interior vertices for open lanes, all vertices for loops.
                int first = lane.Loop ? 0 : 1;
                int last = lane.Loop ? lane.Points.Length : lane.Points.Length - 1;
                for (int i = first; i < last; i++)
                {
                    PlaceJoint(laneT, straight, mat, roadLayer, lane.Points[i], lane.Width, lane.Name + "_joint" + i);
                    joints++;
                }
            }

            Debug.Log($"[City] {label} yol agi: {runs} segment + {joints} kavsak yamasi (uzunluga gore olcekli road-straight, her biri tek obje + Road layer collider). Sahneyi kaydet (Ctrl+S).");
        }

        private static void PlaceRun(Transform parent, GameObject straightPrefab, Material mat, int roadLayer,
            Vector3 a, Vector3 b, float width, string name)
        {
            Vector3 dir = b - a;
            dir.y = 0f;
            float len = dir.magnitude;
            if (len < 0.05f)
            {
                return;
            }

            // full-width overhang each end so consecutive angled segments overlap
            // instead of leaving a gap the car can drop through
            len += width;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(straightPrefab, parent);
            go.name = name;
            go.transform.position = (a + b) * 0.5f + Vector3.up * RoadY;
            go.transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            go.transform.localScale = new Vector3(width, 1f, len);
            SetLayerRecursive(go, roadLayer);

            var col = go.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(1f, ColliderThickness, 1f); // scales with the transform -> world (width, thick, len)

            ApplyMat(go, mat);
            Undo.RegisterCreatedObjectUndo(go, "Build Roads");
        }

        // A flat square (width x width) covering a polyline corner joint.
        private static void PlaceJoint(Transform parent, GameObject straightPrefab, Material mat, int roadLayer,
            Vector3 at, float width, string name)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(straightPrefab, parent);
            go.name = name;
            go.transform.position = new Vector3(at.x, RoadY + 0.005f, at.z);
            go.transform.rotation = Quaternion.identity;
            go.transform.localScale = new Vector3(width, 1f, width);
            SetLayerRecursive(go, roadLayer);

            var col = go.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(1f, ColliderThickness, 1f);

            ApplyMat(go, mat);
            Undo.RegisterCreatedObjectUndo(go, "Build Roads");
        }

        private static void ApplyMat(GameObject go, Material mat)
        {
            if (mat == null)
            {
                return;
            }

            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                mr.sharedMaterial = mat;
            }
        }

        // Solid dark asphalt for the blockout — the Kenney road colormap stretched
        // across a 16 x 50 m scaled tile just reads as smeared dashes; a flat dark
        // ribbon makes the network layout legible for review. Real Kenney road tiles
        // (curves, crossings, markings) come in the v3 pass.
        private static Material EnsureRoadMat() => EnsureMat("CityRoadBlockout", new Color(0.16f, 0.16f, 0.17f));

        private static void BuildRoundabouts(Transform parent)
        {
            Transform raParent = parent.Find("_Roundabouts");
            if (raParent != null)
            {
                return;
            }

            raParent = new GameObject("_Roundabouts").transform;
            raParent.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(raParent.gameObject, "Build Roundabouts");

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RoundaboutTile);
            GameObject straight = AssetDatabase.LoadAssetAtPath<GameObject>(StraightTile);
            int roadLayer = LayerMask.NameToLayer(RoadLayerName);
            Material fallbackMat = EnsureRoadMat();

            foreach (RoundaboutSpec ra in Roundabouts)
            {
                // road-roundabout FBX is 3x3 units — scale so its outer radius ~ ra.Radius.
                bool real = prefab != null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(real ? prefab : straight, raParent);
                go.name = ra.Name;
                go.transform.position = ra.Pos + Vector3.up * (RoadY + 0.01f);
                go.transform.localScale = real
                    ? Vector3.one * (ra.Radius * 2f / 3f)
                    : new Vector3(ra.Radius * 2f, 1f, ra.Radius * 2f);
                SetLayerRecursive(go, roadLayer);

                // No collider: a box over the whole roundabout footprint would trap
                // the car on the centre island. The ground plane under it carries the
                // vehicle; the primary road runs feeding in provide the Road-layer
                // surface RouteManager snaps to.

                // Real roundabout FBX keeps its own imported material (proper markings);
                // the straight fallback gets the flat blockout asphalt.
                if (!real)
                {
                    ApplyMat(go, fallbackMat);
                }

                Undo.RegisterCreatedObjectUndo(go, "Build Roundabouts");
            }

            Debug.Log($"[City] {Roundabouts.Length} kavsak donel adasi yerlestirildi.");
        }

        // ================================================================ graph helpers

        private static List<Vector3> SampleLane(Lane lane)
        {
            var outPts = new List<Vector3>();
            int segCount = lane.Loop ? lane.Points.Length : lane.Points.Length - 1;
            for (int i = 0; i < segCount; i++)
            {
                Vector3 a = lane.Points[i];
                Vector3 b = lane.Points[(i + 1) % lane.Points.Length];
                float len = (b - a).magnitude;
                int steps = Mathf.Max(1, Mathf.RoundToInt(len / WaypointSpacing));
                for (int s = 0; s < steps; s++)
                {
                    outPts.Add(Vector3.Lerp(a, b, s / (float)steps));
                }
            }

            if (!lane.Loop)
            {
                outPts.Add(lane.Points[lane.Points.Length - 1]);
            }

            return outPts;
        }

        private static bool Link(Waypoint a, Waypoint b)
        {
            if (a == null || b == null || a == b)
            {
                return false;
            }

            bool added = false;
            if (!a.neighbors.Contains(b))
            {
                a.neighbors.Add(b);
                added = true;
            }

            if (!b.neighbors.Contains(a))
            {
                b.neighbors.Add(a);
                added = true;
            }

            return added;
        }

        private static float FlatDist(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

        private static bool PathExists(Transform wpRoot, Vector3 from, Vector3 to)
        {
            Waypoint[] all = wpRoot.GetComponentsInChildren<Waypoint>();
            if (all.Length == 0)
            {
                return false;
            }

            Waypoint start = Nearest(all, from);
            Waypoint goal = Nearest(all, to);
            if (start == null || goal == null)
            {
                return false;
            }

            var seen = new HashSet<Waypoint> { start };
            var queue = new Queue<Waypoint>();
            queue.Enqueue(start);
            while (queue.Count > 0)
            {
                Waypoint cur = queue.Dequeue();
                if (cur == goal)
                {
                    return true;
                }

                foreach (Waypoint n in cur.neighbors)
                {
                    if (n != null && seen.Add(n))
                    {
                        queue.Enqueue(n);
                    }
                }
            }

            return false;
        }

        private static Waypoint Nearest(Waypoint[] all, Vector3 p)
        {
            Waypoint best = null;
            float bestD = float.MaxValue;
            foreach (Waypoint w in all)
            {
                float d = FlatDist(w.transform.position, p);
                if (d < bestD)
                {
                    bestD = d;
                    best = w;
                }
            }

            return best;
        }

        // ================================================================ misc helpers

        private static Transform CityChild(string path)
        {
            GameObject root = GameObject.Find(CityRootName);
            if (root == null)
            {
                root = new GameObject(CityRootName);
                Undo.RegisterCreatedObjectUndo(root, "Create CITY");
            }

            Transform current = root.transform;
            foreach (string part in path.Split('/'))
            {
                Transform next = current.Find(part);
                if (next == null)
                {
                    var go = new GameObject(part);
                    go.transform.SetParent(current, false);
                    Undo.RegisterCreatedObjectUndo(go, "Create " + part);
                    next = go.transform;
                }

                current = next;
            }

            return current;
        }

        private static Material EnsureMat(string name, Color color)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                mat = new Material(shader);
                DeliverySimSetup.EnsureFolder(MaterialsFolder);
                AssetDatabase.CreateAsset(mat, path);
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            if (layer < 0)
            {
                return;
            }

            go.layer = layer;
            foreach (Transform t in go.transform)
            {
                SetLayerRecursive(t.gameObject, layer);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            foreach (Transform c in root)
            {
                Transform r = FindDeep(c, name);
                if (r != null)
                {
                    return r;
                }
            }

            return null;
        }

        private static Component FindComponentInScene(string fullTypeName)
        {
            System.Type t = System.Type.GetType(fullTypeName + ", Assembly-CSharp");
            return t != null ? (Component)Object.FindAnyObjectByType(t) : null;
        }

        private static void WireRouteStart(GameObject vehicle)
        {
            if (vehicle == null)
            {
                return;
            }

            Component route = FindComponentInScene("DeliverySim.RouteManager");
            if (route == null)
            {
                return;
            }

            var so = new SerializedObject(route);
            SerializedProperty prop = so.FindProperty("routeStart");
            if (prop != null && prop.objectReferenceValue == null)
            {
                prop.objectReferenceValue = vehicle.transform;
                so.ApplyModifiedProperties();
            }
        }

        private static void ParentWalkingStreetIntoDowntown()
        {
            GameObject ws = GameObject.Find("WalkingStreet");
            if (ws == null)
            {
                return;
            }

            Transform downtown = CityChild("Districts/Downtown");
            if (ws.transform.parent != downtown)
            {
                Undo.SetTransformParent(ws.transform, downtown, "Parent WalkingStreet");
            }

            int roadLayer = LayerMask.NameToLayer(RoadLayerName);
            Transform roads = FindDeep(ws.transform, "Roads");
            if (roads != null && roadLayer >= 0)
            {
                SetLayerRecursive(roads.gameObject, roadLayer);
            }
        }

        private static void AddToOrderPool(OrderData order)
        {
            Component om = FindComponentInScene("DeliverySim.OrderManager");
            if (om == null || order == null)
            {
                return;
            }

            var so = new SerializedObject(om);
            SerializedProperty pool = so.FindProperty("orderPool");
            for (int i = 0; i < pool.arraySize; i++)
            {
                if (pool.GetArrayElementAtIndex(i).objectReferenceValue == order)
                {
                    return;
                }
            }

            pool.InsertArrayElementAtIndex(pool.arraySize);
            pool.GetArrayElementAtIndex(pool.arraySize - 1).objectReferenceValue = order;
            so.ApplyModifiedProperties();
        }

        private static void RemoveFromOrderPool(OrderData order)
        {
            Component om = FindComponentInScene("DeliverySim.OrderManager");
            if (om == null || order == null)
            {
                return;
            }

            var so = new SerializedObject(om);
            SerializedProperty pool = so.FindProperty("orderPool");
            for (int i = pool.arraySize - 1; i >= 0; i--)
            {
                if (pool.GetArrayElementAtIndex(i).objectReferenceValue == order)
                {
                    pool.DeleteArrayElementAtIndex(i);
                }
            }

            so.ApplyModifiedProperties();
        }
    }
}
