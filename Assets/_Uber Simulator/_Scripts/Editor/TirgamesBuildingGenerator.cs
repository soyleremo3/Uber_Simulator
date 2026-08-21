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

        private static readonly string[] FloorVariants =
        {
            "M01Floor01_1", "M01Floor01_2", "M01Floor01_3", "M01Floor01_4", "M01Floor01_5",
            "M01Floor01_6", "M01Floor01_7", "M01Floor01_8", "M01Floor01_9",
        };

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

        private static readonly string[] RoofVariants =
        {
            "M01Roof01_1", "M01Roof01_2", "M01Roof01_3", "M01Roof01_4",
        };

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
        /// on the ground floor's south side) at basePosition, facing buildingYRotation
        /// (must be a multiple of 90 to stay socket-aligned with the module grid).
        /// Parented under parent. Deterministic per seed — same seed always produces
        /// the same building, so re-running a district builder with the same seed base
        /// regenerates identically (matches KennyDistrictSetup's RNG convention).
        /// </summary>
        public static GameObject BuildBuilding(Transform parent, string name, Vector3 basePosition,
            float buildingYRotation, int seed)
        {
            var rng = new System.Random(seed);
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = basePosition;
            root.transform.rotation = Quaternion.identity; // per-piece rotations are absolute (baked below), root stays identity

            Quaternion buildingRot = Quaternion.Euler(0f, buildingYRotation, 0f);
            int floorCount = 1 + rng.Next(3); // 1-3 stories

            for (int floor = 0; floor < floorCount; floor++)
            {
                float floorY = floor * WallHeight;
                string floorVariant = FloorVariants[rng.Next(FloorVariants.Length)];
                SpawnPiece(root.transform, floorVariant, basePosition, buildingRot, new Vector3(0f, floorY, 0f), 0f);

                for (int corner = 0; corner < 4; corner++)
                {
                    bool groundSouth = floor == 0 && corner == 0;
                    string wallVariant = groundSouth
                        ? DoorWallVariants[rng.Next(DoorWallVariants.Length)]
                        : WindowWallVariants[rng.Next(WindowWallVariants.Length)];

                    Vector3 localPos = CornerOffsets[corner] + new Vector3(0f, floorY, 0f);
                    SpawnPiece(root.transform, wallVariant, basePosition, buildingRot, localPos, CornerYRotations[corner]);
                }
            }

            float roofY = floorCount * WallHeight + RoofBottomOffset;
            string roofVariant = RoofVariants[rng.Next(RoofVariants.Length)];
            SpawnPiece(root.transform, roofVariant, basePosition, buildingRot, new Vector3(0f, roofY, ModuleSize), 0f);

            return root;
        }

        private static void SpawnPiece(Transform parent, string prefabName, Vector3 buildingBasePosition,
            Quaternion buildingRotation, Vector3 localOffset, float pieceLocalYRotation)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ArchFolder}/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[Setup] '{ArchFolder}/{prefabName}.prefab' bulunamadı, atlandı.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = prefabName;
            instance.transform.position = buildingBasePosition + buildingRotation * localOffset;
            instance.transform.rotation = buildingRotation * Quaternion.Euler(0f, pieceLocalYRotation, 0f);
            Undo.RegisterCreatedObjectUndo(instance, "Build Tirgames Building");
        }
    }
}
