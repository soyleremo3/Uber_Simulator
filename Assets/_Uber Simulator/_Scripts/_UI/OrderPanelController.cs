using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DeliverySim
{
    /// <summary>
    /// "Phone" screen: lists current offers with accept/reject buttons.
    /// Rows are built at runtime inside listContainer, so the panel works both
    /// with the UIBootstrap canvas and a hand-built canvas. Toggle key: Tab.
    /// Rebuilds only on OnOffersChanged (event-driven, not per-frame).
    /// </summary>
    public class OrderPanelController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Panel root toggled on/off. Defaults to this GameObject.")]
        [SerializeField] private GameObject panelRoot;
        [Tooltip("Rows are instantiated under this container.")]
        [SerializeField] private RectTransform listContainer;

        [Header("Input")]
        // Tab = phone/menu standard. Camera mode moved to C (CameraModeController).
        [SerializeField] private KeyCode toggleKey = KeyCode.Tab;

        [Header("Layout")]
        [SerializeField] private float rowHeight = 84f;
        [SerializeField] private float rowSpacing = 8f;

        private readonly List<GameObject> rows = new List<GameObject>();
        private bool subscribed;

        public void SetReferences(GameObject root, RectTransform container)
        {
            panelRoot = root;
            listContainer = container;
        }

        private void Start()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            TrySubscribe();
            Rebuild();
            panelRoot.SetActive(false);
        }

        private void OnDestroy()
        {
            if (subscribed && OrderManager.Instance != null)
            {
                OrderManager.Instance.OnOffersChanged -= HandleOffersChanged;
            }
        }

        private void Update()
        {
            if (Input.GetKeyDown(toggleKey))
            {
                TrySubscribe(); // OrderManager may spawn after this panel.
                bool show = !panelRoot.activeSelf;
                panelRoot.SetActive(show);
                if (show)
                {
                    Rebuild();
                }
            }
        }

        private void TrySubscribe()
        {
            if (!subscribed && OrderManager.Instance != null)
            {
                OrderManager.Instance.OnOffersChanged += HandleOffersChanged;
                subscribed = true;
            }
        }

        private void HandleOffersChanged(IReadOnlyList<OrderOffer> offers)
        {
            if (panelRoot != null && panelRoot.activeSelf)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            foreach (GameObject row in rows)
            {
                if (row != null)
                {
                    Destroy(row);
                }
            }

            rows.Clear();

            if (listContainer == null || OrderManager.Instance == null)
            {
                return;
            }

            IReadOnlyList<OrderOffer> offers = OrderManager.Instance.CurrentOffers;

            if (offers.Count == 0)
            {
                Text empty = UIFactory.CreateText(listContainer, "EmptyInfo",
                    "Şu an teklif yok. Birazdan yenileri gelecek...", 20, TextAnchor.MiddleCenter, Color.gray);
                LayoutElement emptyLe = empty.gameObject.AddComponent<LayoutElement>();
                emptyLe.preferredHeight = 60f;
                emptyLe.minHeight = 60f;
                rows.Add(empty.gameObject);
                return;
            }

            for (int i = 0; i < offers.Count; i++)
            {
                rows.Add(BuildRow(offers[i], i));
            }
        }

        private GameObject BuildRow(OrderOffer offer, int index)
        {
            RectTransform row = UIFactory.CreatePanel(listContainer, $"OrderRow_{index}", UIFactory.RowColor);
            // Height comes from the LayoutElement; the parent VerticalLayoutGroup
            // handles position + width, so no manual index offset.
            LayoutElement rowLe = row.gameObject.AddComponent<LayoutElement>();
            rowLe.preferredHeight = rowHeight;
            rowLe.minHeight = rowHeight;

            int timeLimitMinutes = Mathf.FloorToInt(offer.TimeLimit / 60f);
            int timeLimitSeconds = Mathf.FloorToInt(offer.TimeLimit % 60f);

            float distanceM = offer.DistanceMeters;
            string distanceText = distanceM <= 0f
                ? "—"
                : (distanceM >= 1000f ? $"{distanceM / 1000f:0.0} km" : $"{distanceM:0} m");

            Text info = UIFactory.CreateText(row, "Info",
                $"{offer.DisplayName}  ({offer.CargoType.Label()})\n₺{offer.Payment:F0}  •  {distanceText}  •  Süre: {timeLimitMinutes:00}:{timeLimitSeconds:00}",
                18, TextAnchor.UpperLeft, Color.white);
            UIFactory.Place((RectTransform)info.transform, new Vector2(0f, 1f),
                new Vector2(12f, -8f), new Vector2(280f, rowHeight - 16f));

            OrderOffer captured = offer;
            Button accept = UIFactory.CreateButton(row, "AcceptButton", "Kabul", UIFactory.AcceptColor,
                () => OrderManager.Instance?.AcceptOffer(captured));
            UIFactory.Place((RectTransform)accept.transform, new Vector2(1f, 0.5f),
                new Vector2(-96f, 0f), new Vector2(80f, 40f));

            Button reject = UIFactory.CreateButton(row, "RejectButton", "Reddet", UIFactory.RejectColor,
                () => OrderManager.Instance?.RejectOffer(captured));
            UIFactory.Place((RectTransform)reject.transform, new Vector2(1f, 0.5f),
                new Vector2(-8f, 0f), new Vector2(80f, 40f));

            return row.gameObject;
        }
    }
}
