using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeliverySim
{
    /// <summary>
    /// Shows the game-over screen when the vehicle is totaled (VehicleCondition
    /// drops to/below its totaled threshold). Owns only the panel visuals and
    /// buttons; GameState.GameOver — which freezes time — goes through GameManager,
    /// same split as PauseMenuController.
    /// </summary>
    public class GameOverController : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;

        private VehicleCondition vehicleCondition;
        private bool shown;

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

            VehicleController vehicle = FindFirstObjectByType<VehicleController>();
            if (vehicle != null)
            {
                vehicleCondition = vehicle.GetComponent<VehicleCondition>();
            }

            if (vehicleCondition != null)
            {
                vehicleCondition.OnVehicleTotaled += HandleVehicleTotaled;
            }
        }

        private void OnDestroy()
        {
            if (vehicleCondition != null)
            {
                vehicleCondition.OnVehicleTotaled -= HandleVehicleTotaled;
            }
        }

        private void HandleVehicleTotaled()
        {
            if (shown)
            {
                return;
            }

            shown = true;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.GameOver);
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
            }

            NotificationService.Raise("Araç pert oldu!");
        }

        /// <summary>Wired to the "Yeniden Başla" button.</summary>
        public void Restart()
        {
            Time.timeScale = 1f;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetGameState(GameState.Playing);
                GameManager.Instance.ReloadCurrentScene();
                return;
            }

            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>Wired to the "Çıkış" button.</summary>
        public void QuitGame()
        {
            Time.timeScale = 1f;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.QuitGame();
            }
        }
    }
}
