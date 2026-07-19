using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Main menu scene controller. Wire the public methods to buttons in the
    /// Inspector (New Game / Continue / Quit). Continue loads the save after the
    /// gameplay scene finishes loading (managers must exist first).
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Tooltip("Gameplay scene name (must be in Build Settings).")]
        [SerializeField] private string gameplaySceneName = "MainScene";

        private void Start()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.MainMenu);
            }
        }

        public void StartNewGame()
        {
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.DeleteSave();
            }

            LoadGameplayScene();
        }

        public void ContinueGame()
        {
            if (SaveSystem.Instance == null || !SaveSystem.Instance.HasSaveFile())
            {
                NotificationService.Raise("Kayıtlı oyun bulunamadı.");
                return;
            }

            SaveSystem.Instance.LoadGame();
            LoadGameplayScene();
        }

        public void QuitGame()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.QuitGame();
            }
            else
            {
                Application.Quit();
            }
        }

        private void LoadGameplayScene()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
                GameManager.Instance.LoadScene(gameplaySceneName);
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(gameplaySceneName);
            }
        }
    }
}
