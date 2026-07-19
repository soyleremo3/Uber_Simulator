using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Oyuncunun bakiyesini yöneten singleton. Tüm para kazanma/harcama işlemleri
    /// bu sınıf üzerinden geçmelidir ki ekonomi tek bir kaynaktan yönetilebilsin.
    /// </summary>
    public class EconomyManager : MonoBehaviour
    {
        public static EconomyManager Instance { get; private set; }

        [Header("Ekonomi Ayarları")]
        [SerializeField] private float startingBalance = 100f;

        public float CurrentBalance { get; private set; }

        /// <summary>
        /// Bakiye her değiştiğinde tetiklenir. UI bu event'i dinleyerek
        /// para göstergesini güncel tutabilir.
        /// </summary>
        public event System.Action<float> OnMoneyChanged;

        /// <summary>
        /// Bir harcama işlemi yetersiz bakiye yüzünden başarısız olduğunda tetiklenir.
        /// </summary>
        public event System.Action<string> OnTransactionFailed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            CurrentBalance = startingBalance;
        }

        private void Start()
        {
            // Sahne yüklendiğinde UI'nin doğru başlangıç değerini göstermesi için tetikliyoruz.
            OnMoneyChanged?.Invoke(CurrentBalance);
        }

        public void AddMoney(float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning("[EconomyManager] AddMoney negatif değerle çağrıldı. SpendMoney kullanılmalı.");
                return;
            }

            CurrentBalance += amount;
            OnMoneyChanged?.Invoke(CurrentBalance);
        }

        /// <returns>İşlem başarılıysa true, yetersiz bakiye varsa false döner.</returns>
        public bool SpendMoney(float amount)
        {
            if (amount < 0f)
            {
                Debug.LogWarning("[EconomyManager] SpendMoney negatif değerle çağrıldı.");
                return false;
            }

            if (amount > CurrentBalance)
            {
                OnTransactionFailed?.Invoke("Yetersiz bakiye.");
                return false;
            }

            CurrentBalance -= amount;
            OnMoneyChanged?.Invoke(CurrentBalance);
            return true;
        }

        /// <summary>Alias for SpendMoney — kept for API-spec compatibility.</summary>
        public bool TrySpendMoney(float amount)
        {
            return SpendMoney(amount);
        }

        public bool HasEnoughMoney(float amount)
        {
            return CurrentBalance >= amount;
        }

        /// <summary>
        /// SaveSystem tarafından yükleme sırasında bakiyeyi geri yazmak için kullanılır.
        /// Doğrudan gameplay kodundan çağrılmamalıdır.
        /// </summary>
        public void SetBalance(float newBalance)
        {
            CurrentBalance = Mathf.Max(0f, newBalance);
            OnMoneyChanged?.Invoke(CurrentBalance);
        }
    }
}