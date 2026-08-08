using System.Collections.Generic;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;

namespace DeliverySim.EditorTools
{
    /// <summary>
    /// One-click scene setup menu items so the human only has to press buttons
    /// instead of assembling objects by hand. Everything here is idempotent:
    /// running an item twice won't duplicate objects.
    /// </summary>
    public static class DeliverySimSetup
    {
        private const string DataFolderRoot = "Assets/_Uber Simulator/_Data";
        private const string OrdersFolder = DataFolderRoot + "/Orders";
        private const string StoreFrontFolder = "Assets/TirgamesAssets/StylizedWorld/Locations/Urban/ExteriorProps/Prefabs";
        private const int StoreFrontVariantCount = 6; // StreetStoreFront01_1 .. _6, already in the owned asset pack.

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/1 - Create Managers")]
        public static void CreateManagers()
        {
            GameObject managers = FindOrCreate("_Managers");
            AddIfMissing<GameManager>(managers);
            AddIfMissing<EconomyManager>(managers);
            AddIfMissing<SaveSystem>(managers);
            AddIfMissing<ReputationManager>(managers);
            AddIfMissing<ShopManager>(managers);
            AddIfMissing<AudioManager>(managers);

            GameObject gameplay = FindOrCreate("_Gameplay");
            AddIfMissing<OrderManager>(gameplay);
            AddIfMissing<RouteManager>(gameplay);

            GameObject ui = FindOrCreate("_UI");
            AddIfMissing<UIBootstrap>(ui);

            Selection.activeGameObject = managers;
            Debug.Log("[Setup] Manager objeleri hazır: _Managers, _Gameplay, _UI.");
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/2 - Setup Player Vehicle Components")]
        public static void SetupPlayerVehicle()
        {
            VehicleController vehicle = Object.FindFirstObjectByType<VehicleController>();
            if (vehicle == null)
            {
                Debug.LogError("[Setup] Sahnede VehicleController bulunamadı. Önce aracı sahneye koy.");
                return;
            }

            GameObject go = vehicle.gameObject;
            AddIfMissing<VehicleFuel>(go);
            AddIfMissing<VehicleCondition>(go);
            AddIfMissing<VehicleInteractor>(go);
            AddIfMissing<VehicleUpgradeApplier>(go);
            AddIfMissing<VehicleReset>(go);

            // NOTE: Rigidbody interpolation is deliberately NOT touched here.
            // The custom raycast-suspension controller was tuned with
            // interpolation = None; enabling Interpolate destabilized handling.

            Selection.activeGameObject = go;
            Debug.Log($"[Setup] '{go.name}' araç bileşenleri tamam (yakıt, hasar, etkileşim, yükseltme, reset).");
        }

        // ------------------------------------------------------------------
        // NOTE: The physics math in VehicleController is interpolation-proof now
        // (reads rb.position/rb.rotation, not transform), so Interpolate can and
        // SHOULD stay ON — the camera depends on it.
        [MenuItem("DeliverySim/Setup/5 - Fix Vehicle Physics (Interpolation AÇIK)")]
        public static void FixVehiclePhysics()
        {
            VehicleController vehicle = Object.FindFirstObjectByType<VehicleController>();
            if (vehicle == null)
            {
                Debug.LogError("[Setup] Sahnede VehicleController bulunamadı.");
                return;
            }

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            if (rb != null && rb.interpolation != RigidbodyInterpolation.Interpolate)
            {
                Undo.RecordObject(rb, "Enable Rigidbody Interpolation");
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                Debug.Log($"[Setup] '{vehicle.name}' Rigidbody interpolation AÇIK yapıldı (kamera akıcılığı; fizik artık interpolation'dan etkilenmiyor).");
            }
            else
            {
                Debug.Log("[Setup] Interpolation zaten açık — değişiklik gerekmedi.");
            }

            AddIfMissing<VehicleReset>(vehicle.gameObject);
            Selection.activeGameObject = vehicle.gameObject;
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/6 - Apply Realistic Vehicle Tuning")]
        public static void ApplyRealisticTuning()
        {
            VehicleController vehicle = Object.FindFirstObjectByType<VehicleController>();
            if (vehicle == null)
            {
                Debug.LogError("[Setup] Sahnede VehicleController bulunamadı.");
                return;
            }

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();

            // Log the OLD values first so the user can restore them by hand if needed.
            string oldWheel = vehicle.wheels != null && vehicle.wheels.Length > 0
                ? $"engineTorque={vehicle.wheels[0].engineTorque}, wheelMass={vehicle.wheels[0].mass}, turnAngle(ön)={vehicle.wheels[0].turnAngle}"
                : "(teker yok)";
            Debug.Log("[Setup] ESKİ DEĞERLER (geri dönmek istersen): " +
                $"mass={(rb != null ? rb.mass : 0f)}, angularDamping={(rb != null ? rb.angularDamping : 0f)}, " +
                $"gripX={vehicle.wheelGripX}, gripZ={vehicle.wheelGripZ}, suspensionForce={vehicle.suspensionForce}, " +
                $"clamp={vehicle.suspensionForceClamp}, damp={vehicle.dampAmount}, downforce={vehicle.downforce}, " +
                $"comY={vehicle.centerOfMassOffset.y}, antiRoll={vehicle.antiRollStiffness}, latHeight={vehicle.lateralForceHeight}, " +
                $"assists=({vehicle.steeringAssist}/{vehicle.throttleAssist}/{vehicle.brakeAssist}), {oldWheel}");

            Undo.RecordObject(vehicle, "Apply Realistic Vehicle Tuning");

            if (rb != null)
            {
                Undo.RecordObject(rb, "Apply Realistic Vehicle Tuning");
                rb.mass = 1200f;                                        // Real sedan mass
                rb.angularDamping = 0.3f;                               // Mild rotational settling
                rb.interpolation = RigidbodyInterpolation.Interpolate;  // Smooth camera; physics is rb-pose based
            }

            // Suspension: 300 kg/corner, ~1.8 Hz natural frequency, damping ratio ~0.45.
            vehicle.suspensionForce = 40000f;
            vehicle.suspensionForceClamp = 15000f;
            vehicle.dampAmount = 4f;

            // Grip slopes from the original proven profile; friction clamp (μN) does the limiting.
            vehicle.wheelGripX = 8f;
            vehicle.wheelGripZ = 42f;

            vehicle.downforce = 0.1f;
            vehicle.centerOfMassOffset = new Vector3(0f, -0.3f, 0f);    // CoM ~0.2 m above ground
            vehicle.inertiaMultiplier = 1.2f;

            // Rollover resistance (the actual anti-flip layer).
            vehicle.antiRollStiffness = 12000f;
            vehicle.lateralForceHeight = 0.6f;

            // Assists on, like the original tuned profile.
            vehicle.steeringAssist = true;
            vehicle.steeringAssistStrength = 0.2f;
            vehicle.throttleAssist = true;
            vehicle.brakeAssist = true;

            if (vehicle.wheels != null)
            {
                foreach (VehicleWheel wheel in vehicle.wheels)
                {
                    wheel.mass = 20f;            // Real wheel+tire
                    wheel.engineTorque = 1200f;  // ~14 kN total thrust target for 1200 kg
                    if (wheel.turnAngle > 0f)
                    {
                        wheel.turnAngle = 30f;   // 34 was overly aggressive at speed
                    }
                }
            }

            AddIfMissing<VehicleReset>(vehicle.gameObject);
            EditorUtility.SetDirty(vehicle);
            Selection.activeGameObject = vehicle.gameObject;
            Debug.Log("[Setup] Gerçekçi araç profili uygulandı (1200 kg). Beğenmezsen: Ctrl+Z veya yukarıda loglanan eski değerleri Inspector'a elle gir. Sahneyi kaydetmeyi unutma (Ctrl+S).");
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/3 - Create Follow Camera (Cinemachine)")]
        public static void CreateFollowCamera()
        {
            // Brain on the main camera is required for Cinemachine to drive it.
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                Debug.LogError("[Setup] Sahnede 'MainCamera' tag'li kamera yok.");
                return;
            }

            if (mainCamera.GetComponent<CinemachineBrain>() == null)
            {
                Undo.AddComponent<CinemachineBrain>(mainCamera.gameObject);
            }

            // Camera rig: filters suspension pitch/roll out of the follow target.
            VehicleCameraRig rig = Object.FindFirstObjectByType<VehicleCameraRig>();
            if (rig == null)
            {
                GameObject rigGo = new GameObject("CameraRig");
                Undo.RegisterCreatedObjectUndo(rigGo, "Create CameraRig");
                rig = rigGo.AddComponent<VehicleCameraRig>();
            }

            if (rig.target == null)
            {
                VehicleController vehicle = Object.FindFirstObjectByType<VehicleController>();
                if (vehicle != null)
                {
                    rig.target = vehicle.transform;
                }
                else
                {
                    Debug.LogWarning("[Setup] Sahnede VehicleController yok — rig.target'ı elle ataman gerekecek.");
                }
            }

            // The Cinemachine camera itself.
            GameObject cmGo = GameObject.Find("CM_FollowCamera");
            if (cmGo == null)
            {
                cmGo = new GameObject("CM_FollowCamera");
                Undo.RegisterCreatedObjectUndo(cmGo, "Create Follow Camera");
            }

            CinemachineCamera cam = cmGo.GetComponent<CinemachineCamera>();
            if (cam == null)
            {
                cam = cmGo.AddComponent<CinemachineCamera>();
            }

            cam.Follow = rig.transform;
            cam.LookAt = rig.transform;

            CinemachineFollow follow = cmGo.GetComponent<CinemachineFollow>();
            if (follow == null)
            {
                follow = cmGo.AddComponent<CinemachineFollow>();
            }

            follow.FollowOffset = new Vector3(0f, 3.5f, -7.5f);

            if (cmGo.GetComponent<CinemachineRotationComposer>() == null)
            {
                cmGo.AddComponent<CinemachineRotationComposer>();
            }

            // Anti wall-clip (camera bug #2).
            if (cmGo.GetComponent<CinemachineDeoccluder>() == null)
            {
                cmGo.AddComponent<CinemachineDeoccluder>();
            }

            Selection.activeGameObject = cmGo;
            Debug.Log("[Setup] Cinemachine takip kamerası hazır (CM_FollowCamera + CameraRig + Deoccluder).");
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/4 - Create Sample Orders + Points")]
        public static void CreateSampleContent()
        {
            EnsureFolder(DataFolderRoot);
            EnsureFolder(OrdersFolder);

            // Scene points (id, type, position).
            CreatePoint<PickupPoint>("Pickup_Restaurant", "pickup_restaurant", new Vector3(25f, 0f, 15f));
            CreatePoint<PickupPoint>("Pickup_Depot", "pickup_depot", new Vector3(-30f, 0f, 20f));
            CreatePoint<DeliveryPoint>("Delivery_HouseA", "delivery_house_a", new Vector3(70f, 0f, 50f));
            CreatePoint<DeliveryPoint>("Delivery_HouseB", "delivery_house_b", new Vector3(-60f, 0f, -40f));
            CreatePoint<DeliveryPoint>("Delivery_Office", "delivery_office", new Vector3(10f, 0f, 90f));

            CreateStation<FuelStation>("FuelStation_Main", new Vector3(12f, 0f, -25f));
            CreateStation<RepairStation>("RepairStation_Main", new Vector3(-14f, 0f, -25f));

            // Order assets. Every pickup/delivery combo gets multiple cargo/payment
            // variants so the offer pool (9) stays well bigger than maxOffers (3)
            // — otherwise the same orders show up every single time. OrderManager
            // also periodically rotates untouched offers (RotateOffers), so a
            // bigger pool actually gets seen instead of just sitting on 3 forever.
            var orders = new List<OrderData>
            {
                CreateOrderAsset("order_food_a", "Sıcak Yemek Siparişi", "pickup_restaurant", "delivery_house_a", 35f, 150f, CargoType.Food),
                CreateOrderAsset("order_package_a", "Kargo Paketi", "pickup_depot", "delivery_house_b", 50f, 220f, CargoType.Package),
                CreateOrderAsset("order_fragile_a", "Kırılabilir Eşya", "pickup_depot", "delivery_office", 70f, 200f, CargoType.Fragile),
                CreateOrderAsset("order_food_b", "Ofise Yemeği", "pickup_restaurant", "delivery_office", 40f, 170f, CargoType.Food),
                CreateOrderAsset("order_package_b", "Uzak Mahalle Kargosu", "pickup_restaurant", "delivery_house_b", 55f, 240f, CargoType.Package),
                CreateOrderAsset("order_fragile_b", "Kırılabilir Eşya (Depo)", "pickup_depot", "delivery_house_a", 65f, 210f, CargoType.Fragile),
                CreateOrderAsset("order_food_c", "Depo Catering Siparişi", "pickup_depot", "delivery_house_b", 45f, 230f, CargoType.Food),
                CreateOrderAsset("order_package_c", "Ekspres Kargo", "pickup_restaurant", "delivery_house_a", 60f, 130f, CargoType.Package),
                CreateOrderAsset("order_fragile_c", "Acil Kırılabilir Eşya", "pickup_depot", "delivery_office", 85f, 160f, CargoType.Fragile)
            };

            // Push the pool into the scene OrderManager.
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
                Debug.Log("[Setup] OrderManager.orderPool 9 örnek siparişle dolduruldu.");
            }
            else
            {
                Debug.LogWarning("[Setup] Sahnede OrderManager yok — önce '1 - Create Managers' çalıştır, sonra bunu tekrarla.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Örnek içerik hazır: 2 alım, 3 teslim noktası, yakıt+tamir istasyonu, 9 sipariş asset'i.");
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/7 - Pro Controls + Camera Feel")]
        public static void ApplyProControlsAndCamera()
        {
            // --- Interact key: scene serialized it as F; switch to E (industry standard). ---
            VehicleInteractor interactor = Object.FindFirstObjectByType<VehicleInteractor>();
            if (interactor != null)
            {
                var interactorSo = new SerializedObject(interactor);
                SerializedProperty keyProp = interactorSo.FindProperty("interactKey");
                if (keyProp != null && keyProp.intValue != (int)KeyCode.E)
                {
                    keyProp.intValue = (int)KeyCode.E;
                    interactorSo.ApplyModifiedProperties();
                    Debug.Log("[Setup] Etkileşim tuşu F -> E yapıldı.");
                }
            }

            // --- Find the two Cinemachine cameras (prefer CameraModeController refs). ---
            CameraModeController modeController = Object.FindFirstObjectByType<CameraModeController>();
            CinemachineCamera thirdCam = modeController != null ? modeController.thirdPersonCamera : null;
            CinemachineCamera firstCam = modeController != null ? modeController.firstPersonCamera : null;

            if (thirdCam == null)
            {
                GameObject go = GameObject.Find("VehicleFollowCamera");
                if (go != null) thirdCam = go.GetComponent<CinemachineCamera>();
            }

            if (firstCam == null)
            {
                GameObject go = GameObject.Find("FirstPersonCamera");
                if (go != null) firstCam = go.GetComponent<CinemachineCamera>();
            }

            // --- Third person: orbit frame rotates with the yaw-smoothed rig, so the
            //     camera settles behind the car on turns (pro driving-cam behavior). ---
            if (thirdCam != null)
            {
                CinemachineOrbitalFollow orbital = thirdCam.GetComponent<CinemachineOrbitalFollow>();
                if (orbital != null)
                {
                    Undo.RecordObject(orbital, "Pro Camera Feel");
                    var tracker = orbital.TrackerSettings;
                    Debug.Log($"[Setup] ESKİ orbital ayarları: binding={tracker.BindingMode}, damping={tracker.PositionDamping}");
                    tracker.BindingMode = Unity.Cinemachine.TargetTracking.BindingMode.LockToTargetWithWorldUp;
                    tracker.PositionDamping = new Vector3(0.3f, 0.8f, 0.3f);
                    orbital.TrackerSettings = tracker;
                    EditorUtility.SetDirty(orbital);
                }

                CinemachineRotationComposer composer = thirdCam.GetComponent<CinemachineRotationComposer>();
                if (composer != null)
                {
                    Undo.RecordObject(composer, "Pro Camera Feel");
                    composer.Damping = new Vector2(0.4f, 0.4f);
                    EditorUtility.SetDirty(composer);
                }
            }
            else
            {
                Debug.LogWarning("[Setup] 3. şahıs kamera (VehicleFollowCamera) bulunamadı — orbital ayarları atlandı.");
            }

            // --- First person: tiny damping filters engine/suspension micro-shake. ---
            if (firstCam != null)
            {
                CinemachineHardLockToTarget hardLock = firstCam.GetComponent<CinemachineHardLockToTarget>();
                if (hardLock != null)
                {
                    Undo.RecordObject(hardLock, "Pro Camera Feel");
                    hardLock.Damping = 0.08f;
                    EditorUtility.SetDirty(hardLock);
                }

                CinemachineRotateWithFollowTarget rotateWith = firstCam.GetComponent<CinemachineRotateWithFollowTarget>();
                if (rotateWith != null)
                {
                    Undo.RecordObject(rotateWith, "Pro Camera Feel");
                    rotateWith.Damping = 0.15f;
                    EditorUtility.SetDirty(rotateWith);
                }
            }

            // --- Rig: slightly snappier yaw follow. ---
            VehicleCameraRig rig = Object.FindFirstObjectByType<VehicleCameraRig>();
            if (rig != null)
            {
                Undo.RecordObject(rig, "Pro Camera Feel");
                rig.yawSmoothSpeed = 6f;
                EditorUtility.SetDirty(rig);
            }

            // --- Brain: crisp professional mode-switch blend (default 2 s is sluggish). ---
            Camera mainCamera = Camera.main;
            CinemachineBrain brain = mainCamera != null ? mainCamera.GetComponent<CinemachineBrain>() : null;
            if (brain != null)
            {
                Undo.RecordObject(brain, "Pro Camera Feel");
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, 0.8f);
                EditorUtility.SetDirty(brain);
            }

            Debug.Log("[Setup] Pro kontroller + kamera hissi uygulandı. Tuşlar: E etkileşim, Tab siparişler, C kamera, LShift/LCtrl vites, R düzelt.");
        }

        // ------------------------------------------------------------------
        [MenuItem("DeliverySim/Setup/8 - Upgrade Point Visuals (Kalıcı İşaret + Katı Kiosk)")]
        public static void UpgradePointVisuals()
        {
            const string materialsFolder = "Assets/_Uber Simulator/Art/Materials";
            EnsureFolder(materialsFolder);

            Material pickupMat = EnsureMaterial($"{materialsFolder}/PickupGreen.mat", new Color(0.15f, 0.85f, 0.3f), true);
            Material deliveryMat = EnsureMaterial($"{materialsFolder}/DeliveryOrange.mat", new Color(1f, 0.5f, 0.1f), true);
            Material stationMat = EnsureMaterial($"{materialsFolder}/StationGray.mat", new Color(0.5f, 0.55f, 0.62f), false);

            InteractionPoint[] points = Object.FindObjectsByType<InteractionPoint>(FindObjectsSortMode.None);
            foreach (InteractionPoint point in points)
            {
                UpgradeSinglePoint(point, point is PickupPoint ? pickupMat : deliveryMat);
            }

            foreach (FuelStation station in Object.FindObjectsByType<FuelStation>(FindObjectsSortMode.None))
            {
                EnsureStationSolid(station.gameObject, stationMat, station.DisplayName);
            }

            foreach (RepairStation station in Object.FindObjectsByType<RepairStation>(FindObjectsSortMode.None))
            {
                EnsureStationSolid(station.gameObject, stationMat, station.DisplayName);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[Setup] {points.Length} nokta güncellendi: kalıcı renkli işaret (hep görünür) + aktif hedef halkası + katı kiosk. İstasyonlar katılaştırıldı. Sahneyi kaydet (Ctrl+S).");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static Material EnsureMaterial(string path, Color color, bool emissive)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard"); // Fallback for non-URP setups
                }

                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }

            mat.color = color;
            if (emissive)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", color * 1.2f);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void UpgradeSinglePoint(InteractionPoint point, Material mat)
        {
            Transform root = point.transform;
            var so = new SerializedObject(point);
            SerializedProperty markerProp = so.FindProperty("markerVisual");
            SerializedProperty beaconProp = so.FindProperty("permanentBeacon");

            // The old tall "Marker" cylinder (bare pole look) is no longer wanted —
            // remove it outright. The named kiosk below replaces it as the
            // always-visible identity marker.
            Transform oldMarker = root.Find("Marker");
            if (oldMarker != null)
            {
                Object.DestroyImmediate(oldMarker.gameObject);
            }

            // Active-target highlight: flat bright ring on the ground, toggled by OrderManager.
            GameObject ring = FindChild(root, "HighlightRing");
            if (ring == null)
            {
                ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                ring.name = "HighlightRing";
                Object.DestroyImmediate(ring.GetComponent<Collider>());
                ring.transform.SetParent(root, false);
                ring.transform.localPosition = new Vector3(0f, 0.05f, 0f);
                ring.transform.localScale = new Vector3(9f, 0.05f, 9f);
                Undo.RegisterCreatedObjectUndo(ring, "Create HighlightRing");
            }

            ApplyMaterial(ring, mat);
            ring.SetActive(false);

            // Solid kiosk/stand: the car physically cannot drive through the location
            // center (REAL visible obstacle — unrelated to the old invisible-trigger
            // catapult bug, which was about raycasts hitting the trigger shell). Now
            // sized like a small stand/kiosk instead of a thin block, and doubles as
            // the always-visible identity marker (no bare "pole" anymore).
            GameObject kiosk = FindChild(root, "Kiosk");
            if (kiosk == null)
            {
                kiosk = GameObject.CreatePrimitive(PrimitiveType.Cube); // Keeps its SOLID BoxCollider on purpose
                kiosk.name = "Kiosk";
                kiosk.transform.SetParent(root, false);
                Undo.RegisterCreatedObjectUndo(kiosk, "Create Kiosk");
            }

            kiosk.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            kiosk.transform.localScale = new Vector3(1.6f, 1.8f, 1.6f);
            kiosk.SetActive(true); // Always visible — never hidden again.
            ApplyMaterial(kiosk, mat);

            // Named sign board + 3D text on top of the kiosk — replaces the bare pole
            // with something that actually reads as "this location's stand".
            AddNamePlate(root, point.DisplayName, mat,
                new Vector3(0f, 2.1f, 0.75f), new Vector3(1.8f, 0.5f, 0.15f),
                new Vector3(0f, 2.1f, 0.83f));

            AddStoreFrontProp(root, point.PointId);

            markerProp.objectReferenceValue = ring;
            beaconProp.objectReferenceValue = kiosk; // Identity object is now the named kiosk, not a pole.
            so.ApplyModifiedProperties();
        }

        private static void EnsureStationSolid(GameObject stationRoot, Material mat, string displayName)
        {
            GameObject visual = FindChild(stationRoot.transform, "Visual");
            if (visual == null)
            {
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(stationRoot.transform, false);
                visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                visual.transform.localScale = new Vector3(2f, 3f, 2f);
                Undo.RegisterCreatedObjectUndo(visual, "Create Station Visual");
            }

            if (visual.GetComponent<Collider>() == null)
            {
                visual.AddComponent<BoxCollider>(); // Solid: can't drive through the pump/lift
            }

            ApplyMaterial(visual, mat);

            // Same named sign board treatment as pickup/delivery kiosks, positioned
            // above the taller station "Visual" block (3 units tall vs kiosk's 1.8).
            AddNamePlate(stationRoot.transform, displayName, mat,
                new Vector3(0f, 3.3f, 1.15f), new Vector3(1.8f, 0.5f, 0.15f),
                new Vector3(0f, 3.3f, 1.23f));

            AddStoreFrontProp(stationRoot.transform, displayName);
        }

        /// <summary>
        /// Instantiates one of the owned "StreetStoreFront01_1..6" props (real modeled
        /// shopfront — awning + detail, not a primitive) beside the point as pure set
        /// dressing, so pickup/delivery/fuel/repair each reads as belonging to an actual
        /// storefront instead of an anonymous colored box. Offset to the side (not
        /// overlapping the kiosk/visual/sign) since we don't know its exact footprint.
        /// Deterministic per point (hash of the id) so re-running doesn't reshuffle it,
        /// and idempotent — skips if already placed.
        /// </summary>
        private static void AddStoreFrontProp(Transform root, string seed)
        {
            if (FindChild(root, "StoreFrontProp") != null)
            {
                return;
            }

            int variant = 1 + (Mathf.Abs(seed.GetHashCode()) % StoreFrontVariantCount);
            string prefabPath = $"{StoreFrontFolder}/StreetStoreFront01_{variant}.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning($"[Setup] '{prefabPath}' bulunamadı — StoreFront prop atlandı.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            instance.name = "StoreFrontProp";
            instance.transform.localPosition = new Vector3(2.4f, 0f, -0.6f);
            instance.transform.localRotation = Quaternion.identity;
            Undo.RegisterCreatedObjectUndo(instance, "Add StoreFront Prop");
        }

        /// <summary>
        /// Creates/updates a flat "SignBoard" plate plus a "SignText" TextMeshPro
        /// label above it, showing displayName. Idempotent: re-running just updates
        /// text/position/material on the existing objects instead of duplicating.
        /// Shared by pickup/delivery kiosks and fuel/repair stations.
        /// </summary>
        private static void AddNamePlate(Transform root, string displayName, Material mat,
            Vector3 boardLocalPosition, Vector3 boardLocalScale, Vector3 textLocalPosition)
        {
            GameObject signBoard = FindChild(root, "SignBoard");
            if (signBoard == null)
            {
                signBoard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                signBoard.name = "SignBoard";
                Object.DestroyImmediate(signBoard.GetComponent<Collider>());
                signBoard.transform.SetParent(root, false);
                Undo.RegisterCreatedObjectUndo(signBoard, "Create SignBoard");
            }

            signBoard.transform.localPosition = boardLocalPosition;
            signBoard.transform.localScale = boardLocalScale;
            ApplyMaterial(signBoard, mat);

            GameObject signTextGo = FindChild(root, "SignText");
            TextMeshPro label;
            if (signTextGo == null)
            {
                signTextGo = new GameObject("SignText");
                signTextGo.transform.SetParent(root, false);
                Undo.RegisterCreatedObjectUndo(signTextGo, "Create SignText");

                label = signTextGo.AddComponent<TextMeshPro>();
                label.fontSize = 8;
                label.alignment = TextAlignmentOptions.Center;
                label.color = Color.white;

                RectTransform rectTransform = signTextGo.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.sizeDelta = new Vector2(20f, 5f);
                }
            }
            else
            {
                label = signTextGo.GetComponent<TextMeshPro>();
                if (label == null)
                {
                    label = signTextGo.AddComponent<TextMeshPro>();
                }
            }

            signTextGo.transform.localPosition = textLocalPosition;
            signTextGo.transform.localScale = Vector3.one * 0.15f;
            label.text = displayName; // Name may have changed — always refresh.
        }

        private static GameObject FindChild(Transform root, string childName)
        {
            Transform child = root.Find(childName);
            return child != null ? child.gameObject : null;
        }

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            MeshRenderer meshRenderer = go.GetComponent<MeshRenderer>();
            if (meshRenderer != null && mat != null)
            {
                meshRenderer.sharedMaterial = mat;
            }
        }

        private static GameObject FindOrCreate(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go == null)
            {
                go = new GameObject(name);
                Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            }

            return go;
        }

        private static T AddIfMissing<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component == null)
            {
                component = Undo.AddComponent<T>(go);
            }

            return component;
        }

        private static void CreatePoint<T>(string objectName, string pointId, Vector3 position)
            where T : InteractionPoint
        {
            if (GameObject.Find(objectName) != null)
            {
                return; // Idempotent: don't duplicate.
            }

            GameObject go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {objectName}");
            go.transform.position = position;

            SphereCollider trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 5f;

            // Visible beacon toggled by OrderManager while this point is the target.
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "Marker";
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.transform.SetParent(go.transform, false);
            marker.transform.localPosition = new Vector3(0f, 6f, 0f);
            marker.transform.localScale = new Vector3(1.5f, 6f, 1.5f);
            marker.SetActive(false);

            T point = go.AddComponent<T>();
            var so = new SerializedObject(point);
            so.FindProperty("pointId").stringValue = pointId;
            so.FindProperty("markerVisual").objectReferenceValue = marker;
            so.ApplyModifiedProperties();
        }

        private static void CreateStation<T>(string objectName, Vector3 position) where T : Component
        {
            if (GameObject.Find(objectName) != null)
            {
                return;
            }

            GameObject go = new GameObject(objectName);
            Undo.RegisterCreatedObjectUndo(go, $"Create {objectName}");
            go.transform.position = position;

            SphereCollider trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 5f;

            // Simple visible block so the station can be found in playtest.
            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(go.transform, false);
            visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
            visual.transform.localScale = new Vector3(2f, 3f, 2f);

            go.AddComponent<T>();
        }

        private static OrderData CreateOrderAsset(string orderId, string orderName,
            string pickupId, string deliveryId, float payment, float timeLimit, CargoType cargo)
        {
            string path = $"{OrdersFolder}/{orderId}.asset";
            OrderData existing = AssetDatabase.LoadAssetAtPath<OrderData>(path);
            if (existing != null)
            {
                return existing; // Idempotent.
            }

            OrderData order = ScriptableObject.CreateInstance<OrderData>();
            var so = new SerializedObject(order);
            so.FindProperty("orderId").stringValue = orderId;
            so.FindProperty("orderName").stringValue = orderName;
            so.FindProperty("pickupPointId").stringValue = pickupId;
            so.FindProperty("deliveryPointId").stringValue = deliveryId;
            so.FindProperty("paymentAmount").floatValue = payment;
            so.FindProperty("timeLimitSeconds").floatValue = timeLimit;
            so.FindProperty("cargoType").enumValueIndex = (int)cargo;
            so.ApplyModifiedProperties();

            AssetDatabase.CreateAsset(order, path);
            return order;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            int lastSlash = path.LastIndexOf('/');
            string parent = path.Substring(0, lastSlash);
            string folderName = path.Substring(lastSlash + 1);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
