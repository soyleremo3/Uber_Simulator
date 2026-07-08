using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// GEÇİCİ TEST SCRIPT'İ — sadece EconomyManager ve SaveSystem'i doğrulamak için.
    /// Gerçek oyun mantığına dahil değildir, sistemler çalıştığı doğrulandıktan sonra
    /// bu component sahneden kaldırılabilir.
    ///
    /// Kullanım (Play modundayken klavyeden):
    /// 1 tuşu -> 50 para ekler
    /// 2 tuşu -> 30 para harcamayı dener
    /// 3 tuşu -> oyunu kaydeder (SaveGame)
    /// 4 tuşu -> kayıtlı oyunu yükler (LoadGame)
    /// </summary>
    public class EconomyTestRunner : MonoBehaviour
    {
        private void Start()
        {
            Debug.Log("=== EconomyTestRunner BAŞLADI ===");

            if (EconomyManager.Instance == null)
            {
                Debug.LogError("[TEST] EconomyManager.Instance NULL! _Managers objesi sahnede yok veya EconomyManager component'i eksik.");
                return;
            }

            Debug.Log($"[TEST] Başlangıç bakiyesi: {EconomyManager.Instance.CurrentBalance}");
        }

        private void Update()
        {
            if (EconomyManager.Instance == null || SaveSystem.Instance == null)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                EconomyManager.Instance.AddMoney(50f);
                Debug.Log($"[TEST] 1 tuşu: 50 eklendi. Yeni bakiye: {EconomyManager.Instance.CurrentBalance}");
            }

            if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                bool success = EconomyManager.Instance.SpendMoney(30f);
                Debug.Log($"[TEST] 2 tuşu: 30 harcama denendi. Başarılı mı: {success}. Yeni bakiye: {EconomyManager.Instance.CurrentBalance}");
            }

            if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SaveSystem.Instance.SaveGame();
                Debug.Log("[TEST] 3 tuşu: SaveGame() çağrıldı.");
            }

            if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SaveSystem.Instance.LoadGame();
                Debug.Log($"[TEST] 4 tuşu: LoadGame() çağrıldı. Yükleme sonrası bakiye: {EconomyManager.Instance.CurrentBalance}");
            }
        }
    }
}