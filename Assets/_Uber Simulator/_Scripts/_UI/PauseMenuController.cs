using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Escape toggles pause. Pausing goes through GameManager (which handles
    /// timeScale); this controller only owns the panel visuals and buttons.
    /// </summary>
    public class PauseMenuController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private KeyCode pauseKey = KeyCode.Escape;

        private GameState stateBeforePause = GameState.Playing;

        public void SetReferences(GameObject root)
        {
            panelRoot = root;
        }

        private void Start()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(pauseKey))
            {
                TogglePause();
            }
        }

        public void TogglePause()
        {
            if (GameManager.Instance == null)
            {
                return;
            }

            if (GameManager.Instance.CurrentState == GameState.Paused)
            {
                Resume();
            }
            else
            {
                stateBeforePause = GameManager.Instance.CurrentState;
                GameManager.Instance.SetGameState(GameState.Paused);
                if (panelRoot != null)
                {
                    panelRoot.SetActive(true);
                }
            }
        }

        /// <summary>Wired to the Resume button.</summary>
        public void Resume()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(stateBeforePause);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        /// <summary>Wired to the Save button.</summary>
        public void SaveGame()
        {
            if (SaveSystem.Instance != null)
            {
                SaveSystem.Instance.SaveGame();
                NotificationService.Raise("Oyun kaydedildi.");
            }
        }

        /// <summary>Wired to the Quit button.</summary>
        public void QuitGame()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.QuitGame();
            }
        }
    }
}
