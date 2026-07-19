using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliverySim
{
    /// <summary>
    /// Central audio playback + volume persistence (PlayerPrefs). Order events are
    /// wired automatically each scene load (OrderManager is scene-local, this is not).
    /// All clip fields are optional — missing clips are silently skipped so the game
    /// runs fine before audio assets exist (see ASSET_NEEDS.md).
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string MusicVolumeKey = "deliverysim_music_volume";
        private const string SfxVolumeKey = "deliverysim_sfx_volume";

        [Header("Music")]
        [SerializeField] private AudioClip backgroundMusic;

        [Header("Order SFX (optional)")]
        [SerializeField] private AudioClip orderAcceptedClip;
        [SerializeField] private AudioClip cargoPickedUpClip;
        [SerializeField] private AudioClip orderCompletedClip;
        [SerializeField] private AudioClip orderFailedClip;

        [Header("UI SFX (optional)")]
        [SerializeField] private AudioClip moneyEarnedClip;
        [SerializeField] private AudioClip errorClip;

        private AudioSource musicSource;
        private AudioSource sfxSource;
        private OrderManager subscribedOrderManager;

        public float MusicVolume
        {
            get => musicSource != null ? musicSource.volume : 1f;
            set
            {
                float v = Mathf.Clamp01(value);
                if (musicSource != null) musicSource.volume = v;
                PlayerPrefs.SetFloat(MusicVolumeKey, v);
            }
        }

        public float SfxVolume
        {
            get => sfxSource != null ? sfxSource.volume : 1f;
            set
            {
                float v = Mathf.Clamp01(value);
                if (sfxSource != null) sfxSource.volume = v;
                PlayerPrefs.SetFloat(SfxVolumeKey, v);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = PlayerPrefs.GetFloat(MusicVolumeKey, 0.6f);

            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.volume = PlayerPrefs.GetFloat(SfxVolumeKey, 1f);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            UnsubscribeOrderEvents();
        }

        private void Start()
        {
            TrySubscribeOrderEvents();

            if (backgroundMusic != null)
            {
                PlayMusic(backgroundMusic);
            }
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // OrderManager lives in the gameplay scene; resubscribe after each load.
            TrySubscribeOrderEvents();
        }

        public void PlaySfx(AudioClip clip)
        {
            if (clip != null && sfxSource != null)
            {
                sfxSource.PlayOneShot(clip);
            }
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || musicSource == null)
            {
                return;
            }

            musicSource.clip = clip;
            musicSource.Play();
        }

        // ---------- Order event hooks ----------

        private void TrySubscribeOrderEvents()
        {
            UnsubscribeOrderEvents();

            subscribedOrderManager = OrderManager.Instance != null
                ? OrderManager.Instance
                : FindFirstObjectByType<OrderManager>();

            if (subscribedOrderManager == null)
            {
                return;
            }

            subscribedOrderManager.OnOrderAccepted += HandleOrderAccepted;
            subscribedOrderManager.OnCargoPickedUp += HandleCargoPickedUp;
            subscribedOrderManager.OnOrderCompleted += HandleOrderCompleted;
            subscribedOrderManager.OnOrderFailed += HandleOrderFailed;
        }

        private void UnsubscribeOrderEvents()
        {
            if (subscribedOrderManager == null)
            {
                return;
            }

            subscribedOrderManager.OnOrderAccepted -= HandleOrderAccepted;
            subscribedOrderManager.OnCargoPickedUp -= HandleCargoPickedUp;
            subscribedOrderManager.OnOrderCompleted -= HandleOrderCompleted;
            subscribedOrderManager.OnOrderFailed -= HandleOrderFailed;
            subscribedOrderManager = null;
        }

        private void HandleOrderAccepted(OrderData order) => PlaySfx(orderAcceptedClip);
        private void HandleCargoPickedUp(OrderData order) => PlaySfx(cargoPickedUpClip);

        private void HandleOrderCompleted(DeliveryResult result)
        {
            PlaySfx(orderCompletedClip);
            PlaySfx(moneyEarnedClip);
        }

        private void HandleOrderFailed(OrderData order)
        {
            PlaySfx(orderFailedClip);
            PlaySfx(errorClip);
        }
    }
}
