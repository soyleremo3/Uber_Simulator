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
        private const string MaterialsFolder = "Assets/_Uber Simulator/Art/Materials";
        private const string DistrictRootName = "KennyDistrict";

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
            float spacing = Mathf.Max(commercialFootprint, suburbanFootprint) + 2.5f; // +margin so meshes never touch

            if (spacing <= 2.5f)
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
                return;
            }

            var cols = new List<int>(colX.Keys);
            var rows = new List<int>(rowZ.Keys);
            cols.Sort();
            rows.Sort();

            float spacing = Mathf.Abs(rowZ[rows[1]] - rowZ[rows[0]]);
            if (spacing <= 0.01f)
            {
                Debug.LogError("[Setup] Kenney bina aralığı ölçülemedi (spacing <= 0) — grid hesaplanamadı.");
                return;
            }

            float halfSpacing = spacing * 0.5f;

            // Aisle lines: one more than the row/column count, bounding every row/column
            // (e.g. 4 building rows -> 5 aisle lines running east-west between/around them).
            var columnLines = new List<float>(cols.Count + 1);
            for (int i = 0; i <= cols.Count; i++)
            {
                columnLines.Add(colX[cols[0]] - halfSpacing + i * spacing);
            }

            var rowLines = new List<float>(rows.Count + 1);
            for (int i = 0; i <= rows.Count; i++)
            {
                rowLines.Add(rowZ[rows[0]] - halfSpacing + i * spacing);
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
