using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeliverySim.EditorTools
{
    /// <summary>
    /// Builds a genuinely new-looking district out of the Kenney "City Kit
    /// (Commercial)" and "City Kit (Suburban)" packs the user imported
    /// (CC0 — free for commercial use) instead of just re-tiling the same
    /// Tirgames "Stylized Street" block over and over.
    ///
    /// Deliberately does NOT place Kenney's road pieces: their FBX meshes have
    /// no readable "this is the forward/lane direction" metadata from outside
    /// the Editor, so blind placement risks half the tiles facing sideways —
    /// a real, driveable-road correctness problem. Buildings don't have that
    /// issue (a generic city building reads fine from any of the 4 cardinal
    /// facings), so this pass only places buildings, sitting on the existing
    /// flat ground Plane (already fully drivable, no new road needed there).
    ///
    /// Grid spacing is measured from the actual imported mesh bounds (not a
    /// guessed unit size), so buildings can't end up overlapping or absurdly
    /// gapped regardless of the kit's real-world scale.
    /// </summary>
    public static class KennyDistrictSetup
    {
        private const string KenneyRoot = "Assets/_Uber Simulator/Art/Assets/Kenny";
        private const string CommercialFolder = KenneyRoot + "/CityKitCommercial";
        private const string SuburbanFolder = KenneyRoot + "/CityKitSuburban";
        private const string RoadsFolder = KenneyRoot + "/CityKitRoads";
        private const string MaterialsFolder = "Assets/_Uber Simulator/Art/Materials";
        private const string DistrictRootName = "KennyDistrict";
        private const string RoadProbeRootName = "_KennyRoadProbe";
        // Gap between adjacent building centers, minus their own footprint, that's left
        // for the aisle/road between them — must exceed RoadWidth (see BuildRoadNetwork)
        // with room to spare on both sides for the player vehicle to actually steer.
        private const float BuildingGapMargin = 5f;

        // Minimum tile set that can build a rectangular Manhattan-style aisle grid
        // (matches the row/column aisle lines BuildWaypointGraph already derives) —
        // no roundabouts/bridges/elevation tiles needed for a flat district.
        private static readonly string[] RoadProbeTiles =
        {
            "road-straight", "road-bend", "road-crossroad", "road-intersection",
            "road-end", "road-side",
        };

        // Front rows (facing the Tirgames downtown, to the east) read as "shops",
        // back rows as "houses" — same commercial-center / suburb-edge logic real
        // towns have, so the two very different art styles read as two zones
        // instead of a jumble.
        private static readonly string[] CommercialBuildings =
        {
            "building-a", "building-b", "building-c", "building-d", "building-e",
            "building-f", "building-g", "building-h", "building-i", "building-j",
            "building-k", "building-l", "building-m", "building-n",
            "building-skyscraper-a", "building-skyscraper-b", "building-skyscraper-c",
        };

        private static readonly string[] SuburbanBuildings =
        {
            "building-type-a", "building-type-b", "building-type-c", "building-type-d",
            "building-type-e", "building-type-f", "building-type-g", "building-type-h",
            "building-type-i", "building-type-j", "building-type-k", "building-type-l",
            "building-type-m", "building-type-n", "building-type-o", "building-type-p",
            "building-type-q", "building-type-r", "building-type-s", "building-type-t",
            "building-type-u",
        };

        [MenuItem("DeliverySim/Setup/13 - Build Kenney District (Yeni Mahalle)")]
        public static void BuildDistrict()
        {
            if (GameObject.Find(DistrictRootName) != null)
            {
                Debug.Log("[Setup] '" + DistrictRootName + "' zaten sahnede — tekrar oluşturulmadı.");
                return;
            }

            Material commercialMat = EnsureKenneyMaterial(CommercialFolder, "KenneyCommercial");
            Material suburbanMat = EnsureKenneyMaterial(SuburbanFolder, "KenneySuburban");

            // Real footprint from the imported meshes — not a guessed grid unit —
            // so spacing can't come out overlapping or oddly sparse. Also records each
            // prefab's own mesh-bottom-to-pivot offset: Kenney FBX pivots are not
            // guaranteed to sit at the mesh base, so placing every instance at
            // localPosition.y = 0 made roughly half the grid float above the ground
            // and the other half sink into it — the single biggest reason the district
            // read as "bad" instead of a clean skyline. groundOffsets fixes that.
            var groundOffsets = new Dictionary<string, float>();
            float commercialFootprint = MeasureMaxFootprint(CommercialFolder, CommercialBuildings, groundOffsets);
            float suburbanFootprint = MeasureMaxFootprint(SuburbanFolder, SuburbanBuildings, groundOffsets);
            // Margin must fit a real driveable street between buildings, not just visual
            // breathing room — RoadWidth (BuildRoadNetwork) needs to sit inside this gap
            // with clearance on both sides for the player vehicle (~1.9m wide).
            float spacing = Mathf.Max(commercialFootprint, suburbanFootprint) + BuildingGapMargin;

            if (spacing <= BuildingGapMargin)
            {
                Debug.LogError("[Setup] Kenney bina mesh'leri ölçülemedi — FBX'ler doğru klasörde mi kontrol et: " +
                                $"'{CommercialFolder}', '{SuburbanFolder}'.");
                return;
            }

            GameObject root = new GameObject(DistrictRootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Kenney District");
            // West of the existing Tirgames district cluster (which spans roughly
            // X:-31..122, Z:-38..133) and safely inside the 300x300 ground Plane
            // (X/Z -150..150) — no overlap, no driving off the edge of the world.
            root.transform.position = new Vector3(-95f, 0f, 0f);

            const int columns = 6;
            const int rows = 4;
            var rng = new System.Random(12345); // deterministic — re-running regenerates the same layout

            for (int r = 0; r < rows; r++)
            {
                bool commercialRow = r < 2;
                string[] pool = commercialRow ? CommercialBuildings : SuburbanBuildings;
                string folder = commercialRow ? CommercialFolder : SuburbanFolder;
                Material mat = commercialRow ? commercialMat : suburbanMat;

                for (int c = 0; c < columns; c++)
                {
                    string prefabName = pool[rng.Next(pool.Length)];
                    GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{prefabName}.fbx");
                    if (prefab == null)
                    {
                        continue;
                    }

                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                    instance.name = $"{prefabName}_{r}_{c}";
                    string offsetKey = $"{folder}/{prefabName}";
                    float groundY = groundOffsets.TryGetValue(offsetKey, out float offset) ? offset : 0f;
                    instance.transform.localPosition = new Vector3(
                        (c - (columns - 1) * 0.5f) * spacing,
                        groundY,
                        r * spacing);
                    // Random cardinal facing: a background building reads fine from
                    // any of the 4 rotations, unlike a road tile, so this is safe.
                    instance.transform.localRotation = Quaternion.Euler(0f, rng.Next(4) * 90f, 0f);

                    ApplyMaterialRecursive(instance, mat);
                    Undo.RegisterCreatedObjectUndo(instance, "Build Kenney District");
                }
            }

            Selection.activeGameObject = root;
            EditorUtility.SetDirty(root);
            Debug.Log($"[Setup] Kenney mahallesi kuruldu: {rows * columns} bina (ön 2 sıra dükkan/Commercial, arka 2 sıra ev/Suburban), " +
                      $"'{DistrictRootName}' altında, ({root.transform.position.x:F0},0,0) konumunda, {spacing:F1}m aralıkla. " +
                      "Araç zaten oraya sürülebilir (mevcut düz zemin). Bir bina tuhaf yöne bakıyorsa Inspector'dan " +
                      "Y rotasyonunu 90'ar derece çevir — arka planda duran binalar için yön önemli değil, sadece göze " +
                      "tuhaf gelirse düzelt. Sahneyi kaydet (Ctrl+S).");
        }

        [MenuItem("DeliverySim/Setup/14 - Remove Kenney District")]
        public static void RemoveDistrict()
        {
            GameObject root = GameObject.Find(DistrictRootName);
            if (root == null)
            {
                Debug.Log("[Setup] '" + DistrictRootName + "' sahnede yok — silinecek bir şey yok.");
                return;
            }

            Undo.DestroyObjectImmediate(root);
            Debug.Log("[Setup] '" + DistrictRootName + "' kaldırıldı. Sahneyi kaydet (Ctrl+S).");
        }

        private const string KennyWaypointRootName = "_KennyRoadWaypoints";
        private const float KennyWaypointHeight = 0.15f; // matches DowntownMapSetup.WaypointHeight

        /// <summary>
        /// Kenney has no discrete road meshes (see BuildDistrict's doc comment above) —
        /// the whole district drives on one open ground Plane, so there's no
        /// RoadTilePositions-style array to extract like Downtown has. Instead this
        /// derives an aisle grid from the ACTUAL placed building positions (parsed
        /// from their "prefabName_row_col" name suffix), so the waypoint spacing
        /// always matches whatever BuildDistrict really used, without duplicating its
        /// footprint-measurement logic. Before this existed, orders in this district
        /// had zero waypoint graph — RouteManager fell back to a straight line from
        /// the nearest Downtown node, straight through buildings.
        /// </summary>
        [MenuItem("DeliverySim/Setup/15 - Build Kenney Waypoint Graph")]
        public static void BuildWaypointGraph()
        {
            if (GameObject.Find(KennyWaypointRootName) != null)
            {
                Debug.Log("[Setup] Kenney yol waypoint ağı zaten var — tekrar oluşturulmadı.");
                return;
            }

            GameObject districtRoot = GameObject.Find(DistrictRootName);
            if (districtRoot == null)
            {
                Debug.LogError("[Setup] '" + DistrictRootName + "' sahnede yok — önce '13 - Build Kenney District' çalıştır.");
                return;
            }

            if (!TryGetDistrictAisleGrid(districtRoot, out List<float> columnLines, out List<float> rowLines, out float spacing))
            {
                return;
            }

            GameObject root = new GameObject(KennyWaypointRootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Kenney Waypoints");

            var grid = new Waypoint[rowLines.Count, columnLines.Count];
            for (int ri = 0; ri < rowLines.Count; ri++)
            {
                for (int ci = 0; ci < columnLines.Count; ci++)
                {
                    GameObject go = new GameObject($"KW_{ri}_{ci}");
                    go.transform.SetParent(root.transform, false);
                    Vector3 localPos = new Vector3(columnLines[ci], KennyWaypointHeight, rowLines[ri]);
                    go.transform.position = districtRoot.transform.position + localPos;
                    grid[ri, ci] = go.AddComponent<Waypoint>();
                }
            }

            int linkCount = 0;
            for (int ri = 0; ri < rowLines.Count; ri++)
            {
                for (int ci = 0; ci < columnLines.Count; ci++)
                {
                    if (ci + 1 < columnLines.Count)
                    {
                        grid[ri, ci].neighbors.Add(grid[ri, ci + 1]);
                        linkCount++;
                    }

                    if (ri + 1 < rowLines.Count)
                    {
                        grid[ri, ci].neighbors.Add(grid[ri + 1, ci]);
                        linkCount++;
                    }
                }
            }

            // Bridge into whatever other district graph already exists (e.g. Downtown's
            // _RoadWaypoints) — same nearest-pair idea DowntownMapSetup.ExtendWaypointGraph
            // uses. Without this, Kenney's grid would be an island: RouteManager could
            // route within it but never through it to reach the rest of the map.
            var otherNodes = new List<Waypoint>();
            foreach (Waypoint w in Object.FindObjectsByType<Waypoint>(FindObjectsSortMode.None))
            {
                if (!w.transform.IsChildOf(root.transform))
                {
                    otherNodes.Add(w);
                }
            }

            if (otherNodes.Count > 0)
            {
                Waypoint bestOld = null;
                Waypoint bestNew = null;
                float bestDist = float.MaxValue;

                foreach (Waypoint oldNode in otherNodes)
                {
                    for (int ri = 0; ri < rowLines.Count; ri++)
                    {
                        for (int ci = 0; ci < columnLines.Count; ci++)
                        {
                            float dist = Vector3.Distance(oldNode.transform.position, grid[ri, ci].transform.position);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                bestOld = oldNode;
                                bestNew = grid[ri, ci];
                            }
                        }
                    }
                }

                if (bestOld != null && bestNew != null)
                {
                    bestOld.neighbors.Add(bestNew);
                    bestNew.neighbors.Add(bestOld);
                    Debug.Log($"[Setup] Kenney yol ağı diğer bölgeye bağlandı: '{bestOld.name}' <-> '{bestNew.name}' (mesafe {bestDist:F1}m).");
                }
            }
            else
            {
                Debug.LogWarning("[Setup] Bağlanacak başka waypoint bulunamadı (Downtown ağı henüz kurulmamış olabilir) — Kenney ağı izole kaldı.");
            }

            EditorUtility.SetDirty(root);
            Selection.activeGameObject = root;
            Debug.Log($"[Setup] Kenney mahallesine {grid.Length} waypoint, {linkCount} komşuluk bağlantısıyla " +
                      $"({rowLines.Count}x{columnLines.Count} ızgara, {spacing:F1}m aralık) eklendi. RouteManager artık " +
                      "bu bölgede de yola göre çizecek (bina arasından kestirme yok). Sahneyi kaydet (Ctrl+S).");
        }

        /// <summary>
        /// Derives the aisle-line lattice (in districtRoot LOCAL space) from the actual
        /// placed "prefabName_row_col" building instances, so the waypoint graph AND the
        /// road mesh network (BuildWaypointGraph, BuildRoadNetwork) always agree on where
        /// the streets are — neither one guesses independently.
        /// </summary>
        private static bool TryGetDistrictAisleGrid(GameObject districtRoot, out List<float> columnLines,
            out List<float> rowLines, out float spacing)
        {
            columnLines = null;
            rowLines = null;
            spacing = 0f;

            var colX = new Dictionary<int, float>();
            var rowZ = new Dictionary<int, float>();

            foreach (Transform child in districtRoot.transform)
            {
                string[] parts = child.name.Split('_');
                if (parts.Length < 3)
                {
                    continue;
                }

                if (!int.TryParse(parts[parts.Length - 1], out int c) ||
                    !int.TryParse(parts[parts.Length - 2], out int r))
                {
                    continue;
                }

                colX[c] = child.localPosition.x;
                rowZ[r] = child.localPosition.z;
            }

            if (colX.Count < 2 || rowZ.Count < 2)
            {
                Debug.LogError("[Setup] Kenney bina grid'i okunamadı — '" + DistrictRootName +
                                "' altında beklenen 'isim_satır_sütun' adlı obje bulunamadı.");
                return false;
            }

            var cols = new List<int>(colX.Keys);
            var rows = new List<int>(rowZ.Keys);
            cols.Sort();
            rows.Sort();

            spacing = Mathf.Abs(rowZ[rows[1]] - rowZ[rows[0]]);
            if (spacing <= 0.01f)
            {
                Debug.LogError("[Setup] Kenney bina aralığı ölçülemedi (spacing <= 0) — grid hesaplanamadı.");
                return false;
            }

            float halfSpacing = spacing * 0.5f;

            // Aisle lines: one more than the row/column count, bounding every row/column
            // (e.g. 4 building rows -> 5 aisle lines running east-west between/around them).
            columnLines = new List<float>(cols.Count + 1);
            for (int i = 0; i <= cols.Count; i++)
            {
                columnLines.Add(colX[cols[0]] - halfSpacing + i * spacing);
            }

            rowLines = new List<float>(rows.Count + 1);
            for (int i = 0; i <= rows.Count; i++)
            {
                rowLines.Add(rowZ[rows[0]] - halfSpacing + i * spacing);
            }

            return true;
        }

        /// <summary>
        /// One-time diagnostic: spawns each candidate CityKitRoads tile in a row, far
        /// from any gameplay area, so their true forward/lane direction can be read
        /// visually from a top-down screenshot (lane markings, curb asymmetry) instead
        /// of guessed from FBX metadata — see the class doc comment for why blind
        /// placement was avoided originally. Re-running clears and rebuilds the row, so
        /// it's safe to invoke repeatedly while inspecting. Findings should be recorded
        /// as fixed Y-rotation offsets in BuildRoadNetwork once determined.
        /// </summary>
        [MenuItem("DeliverySim/Setup/17 - Probe Kenney Road Tile Orientations")]
        public static void ProbeRoadTileOrientations()
        {
            GameObject existing = GameObject.Find(RoadProbeRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            var groundOffsets = new Dictionary<string, float>();
            float spacing = MeasureMaxFootprint(RoadsFolder, RoadProbeTiles, groundOffsets) + 2.5f;
            if (spacing <= 2.5f)
            {
                Debug.LogError("[Setup] Kenney yol mesh'leri ölçülemedi — FBX'ler doğru klasörde mi kontrol et: '" + RoadsFolder + "'.");
                return;
            }

            Material roadMat = EnsureKenneyMaterial(RoadsFolder, "KenneyRoads");

            GameObject root = new GameObject(RoadProbeRootName);
            Undo.RegisterCreatedObjectUndo(root, "Probe Kenney Road Tiles");
            // Far off in +Z, well outside any driveable/gameplay zone (Downtown ends
            // around Z:133, Kenney's building grid around Z:0..3*spacing) — purely a
            // scratch inspection area, never touched by BuildRoadNetwork or gameplay.
            root.transform.position = new Vector3(-95f, 0f, 300f);

            var legend = new System.Text.StringBuilder();
            legend.AppendLine("[Setup] Kenney yol tile probe dizildi — üstten screenshot al, her tile'ın yönünü " +
                               "(colormap'teki şerit/kaldırım asimetrisine bakarak) incele. Sırayla, +X yönünde:");

            for (int i = 0; i < RoadProbeTiles.Length; i++)
            {
                string tileName = RoadProbeTiles[i];
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoadsFolder}/{tileName}.fbx");
                if (prefab == null)
                {
                    Debug.LogWarning($"[Setup] '{RoadsFolder}/{tileName}.fbx' bulunamadı, atlandı.");
                    continue;
                }

                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = $"{i}_{tileName}";
                float groundY = groundOffsets.TryGetValue($"{RoadsFolder}/{tileName}", out float offset) ? offset : 0f;
                instance.transform.localPosition = new Vector3(i * spacing, groundY, 0f);
                instance.transform.localRotation = Quaternion.identity; // baseline: no rotation applied, this IS the raw FBX facing
                ApplyMaterialRecursive(instance, roadMat);
                Undo.RegisterCreatedObjectUndo(instance, "Probe Kenney Road Tiles");

                legend.AppendLine($"  [{i}] '{tileName}' — dünya konumu ({root.transform.position.x + i * spacing:F0}, 0, 300), rotasyon uygulanmadı (ham FBX yönü).");
            }

            Selection.activeGameObject = root;
            EditorUtility.SetDirty(root);
            Debug.Log(legend.ToString());
            Debug.Log("[Setup] Probe tamamlandı. Scene view'i bu satıra odakla, üstten (top-down) bak, her tile'ın " +
                      "gerçek 'ileri' yönünü not al, sonra '18 - Build Kenney Road Network' içindeki RoadTileYOffset " +
                      "dictionary'sine düzeltme açısını yaz. İşin bitince bu geçici objeyi silmek için tekrar bu menüyü " +
                      "çalıştırmak günceller, elle silmek istersen '" + RoadProbeRootName + "' objesini sahneden kaldır.");
        }

        private const string KennyRoadNetworkRootName = "_KennyRoadNetwork";
        private const float RoadWidth = 3f; // world meters — fits inside BuildingGapMargin with ~1m clearance each side for the ~1.9m-wide player vehicle
        private const float RoadYEpsilon = 0.02f; // lifts the road mesh just above the ground Plane, avoids z-fighting

        // Verified via '17 - Probe Kenney Road Tile Orientations': every CityKitRoads
        // tile's UNROTATED (Quaternion.identity) forward/open axis is local-X (world
        // East-West in this project), not local-Z as often assumed — confirmed from a
        // top-down screenshot by matching curb/lane-marking asymmetry across 4 tile
        // types (road-straight's lane line runs E-W; road-bend's open quarter faces
        // West+North; road-intersection's single curbed side faces South; road-end's
        // single open side faces South). These four Y-offsets are derived from that
        // one shared convention — see BuildRoadNetwork's corner/T switch below for the
        // reasoning per grid position.
        private const string RoadStraightTile = "road-straight";
        private const string RoadBendTile = "road-bend";
        private const string RoadCrossroadTile = "road-crossroad";
        private const string RoadIntersectionTile = "road-intersection"; // T-junction, one side closed

        /// <summary>
        /// Builds real, driveable CityKitRoads geometry over the aisle lattice
        /// BuildWaypointGraph already derived (via the shared TryGetDistrictAisleGrid),
        /// so the visual road and the routing graph are guaranteed to line up. Every
        /// placed tile gets a real (non-trigger) BoxCollider on the "Road" layer, which
        /// is what RouteManager.GroundSnap needs to hug this district's streets instead
        /// of falling back to its flat fallbackGroundY. Idempotent: clears and rebuilds
        /// if run again, so tuning RoadWidth/rotations is safe to iterate on.
        /// </summary>
        [MenuItem("DeliverySim/Setup/18 - Build Kenney Road Network")]
        public static void BuildRoadNetwork()
        {
            GameObject districtRoot = GameObject.Find(DistrictRootName);
            if (districtRoot == null)
            {
                Debug.LogError("[Setup] '" + DistrictRootName + "' sahnede yok — önce '13 - Build Kenney District' çalıştır.");
                return;
            }

            if (!TryGetDistrictAisleGrid(districtRoot, out List<float> columnLines, out List<float> rowLines, out float spacing))
            {
                return;
            }

            if (spacing <= RoadWidth)
            {
                Debug.LogError($"[Setup] Kenney bina aralığı ({spacing:F1}m) RoadWidth'ten ({RoadWidth:F1}m) küçük — " +
                                "yol kavşakları çakışır. RoadWidth'i küçült veya bina spacing'ini büyüt.");
                return;
            }

            GameObject existing = GameObject.Find(KennyRoadNetworkRootName);
            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing);
            }

            int roadLayer = LayerMask.NameToLayer("Road");
            if (roadLayer < 0)
            {
                Debug.LogError("[Setup] 'Road' layer'ı yok — önce '16 - Assign Road Physics Layer' çalıştır.");
                return;
            }

            var groundOffsets = new Dictionary<string, float>();
            MeasureMaxFootprint(RoadsFolder, new[] { RoadStraightTile, RoadBendTile, RoadCrossroadTile, RoadIntersectionTile }, groundOffsets);

            Material roadMat = EnsureKenneyMaterial(RoadsFolder, "KenneyRoads");

            GameObject root = new GameObject(KennyRoadNetworkRootName);
            Undo.RegisterCreatedObjectUndo(root, "Build Kenney Road Network");
            root.transform.position = districtRoot.transform.position;

            int junctionCount = 0;
            int segmentCount = 0;

            // Junctions: every aisle intersection is a crossroad (interior), a T (one
            // grid edge, not a corner), or an L-bend (one of the 4 rectangle corners).
            for (int ri = 0; ri < rowLines.Count; ri++)
            {
                bool minR = ri == 0;
                bool maxR = ri == rowLines.Count - 1;

                for (int ci = 0; ci < columnLines.Count; ci++)
                {
                    bool minC = ci == 0;
                    bool maxC = ci == columnLines.Count - 1;
                    bool onCEdge = minC || maxC;
                    bool onREdge = minR || maxR;

                    string tileName;
                    float yRotation;

                    if (onCEdge && onREdge)
                    {
                        tileName = RoadBendTile;
                        // Open legs must point INTO the grid along both boundary edges
                        // meeting at this corner — see class-level derivation notes.
                        if (minC && minR) yRotation = 90f;       // SW: open North+East
                        else if (minC && maxR) yRotation = 180f; // NW: open South+East
                        else if (maxC && minR) yRotation = 0f;   // SE: open North+West (tile default)
                        else yRotation = 270f;                   // NE: open South+West
                    }
                    else if (onCEdge || onREdge)
                    {
                        tileName = RoadIntersectionTile;
                        // Closed leg points OUTWARD, off the grid.
                        if (minR) yRotation = 0f;        // south boundary: closed South (tile default)
                        else if (maxR) yRotation = 180f; // north boundary: closed North
                        else if (minC) yRotation = 90f;  // west boundary: closed West
                        else yRotation = 270f;            // east boundary: closed East
                    }
                    else
                    {
                        tileName = RoadCrossroadTile; // interior 4-way — symmetric, rotation irrelevant
                        yRotation = 0f;
                    }

                    PlaceRoadTile(root, RoadsFolder, tileName, roadMat, groundOffsets, roadLayer,
                        new Vector3(columnLines[ci], 0f, rowLines[ri]), yRotation,
                        new Vector3(RoadWidth, 1f, RoadWidth), $"Junction_{ri}_{ci}_{tileName}");
                    junctionCount++;
                }
            }

            // East-West straight segments (along each row line, default Y=0 — tile's
            // unrotated open axis already matches world X per the probe findings).
            for (int ri = 0; ri < rowLines.Count; ri++)
            {
                for (int ci = 0; ci < columnLines.Count - 1; ci++)
                {
                    float length = (columnLines[ci + 1] - columnLines[ci]) - RoadWidth;
                    if (length <= 0.05f)
                    {
                        continue;
                    }

                    float midX = (columnLines[ci] + columnLines[ci + 1]) * 0.5f;
                    PlaceRoadTile(root, RoadsFolder, RoadStraightTile, roadMat, groundOffsets, roadLayer,
                        new Vector3(midX, 0f, rowLines[ri]), 0f,
                        new Vector3(length, 1f, RoadWidth), $"SegmentEW_{ri}_{ci}");
                    segmentCount++;
                }
            }

            // North-South straight segments (along each column line, rotated 90° so the
            // tile's open axis swaps from world X to world Z).
            for (int ci = 0; ci < columnLines.Count; ci++)
            {
                for (int ri = 0; ri < rowLines.Count - 1; ri++)
                {
                    float length = (rowLines[ri + 1] - rowLines[ri]) - RoadWidth;
                    if (length <= 0.05f)
                    {
                        continue;
                    }

                    float midZ = (rowLines[ri] + rowLines[ri + 1]) * 0.5f;
                    PlaceRoadTile(root, RoadsFolder, RoadStraightTile, roadMat, groundOffsets, roadLayer,
                        new Vector3(columnLines[ci], 0f, midZ), 90f,
                        new Vector3(length, 1f, RoadWidth), $"SegmentNS_{ci}_{ri}");
                    segmentCount++;
                }
            }

            EditorUtility.SetDirty(root);
            Selection.activeGameObject = root;
            Debug.Log($"[Setup] Kenney yol ağı kuruldu: {junctionCount} kavşak + {segmentCount} düz segment, " +
                      $"RoadWidth={RoadWidth:F1}m, hepsi 'Road' layer'da gerçek collider'la. RouteManager artık bu " +
                      "bölgede de yola yapışacak. Rotasyonlardan biri yanlış görünüyorsa (araç yola göre çapraz " +
                      "sürüyor gibi), ilgili tile'ın Inspector'daki Y rotasyonunu 90 derecelik adımlarla elle düzelt " +
                      "— bu geçici bir uyuşmazlık, kod tekrar çalıştırılınca sıfırlanır. Sahneyi kaydet (Ctrl+S).");
        }

        [MenuItem("DeliverySim/Setup/19 - Remove Kenney Road Network")]
        public static void RemoveRoadNetwork()
        {
            GameObject root = GameObject.Find(KennyRoadNetworkRootName);
            if (root == null)
            {
                Debug.Log("[Setup] '" + KennyRoadNetworkRootName + "' sahnede yok — silinecek bir şey yok.");
                return;
            }

            Undo.DestroyObjectImmediate(root);
            Debug.Log("[Setup] '" + KennyRoadNetworkRootName + "' kaldırıldı. Sahneyi kaydet (Ctrl+S).");
        }

        // Anchored to real placed buildings (see BuildDistrict's "prefabName_row_col"
        // naming) so points sit next to an actual landmark instead of a bare street
        // corner. Each point is offset 3m south (toward that row's own street aisle)
        // from its anchor building's center — well within VehicleInteractor's 6m
        // interact radius of the new road network built by BuildRoadNetwork.
        private static readonly (string objectName, string pointId, string anchorBuilding)[] KennyPickups =
        {
            ("Kenney_Pickup_Office", "kenney_pickup_office", "building-skyscraper-a_1_3"),
            ("Kenney_Pickup_Shop", "kenney_pickup_shop", "building-b_0_0"),
        };

        private static readonly (string objectName, string pointId, string anchorBuilding)[] KennyDeliveries =
        {
            ("Kenney_Delivery_HouseA", "kenney_delivery_house_a", "building-type-a_3_0"),
            ("Kenney_Delivery_HouseB", "kenney_delivery_house_b", "building-type-f_2_5"),
            ("Kenney_Delivery_HouseC", "kenney_delivery_house_c", "building-type-h_3_5"),
        };

        /// <summary>
        /// Adds 2 pickup + 3 delivery points + 1 fuel + 1 repair station inside Kenney,
        /// reusing DeliverySimSetup's CreatePoint/CreateStation helpers (promoted to
        /// internal) so InteractionPoint/OrderData wiring stays identical to Downtown's.
        /// Requires '18 - Build Kenney Road Network' to have run first (anchors are
        /// resolved from BuildDistrict's building instances, not the road network
        /// itself, but the points are useless without driveable streets nearby).
        /// </summary>
        [MenuItem("DeliverySim/Setup/20 - Populate Kenney Gameplay Points")]
        public static void PopulateGameplayPoints()
        {
            GameObject districtRoot = GameObject.Find(DistrictRootName);
            if (districtRoot == null)
            {
                Debug.LogError("[Setup] '" + DistrictRootName + "' sahnede yok — önce '13 - Build Kenney District' çalıştır.");
                return;
            }

            if (GameObject.Find(KennyRoadNetworkRootName) == null)
            {
                Debug.LogWarning("[Setup] '" + KennyRoadNetworkRootName + "' henüz yok — önce '18 - Build Kenney Road Network' " +
                                  "çalıştırman önerilir, yoksa bu noktalara sürülecek yol olmayabilir. Yine de devam ediliyor.");
            }

            DeliverySimSetup.EnsureFolder(DeliverySimSetup.DataFolderRoot);

            int created = 0;
            foreach (var p in KennyPickups)
            {
                if (TryGetAnchorPosition(districtRoot, p.anchorBuilding, out Vector3 pos))
                {
                    DeliverySimSetup.CreatePoint<PickupPoint>(p.objectName, p.pointId, pos + new Vector3(0f, 0f, -3f));
                    created++;
                }
            }

            foreach (var d in KennyDeliveries)
            {
                if (TryGetAnchorPosition(districtRoot, d.anchorBuilding, out Vector3 pos))
                {
                    DeliverySimSetup.CreatePoint<DeliveryPoint>(d.objectName, d.pointId, pos + new Vector3(0f, 0f, -3f));
                    created++;
                }
            }

            if (TryGetAnchorPosition(districtRoot, "building-d_1_2", out Vector3 fuelAnchor))
            {
                DeliverySimSetup.CreateStation<FuelStation>("Kenney_FuelStation", fuelAnchor + new Vector3(0f, 0f, -3f));
                created++;
            }

            if (TryGetAnchorPosition(districtRoot, "building-type-i_2_2", out Vector3 repairAnchor))
            {
                DeliverySimSetup.CreateStation<RepairStation>("Kenney_RepairStation", repairAnchor + new Vector3(0f, 0f, -3f));
                created++;
            }

            Debug.Log($"[Setup] Kenney gameplay noktaları eklendi ({created}/7 hedeflenen): 2 alım, 3 teslim, " +
                      "1 yakıt, 1 tamir. OrderData eşleştirmesi için pointId'ler: kenney_pickup_office, " +
                      "kenney_pickup_shop, kenney_delivery_house_a/b/c. Sahneyi kaydet (Ctrl+S).");
        }

        private static bool TryGetAnchorPosition(GameObject districtRoot, string childName, out Vector3 worldPosition)
        {
            Transform child = districtRoot.transform.Find(childName);
            if (child == null)
            {
                Debug.LogWarning($"[Setup] '{childName}' KennyDistrict altında bulunamadı — o nokta atlandı (bina rastgele seçildiği için isim farklı çıkmış olabilir).");
                worldPosition = Vector3.zero;
                return false;
            }

            worldPosition = child.position;
            return true;
        }

        /// <summary>
        /// Instantiates one CityKitRoads tile, non-uniformly scaled to fit its slot
        /// (length along local X = travel axis, width along local Z), with a real
        /// BoxCollider on the Road layer sized to match — collider size is defined in
        /// the tile's own unscaled 1x1 unit space, so Transform.localScale stretches
        /// both the mesh and the collider together automatically.
        /// </summary>
        private static void PlaceRoadTile(GameObject parent, string folder, string tileName, Material mat,
            Dictionary<string, float> groundOffsets, int roadLayer, Vector3 localPosXZ, float yRotation,
            Vector3 scale, string instanceName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{tileName}.fbx");
            if (prefab == null)
            {
                Debug.LogWarning($"[Setup] '{folder}/{tileName}.fbx' bulunamadı, atlandı.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent.transform);
            instance.name = instanceName;
            float groundY = groundOffsets.TryGetValue($"{folder}/{tileName}", out float offset) ? offset : 0f;
            instance.transform.localPosition = new Vector3(localPosXZ.x, groundY + RoadYEpsilon, localPosXZ.z);
            instance.transform.localRotation = Quaternion.Euler(0f, yRotation, 0f);
            instance.transform.localScale = scale;
            instance.layer = roadLayer;

            BoxCollider collider = instance.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.01f, 0f);
            collider.size = new Vector3(1f, 0.02f, 1f);

            ApplyMaterialRecursive(instance, mat);
            Undo.RegisterCreatedObjectUndo(instance, "Build Kenney Road Network");
        }

        /// <summary>
        /// Measures max X/Z footprint across all named prefabs, and records each
        /// prefab's world-space bounds.min.y (measured with the temp instance at
        /// the origin, so min.y IS the pivot-to-base offset) into groundOffsets
        /// keyed by "folder/name". BuildDistrict negates that into localPosition.y
        /// so every instance's mesh base — not its pivot — touches the ground.
        /// </summary>
        private static float MeasureMaxFootprint(string folder, IReadOnlyList<string> names,
            Dictionary<string, float> groundOffsets)
        {
            float max = 0f;
            foreach (string name in names)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{name}.fbx");
                if (prefab == null)
                {
                    continue;
                }

                GameObject temp = (GameObject)Object.Instantiate(prefab);
                try
                {
                    Bounds? combined = null;
                    foreach (Renderer r in temp.GetComponentsInChildren<Renderer>())
                    {
                        if (combined == null)
                        {
                            combined = r.bounds;
                        }
                        else
                        {
                            Bounds b = combined.Value;
                            b.Encapsulate(r.bounds);
                            combined = b;
                        }
                    }

                    if (combined != null)
                    {
                        max = Mathf.Max(max, combined.Value.size.x, combined.Value.size.z);
                        groundOffsets[$"{folder}/{name}"] = -combined.Value.min.y;
                    }
                }
                finally
                {
                    Object.DestroyImmediate(temp);
                }
            }

            return max;
        }

        private static Material EnsureKenneyMaterial(string kitFolder, string materialName)
        {
            string matPath = $"{MaterialsFolder}/{materialName}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                return mat;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                shader = Shader.Find("Standard");
            }

            mat = new Material(shader);

            Texture2D colormap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{kitFolder}/Textures/colormap.png");
            if (colormap != null)
            {
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTexture("_BaseMap", colormap);
                }

                if (mat.HasProperty("_MainTex"))
                {
                    mat.SetTexture("_MainTex", colormap);
                }
            }
            else
            {
                Debug.LogWarning($"[Setup] '{kitFolder}/Textures/colormap.png' bulunamadı — '{materialName}' dokusuz kaldı.");
            }

            EnsureFolderExists(MaterialsFolder);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        private static void ApplyMaterialRecursive(GameObject go, Material mat)
        {
            foreach (MeshRenderer renderer in go.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = mat;
            }
        }

        private static void EnsureFolderExists(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int lastSlash = path.LastIndexOf('/');
            string parent = path.Substring(0, lastSlash);
            string folderName = path.Substring(lastSlash + 1);
            EnsureFolderExists(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
