using System;
using System.IO;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Kalıcı verinin JSON karşılığı. İleride yeni alanlar (itibar detayları,
    /// sahip olunan araçlar vb.) eklendikçe bu sınıfa genişletilir.
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public float balance;
        public float reputationScore;
        public string lastSaveTimestamp;

        public SaveData()
        {
            balance = 0f;
            reputationScore = 0f;
            lastSaveTimestamp = string.Empty;
        }
    }

    /// <summary>
    /// JSON tabanlı basit kayıt sistemi. Şu an için sadece bakiyeyi kaydeder;
    /// itibar sistemi kurulduğunda reputationScore gerçek kaynaktan okunacak.
    /// </summary>
    public class SaveSystem : MonoBehaviour
    {
        public static SaveSystem Instance { get; private set; }

        private const string SaveFileName = "deliverysim_save.json";
        private string SaveFilePath => Path.Combine(Application.persistentDataPath, SaveFileName);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public void SaveGame()
        {
            SaveData data = new SaveData
            {
                balance = EconomyManager.Instance != null ? EconomyManager.Instance.CurrentBalance : 0f,
                reputationScore = 0f, // TODO: ReputationManager eklendiğinde buradan okunacak.
                lastSaveTimestamp = DateTime.UtcNow.ToString("o")
            };

            try
            {
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SaveFilePath, json);
                Debug.Log($"[SaveSystem] Oyun kaydedildi: {SaveFilePath}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Kayıt sırasında hata oluştu: {e.Message}");
            }
        }

        public SaveData LoadGame()
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.Log("[SaveSystem] Kayıt dosyası bulunamadı, varsayılan veri döndürülüyor.");
                return new SaveData();
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                SaveData data = JsonUtility.FromJson<SaveData>(json);

                if (data == null)
                {
                    Debug.LogWarning("[SaveSystem] Kayıt dosyası bozuk görünüyor, varsayılan veri döndürülüyor.");
                    return new SaveData();
                }

                if (EconomyManager.Instance != null)
                {
                    EconomyManager.Instance.SetBalance(data.balance);
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Yükleme sırasında hata oluştu: {e.Message}");
                return new SaveData();
            }
        }

        public bool HasSaveFile()
        {
            return File.Exists(SaveFilePath);
        }

        public void DeleteSave()
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                Debug.Log("[SaveSystem] Kayıt dosyası silindi.");
            }
        }
    }
}