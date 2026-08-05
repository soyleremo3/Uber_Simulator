using UnityEngine;
using UnityEngine.UI;

namespace DeliverySim
{
    /// <summary>
    /// Shows/hides the "Yükü Al [F]" style prompt. Listens to
    /// VehicleInteractor.OnPromptChanged — no per-frame polling.
    /// </summary>
    public class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private Text promptText;
        [SerializeField] private GameObject backdrop;

        private VehicleInteractor interactor;

        public void SetText(Text text)
        {
            promptText = text;
        }

        /// <summary>Optional pill background shown/hidden together with the prompt text.</summary>
        public void SetBackdrop(GameObject backdropObject)
        {
            backdrop = backdropObject;
        }

        private void Start()
        {
            interactor = FindFirstObjectByType<VehicleInteractor>();
            if (interactor != null)
            {
                interactor.OnPromptChanged += HandlePromptChanged;
            }

            HandlePromptChanged(string.Empty);
        }

        private void OnDestroy()
        {
            if (interactor != null)
            {
                interactor.OnPromptChanged -= HandlePromptChanged;
            }
        }

        private void HandlePromptChanged(string prompt)
        {
            if (promptText == null)
            {
                return;
            }

            bool show = !string.IsNullOrEmpty(prompt);
            promptText.text = prompt;
            promptText.enabled = show;

            if (backdrop != null)
            {
                backdrop.SetActive(show);
            }
        }
    }
}
