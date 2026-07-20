using System.Collections.Generic;
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
        [MenuItem("DeliverySim/Setup/5 - Fix Vehicle Physics (Interpolation Geri Al)")]
        public static void FixVehiclePhysics()
        {
            VehicleController vehicle = Object.FindFirstObjectByType<VehicleController>();
            if (vehicle == null)
            {
                Debug.LogError("[Setup] Sahnede VehicleController bulunamadı.");
                return;
            }

            Rigidbody rb = vehicle.GetComponent<Rigidbody>();
            if (rb != null && rb.interpolation != RigidbodyInterpolation.None)
            {
                Undo.RecordObject(rb, "Restore Rigidbody Interpolation");
                rb.interpolation = RigidbodyInterpolation.None;
                Debug.Log($"[Setup] '{vehicle.name}' Rigidbody interpolation None yapıldı (eski, test edilmiş ayar).");
            }
            else
            {
                Debug.Log("[Setup] Interpolation zaten None — değişiklik gerekmedi.");
            }

            AddIfMissing<VehicleReset>(vehicle.gameObject);
            Selection.activeGameObject = vehicle.gameObject;
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

            // Order assets.
            var orders = new List<OrderData>
            {
                CreateOrderAsset("order_food_a", "Sıcak Yemek Siparişi", "pickup_restaurant", "delivery_house_a", 35f, 150f, CargoType.Food),
                CreateOrderAsset("order_package_a", "Kargo Paketi", "pickup_depot", "delivery_house_b", 50f, 220f, CargoType.Package),
                CreateOrderAsset("order_fragile_a", "Kırılabilir Eşya", "pickup_depot", "delivery_office", 70f, 200f, CargoType.Fragile)
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
                Debug.Log("[Setup] OrderManager.orderPool 3 örnek siparişle dolduruldu.");
            }
            else
            {
                Debug.LogWarning("[Setup] Sahnede OrderManager yok — önce '1 - Create Managers' çalıştır, sonra bunu tekrarla.");
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Setup] Örnek içerik hazır: 2 alım, 3 teslim noktası, yakıt+tamir istasyonu, 3 sipariş asset'i.");
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

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
