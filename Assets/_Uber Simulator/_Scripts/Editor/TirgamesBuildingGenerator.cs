using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeliverySim.EditorTools
{
    /// <summary>
    /// Assembles NEW buildings out of Tirgames "StylizedWorld"'s modular Architecture
    /// kit (floor/wall/roof pieces) instead of reusing the vendor's single fixed
    /// "WalkingStreet" block (only 9 buildings, see DowntownMapSetup) — needed because
    /// a big single city needs far more than 9 distinct buildings.
    ///
    /// Module geometry (measured directly off the imported meshes, not guessed):
    /// every floor/wall piece is a 4.5m x 4.5m footprint with its pivot at one
    /// footprint CORNER (not centered) — floor spans local X:[-4.5,0] Z:[0,4.5],
    /// walls span local X:[-4.5,0], are 3.5m tall (pivot at the bottom), 0.2m thick,
    /// centered on Z=0. Door and window wall variants share the exact same bounds as
    /// the plain wall, so they're fully interchangeable in this socket. Building a
    /// 4-wall ring around one floor square means placing a wall at each of the
    /// square's 4 corners, rotated 0/90/180/270 in turn (BuildingCorners below).
    ///
    /// The roof piece (M01Roof01_x) is a mono-pitch (single-slope) wedge, not a
    /// symmetrical cap — verified against the vendor's own WalkingStreet scene data
    /// (StylizedStreet.unity) and by direct measurement: local bounds X:[-4.5,0]
    /// Y:[-0.092,3.746] Z:[-4.5,0.262]. Placed at the building's "north" corner
    /// (0, wallTopY, 4.5) with no extra rotation, its low edge sits flush on the
    /// north wall top and it slopes up toward the south — by design (a stylized
    /// mono-pitch roof), not a placement bug. The gap under the high (south) eave
    /// isn't visible from normal street-level/chase-cam angles.
    /// </summary>
    public static class TirgamesBuildingGenerator
    {
        private const string ArchFolder = "Assets/TirgamesAssets/StylizedWorld/Architecture/Prefabs";

        public const float ModuleSize = 4.5f;   // floor/wall footprint width+depth
        public const float WallHeight = 3.5f;   // one story
        private const float RoofBottomOffset = 0.092f; // local min.y of M01Roof01_x, measured

        // Only "_1" is actually a 4.5x4.5 slab matching the wall ring — measured all 9
        // numbered variants and they turn out to be DIFFERENT floor-plan sizes (2.25,
        // 3.5, 6.75, even a 9x9 slab for _6), not cosmetic re-skins of the same
        // footprint. Using any of the others made buildings spill into neighboring
        // lots/streets. Same story for roofs below (M01Roof01_2 is a completely
        // different flat/thin shape, not a pitch-style variant).
        private static readonly string[] FloorVariants = { "M01Floor01_1" };

        private static readonly string[] DoorWallVariants =
        {
            "M01WallDoorTypeA_1", "M01WallDoorTypeA_2", "M01WallDoorTypeB_1",
        };

        private static readonly string[] WindowWallVariants =
        {
            "M01WallWindowTypeA_1", "M01WallWindowTypeA_2", "M01WallWindowTypeA_3",
            "M01WallWindowTypeB_1", "M01WallWindowTypeB_2",
            "M01WallWindowTypeE_1", "M01WallWindowTypeE_2",
            "M01WallWindowTypeG_1", "M01WallWindowTypeG_2",
            "M01WallWindowTypeH_1", "M01WallWindowTypeH_2",
        };

        // _1 and _4 measured with matching bounds/orientation convention (min.z=-4.5,
        // same low-north/high-south pitch direction as PlaceBuilding assumes). _2 is a
        // near-flat 0.38-deep shape (different piece entirely) and _3's Z bounds run
        // the opposite direction (min.z=0) — neither drops in without extra handling,
        // so left out rather than risking another floating/misaligned roof.
        private static readonly string[] RoofVariants = { "M01Roof01_1", "M01Roof01_4" };

        // The 4 wall-ring corners of one ModuleSize x ModuleSize floor square, and the
        // Y-rotation that orients a wall (whose own local shape spans X:[-4.5,0] at
        // Z=0) to lie along that corner's edge. Order: south, west, north, east.
        private static readonly Vector3[] CornerOffsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(-ModuleSize, 0f, 0f),
            new Vector3(-ModuleSize, 0f, ModuleSize),
            new Vector3(0f, 0f, ModuleSize),
        };

        private static readonly float[] CornerYRotations = { 0f, 90f, 180f, 270f };

        /// <summary>
        /// Builds one building (1-3 floors, randomized floor/wall/roof variants, a door
        /// on one randomly-chosen ground-floor corner) at basePosition. The footprint
        /// itself is NEVER rotated — it always occupies the fixed square
        /// X:[-ModuleSize,0] Z:[0,ModuleSize] relative to basePosition (matching
        /// CornerOffsets exactly), because callers that lay buildings out on a street
        /// grid (TirgamesCitySetup) size their road cells around that fixed footprint.
        /// An earlier version let the whole building rotate for "facing" variety, which
        /// silently shifted the footprint into a neighboring street cell for 3 of every
        /// 4 buildings — door-corner selection gives the same visual variety (which
        /// side of the lot the entrance faces) without moving the footprint at all.
        /// Deterministic per seed — same seed always produces the same building.
        /// </summary>
        public static GameObject BuildBuilding(Transform parent, string name, Vector3 basePosition, int seed)
        {
            var rng = new System.Random(seed);
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = basePosition;
            root.transform.rotation = Quaternion.identity;

            int floorCount = 1 + rng.Next(3); // 1-3 stories
            int doorCorner = rng.Next(4);

            for (int floor = 0; floor < floorCount; floor++)
            {
                float floorY = floor * WallHeight;
                string floorVariant = FloorVariants[rng.Next(FloorVariants.Length)];
                SpawnPiece(root.transform, floorVariant, basePosition, new Vector3(0f, floorY, 0f), 0f);

                for (int corner = 0; corner < 4; corner++)
                {
                    bool groundDoor = floor == 0 && corner == doorCorner;
                    string wallVariant = groundDoor
                        ? DoorWallVariants[rng.Next(DoorWallVariants.Length)]
                        : WindowWallVariants[rng.Next(WindowWallVariants.Length)];

                    Vector3 localPos = CornerOffsets[corner] + new Vector3(0f, floorY, 0f);
                    SpawnPiece(root.transform, wallVariant, basePosition, localPos, CornerYRotations[corner]);
                }
            }

            float roofY = floorCount * WallHeight + RoofBottomOffset;
            string roofVariant = RoofVariants[rng.Next(RoofVariants.Length)];
            SpawnPiece(root.transform, roofVariant, basePosition, new Vector3(0f, roofY, ModuleSize), 0f);

            return root;
        }

        private static void SpawnPiece(Transform parent, string prefabName, Vector3 buildingBasePosition,
            Vector3 localOffset, float pieceLocalYRotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArchFolder}/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[Setup] '{ArchFolder}/{prefabName}.prefab' bulunamadı, atlandı.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = prefabName;
            instance.transform.position = buildingBasePosition + localOffset;
            instance.transform.rotation = Quaternion.Euler(0f, pieceLocalYRotation, 0f);
            Undo.RegisterCreatedObjectUndo(instance, "Build Tirgames Building");
        }
    }
}
