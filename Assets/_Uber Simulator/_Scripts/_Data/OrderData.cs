using UnityEngine;

namespace DeliverySim
{
    public enum CargoType
    {
        Yemek,
        Paket,
        Kirilabilir
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
        [SerializeField] private CargoType cargoType = CargoType.Paket;
        [SerializeField][TextArea] private string description;

        public string OrderId => orderId;
        public string OrderName => orderName;
        public string PickupPointId => pickupPointId;
        public string DeliveryPointId => deliveryPointId;
        public float PaymentAmount => paymentAmount;
        public float TimeLimitSeconds => timeLimitSeconds;
        public CargoType CargoType => cargoType;
        public string Description => description;

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