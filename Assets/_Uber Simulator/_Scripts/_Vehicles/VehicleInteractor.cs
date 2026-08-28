using System;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Lets the vehicle interact with nearby IInteractable objects (pickup/delivery
    /// points, fuel/repair stations). Scans with OverlapSphere on an interval and
    /// exposes the current prompt for UI.
    /// Key is F — E is already taken by gear-up in VehicleController.
    /// </summary>
    public class VehicleInteractor : MonoBehaviour
    {
        [Header("Detection")]
        [SerializeField] private float interactRadius = 6f;
        [Tooltip("Seconds between proximity scans (scanning every frame is wasteful).")]
        [SerializeField] private float scanInterval = 0.2f;
        [Tooltip("Restrict to the Interactable layer — in a collider-dense scene an all-layers scan overflows the fixed buffer before the pickup/delivery trigger is even reached.")]
        [SerializeField] private LayerMask interactMask = ~0;

        [Header("Input")]
        // E = industry-standard interact. Gears moved to LShift/LCtrl to free it up.
        [SerializeField] private KeyCode interactKey = KeyCode.E;

        private readonly Collider[] scanBuffer = new Collider[32];
        private IInteractable currentInteractable;
        private float scanTimer;
        private string currentPrompt = string.Empty;

        /// <summary>Fires when the on-screen prompt should change; empty string = hide.</summary>
        public event Action<string> OnPromptChanged;

        public IInteractable CurrentInteractable => currentInteractable;

        private void Update()
        {
            scanTimer -= Time.deltaTime;
            if (scanTimer <= 0f)
            {
                scanTimer = scanInterval;
                ScanForInteractable();
            }

            if (currentInteractable != null && Input.GetKeyDown(interactKey))
            {
                currentInteractable.Interact(gameObject);
                // Re-scan immediately: the interaction usually changes the prompt.
                ScanForInteractable();
            }
        }

        private void ScanForInteractable()
        {
            IInteractable nearest = null;
            float nearestSqr = float.MaxValue;

            int hits = Physics.OverlapSphereNonAlloc(
                transform.position, interactRadius, scanBuffer, interactMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < hits; i++)
            {
                Collider hit = scanBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                IInteractable interactable = hit.GetComponentInParent<IInteractable>();
                if (interactable == null)
                {
                    continue;
                }

                // Skip interactables that currently have nothing to say
                // (e.g. a delivery point that is not the active target).
                string prompt = interactable.GetInteractionPrompt();
                if (string.IsNullOrEmpty(prompt))
                {
                    continue;
                }

                // IUsable gate: respect CanUse when implemented.
                if (interactable is IUsable usable && !usable.CanUse(gameObject))
                {
                    continue;
                }

                float sqr = (hit.transform.position - transform.position).sqrMagnitude;
                if (sqr < nearestSqr)
                {
                    nearestSqr = sqr;
                    nearest = interactable;
                }
            }

            currentInteractable = nearest;
            string newPrompt = nearest != null ? nearest.GetInteractionPrompt() : string.Empty;

            if (newPrompt != currentPrompt)
            {
                currentPrompt = newPrompt;
                OnPromptChanged?.Invoke(currentPrompt);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, interactRadius);
        }
    }
}
