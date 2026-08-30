using UnityEngine;

namespace DeliverySim
{
    public enum CargoType
    {
        Food,
        Package,
        Fragile
    }

    /// <summary>Player-facing Turkish labels for CargoType — the UI is Turkish, the raw enum names are not.</summary>
    public static class CargoTypeExtensions
    {
        public static string Label(this CargoType type)
        {
            switch (type)
            {
                case CargoType.Food: return "Yemek";
                case CargoType.Package: return "Paket";
                case CargoType.Fragile: return "Kırılır";
                default: return type.ToString();
            }
        }
    }

    /// <summary>
    /// Bir teslimat siparişinin tüm verisini tutan ScriptableObject.
    /// Inventory sistemindeki ItemData ile aynı mimari mantığı izler:
    /// veri asset olarak tutulur, sahne referansları ID string'leri üzerinden çözülür.
    /// </summary>
    [CreateAssetMenu(fileName = "NewOrder", menuName = "DeliverySim/Order Data")]
    public class OrderData : ScriptableObject
    {
        [Header("Kimlik")]
        [SerializeField] private string orderId = "order_000";
        [SerializeField] private string orderName = "Yeni Sipariş";

        [Header("Konum Referansları")]
        [Tooltip("Sahnedeki DeliveryPoint bileşenlerinin PointId alanıyla eşleşmelidir.")]
        [SerializeField] private string pickupPointId;
        [SerializeField] private string deliveryPointId;

        [Header("Ekonomi")]
        [SerializeField] private float paymentAmount = 25f;

        [Header("Zaman")]
        [Tooltip("Saniye cinsinden teslimat süresi.")]
        [SerializeField] private float timeLimitSeconds = 180f;

        [Header("Yük Bilgisi")]
        [SerializeField] private CargoType cargoType = CargoType.Package;
        [SerializeField][TextArea] private string description;

        [Header("İtibar Kilidi")]
        [Tooltip("Bu sipariş, oyuncunun itibarı en az bu kademedeyse havuza girer.")]
        [SerializeField] private ReputationTier minReputationTier = ReputationTier.Bronze;

        public string OrderId => orderId;
        public string OrderName => orderName;
        public string PickupPointId => pickupPointId;
        public string DeliveryPointId => deliveryPointId;
        public float PaymentAmount => paymentAmount;
        public float TimeLimitSeconds => timeLimitSeconds;
        public CargoType CargoType => cargoType;
        public string Description => description;
        public ReputationTier MinReputationTier => minReputationTier;

        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                Debug.LogWarning($"[OrderData] '{name}' için orderId boş bırakılamaz.");
            }

            if (string.IsNullOrWhiteSpace(pickupPointId) || string.IsNullOrWhiteSpace(deliveryPointId))
            {
                Debug.LogWarning($"[OrderData] '{name}' için alım/teslim noktası ID'si eksik.");
            }

            if (paymentAmount < 0f)
            {
                paymentAmount = 0f;
            }

            if (timeLimitSeconds <= 0f)
            {
                timeLimitSeconds = 1f;
            }
        }
    }
}