using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliverySim
{
    public enum GameState
    {
        MainMenu,
        Playing,
        Paused,
        OrderActive,
        GameOver,
        Shop
    }

    /// <summary>
    /// Oyunun genel durumunu ve sahne geçişlerini yöneten singleton.
    /// Sahneler arası hayatta kalır (DontDestroyOnLoad).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        /// <summary>
        /// Oyun durumu değiştiğinde tetiklenir. UI ve diğer sistemler bunu dinleyebilir.
        /// </summary>
        public event System.Action<GameState> OnGameStateChanged;

        private float previousTimeScale = 1f;

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

        public void SetGameState(GameState newState)
        {
            if (CurrentState == newState)
            {
                return;
            }

            CurrentState = newState;
            OnGameStateChanged?.Invoke(newState);

            if (newState == GameState.Paused)
            {
                previousTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            else if (Time.timeScale == 0f)
            {
                Time.timeScale = previousTimeScale > 0f ? previousTimeScale : 1f;
            }
        }

        public void LoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[GameManager] LoadScene çağrıldı ama sceneName boş.");
                return;
            }

            SceneManager.LoadScene(sceneName);
        }

        public void ReloadCurrentScene()
        {
            Scene currentScene = SceneManager.GetActiveScene();
            SceneManager.LoadScene(currentScene.buildIndex);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}