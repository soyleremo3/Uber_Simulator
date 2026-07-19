using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Full-repair interaction: charges per missing condition point, all-or-nothing.
    /// Needs a trigger collider so VehicleInteractor can detect it.
    /// </summary>
    public class RepairStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private float costPerConditionPoint = 4f;

        public void Interact(GameObject interactor)
        {
            if (interactor == null)
            {
                return;
            }

            VehicleCondition condition = interactor.GetComponentInParent<VehicleCondition>();
            if (condition == null)
            {
                Debug.LogWarning("[RepairStation] Etkileşen objede VehicleCondition yok.");
                return;
            }

            float missing = condition.MaxCondition - condition.CurrentCondition;
            if (missing <= 0.5f)
            {
                NotificationService.Raise("Araç zaten sağlam.");
                return;
            }

            float cost = missing * costPerConditionPoint;
            if (EconomyManager.Instance == null || !EconomyManager.Instance.SpendMoney(cost))
            {
                NotificationService.Raise($"Tamir için {cost:F0} para gerekli — bakiye yetersiz.");
                return;
            }

            condition.RepairFully();
            NotificationService.Raise($"Araç tamir edildi (-{cost:F0} para).");
        }

        public string GetInteractionPrompt()
        {
            return $"Tamir Et [F] ({costPerConditionPoint:F1}/puan)";
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.6f, 0.6f, 0.9f);
            Gizmos.DrawWireCube(transform.position + Vector3.up * 1.5f, new Vector3(3f, 3f, 3f));

#if UNITY_EDITOR
            UnityEditor.Handles.Label(transform.position + Vector3.up * 3.5f, "RepairStation");
#endif
        }
    }
}
