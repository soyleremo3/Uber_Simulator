using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Kalıcı verinin JSON karşılığı. İleride yeni alanlar eklendikçe bu sınıfa
    /// genişletilir ve CurrentSaveVersion artırılarak Migrate() içinde ele alınır.
    /// </summary>
    /// <summary>One upgrade category's saved level (JsonUtility-friendly).</summary>
    [Serializable]
    public class UpgradeEntry
    {
        public string category;
        public int level;
    }

    [Serializable]
    public class SaveData
    {
        /// <summary>Kayıt formatı sürümü — alan eklenince artır, eski kayıtlar Migrate() ile taşınır.</summary>
        public int saveVersion;
        public float balance;
        public float reputationScore;
        public List<float> recentDeliveryScores;
        public List<string> ownedVehicleIds;
        public List<UpgradeEntry> upgrades;
        public string lastSaveTimestamp;

        public SaveData()
        {
            saveVersion = SaveSystem.CurrentSaveVersion;
            balance = 0f;
            reputationScore = 0f;
            recentDeliveryScores = new List<float>();
            ownedVehicleIds = new List<string>();
            upgrades = new List<UpgradeEntry>();
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

        /// <summary>Güncel kayıt formatı sürümü. SaveData'ya alan eklenince artır.</summary>
        public const int CurrentSaveVersion = 2;

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
                saveVersion = CurrentSaveVersion,
                balance = EconomyManager.Instance != null ? EconomyManager.Instance.CurrentBalance : 0f,
                lastSaveTimestamp = DateTime.UtcNow.ToString("o")
            };

            if (ReputationManager.Instance != null)
            {
                data.reputationScore = ReputationManager.Instance.AverageScore;
                data.recentDeliveryScores = ReputationManager.Instance.GetScoresSnapshot();
            }

            if (ShopManager.Instance != null)
            {
                data.ownedVehicleIds = ShopManager.Instance.GetOwnedVehiclesSnapshot();
                data.upgrades = ShopManager.Instance.GetUpgradeSnapshot();
            }

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

                Migrate(data);

                if (EconomyManager.Instance != null)
                {
                    EconomyManager.Instance.SetBalance(data.balance);
                }

                if (ReputationManager.Instance != null)
                {
                    ReputationManager.Instance.RestoreScores(data.recentDeliveryScores);
                }

                if (ShopManager.Instance != null)
                {
                    ShopManager.Instance.RestoreState(data.upgrades, data.ownedVehicleIds);
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveSystem] Yükleme sırasında hata oluştu: {e.Message}");
                return new SaveData();
            }
        }

        /// <summary>
        /// Eski sürüm kayıtları güncel formata taşır. Yeni alan eklenince
        /// burada sürüm sürüm ele alınır (örn. version 0 -> 1: ownedVehicleIds eklendi).
        /// </summary>
        private void Migrate(SaveData data)
        {
            if (data.saveVersion >= CurrentSaveVersion)
            {
                return;
            }

            // Version 0-1 (alanlar yokken kaydedilenler): eksik koleksiyonları doldur.
            if (data.ownedVehicleIds == null)
            {
                data.ownedVehicleIds = new List<string>();
            }

            if (data.recentDeliveryScores == null)
            {
                data.recentDeliveryScores = new List<float>();
            }

            if (data.upgrades == null)
            {
                data.upgrades = new List<UpgradeEntry>();
            }

            Debug.Log($"[SaveSystem] Kayıt version {data.saveVersion} -> {CurrentSaveVersion} taşındı.");
            data.saveVersion = CurrentSaveVersion;
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