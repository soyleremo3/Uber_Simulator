using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Bir araç "profilini" tanımlayan ScriptableObject. Mağazadan satın alınabilecek
    /// her araç türü için bir asset oluşturulur (ör. Bisiklet, Scooter, Araba, Kamyon).
    /// PRO RACER'daki araç profili ScriptableObject mantığının bu projeye uyarlanmış hali.
    ///
    /// Start()'ta VehicleController.vehicleData alanına atanırsa, buradaki değerler
    /// VehicleController üzerindeki motor/teker/süspansiyon alanlarının üzerine yazılır.
    /// </summary>
    [CreateAssetMenu(fileName = "NewVehicle", menuName = "DeliverySim/Vehicle Data")]
    public class VehicleData : ScriptableObject
    {
        [Header("Kimlik")]
        [SerializeField] private string vehicleId = "vehicle_000";
        [SerializeField] private string vehicleName = "Yeni Araç";

        [Header("Ekonomi (Mağaza)")]
        [Tooltip("Mağazadan satın alma fiyatı.")]
        [SerializeField] private float price = 500f;
        [Tooltip("Litre cinsinden yakıt deposu kapasitesi.")]
        [SerializeField] private float fuelCapacity = 40f;

        [Header("Görüntüleme Amaçlı Değerler (Mağaza UI'sinde gösterilir)")]
        [Tooltip("Sadece bilgilendirme amaçlı; gerçek fizik motor/teker ayarlarından hesaplanmaz.")]
        [SerializeField] private float displayTopSpeedKph = 120f;
        [Range(1, 10)]
        [SerializeField] private int accelerationRating = 5;

        [Header("Motor Ayarları")]
        [SerializeField] private float idleRPM = 2400f;
        [SerializeField] private float maxRPM = 7000f;
        [SerializeField] private float[] gearRatios = { 3.50f, 2.80f, 2.30f, 1.90f, 1.60f, 1.30f, 1.00f, 0.85f };
        [SerializeField] private float finalDriveRatio = 4.0f;

        [Header("Teker Ayarları (Tüm Tekerlere Uygulanır)")]
        [SerializeField] private float engineTorquePerWheel = 40f;
        [SerializeField] private float brakeStrengthPerWheel = 0.5f;
        [SerializeField] private float wheelSize = 0.5f;

        [Header("Süspansiyon / Tutunma")]
        [SerializeField] private float wheelGripX = 8f;
        [SerializeField] private float wheelGripZ = 42f;
        [SerializeField] private float suspensionForce = 90f;
        [SerializeField] private float dampAmount = 2.5f;
        [SerializeField] private float suspensionForceClamp = 200f;
        [SerializeField] private float downforce = 0.16f;

        public string VehicleId => vehicleId;
        public string VehicleName => vehicleName;
        public float Price => price;
        public float FuelCapacity => fuelCapacity;
        public float DisplayTopSpeedKph => displayTopSpeedKph;
        public int AccelerationRating => accelerationRating;

        /// <summary>
        /// Bu profildeki tüm ayarları verilen VehicleController'a uygular.
        /// VehicleController.Start() içinde otomatik çağrılır; elle de çağrılabilir
        /// (ör. mağazada araç değiştirildiğinde).
        /// </summary>
        public void ApplyTo(VehicleController controller)
        {
            if (controller == null)
            {
                Debug.LogError("[VehicleData] ApplyTo çağrıldı ama controller null.");
                return;
            }

            if (controller.engine == null)
            {
                controller.engine = new VehicleEngine();
            }

            controller.engine.idleRPM = idleRPM;
            controller.engine.maxRPM = maxRPM;
            controller.engine.gearRatios = gearRatios;
            controller.engine.finalDriveRatio = finalDriveRatio;
            controller.engine.ResetState();

            controller.wheelGripX = wheelGripX;
            controller.wheelGripZ = wheelGripZ;
            controller.suspensionForce = suspensionForce;
            controller.dampAmount = dampAmount;
            controller.suspensionForceClamp = suspensionForceClamp;
            controller.downforce = downforce;

            if (controller.wheels != null)
            {
                foreach (var wheel in controller.wheels)
                {
                    wheel.engineTorque = engineTorquePerWheel;
                    wheel.brakeStrength = brakeStrengthPerWheel;
                    wheel.size = wheelSize;
                }
            }
        }

        private void OnValidate()
        {
            if (price < 0f) price = 0f;
            if (fuelCapacity < 0f) fuelCapacity = 0f;
            if (gearRatios == null || gearRatios.Length == 0)
            {
                Debug.LogWarning($"[VehicleData] '{name}' için en az bir vites oranı gerekli.");
            }
        }
    }
}