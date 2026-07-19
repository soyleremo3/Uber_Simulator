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

        private VehicleInteractor interactor;

        public void SetText(Text text)
        {
            promptText = text;
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

            promptText.text = prompt;
            promptText.enabled = !string.IsNullOrEmpty(prompt);
        }
    }
}
