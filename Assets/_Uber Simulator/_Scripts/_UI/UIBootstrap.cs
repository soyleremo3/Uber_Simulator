using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DeliverySim
{
    /// <summary>
    /// Builds the entire gameplay UI at runtime (canvas, HUD, order panel, shop
    /// panel, pause menu, prompt, toasts) so the game is testable before any
    /// hand-made canvas exists. Drop this on one scene object — nothing to wire.
    /// Later, replace with a hand-built canvas and simply disable this component.
    /// </summary>
    public class UIBootstrap : MonoBehaviour
    {
        [SerializeField] private bool buildOnStart = true;

        private void Start()
        {
            if (buildOnStart)
            {
                Build();
            }
        }

        public void Build()
        {
            EnsureEventSystem();

            Canvas canvas = UIFactory.CreateCanvas("RuntimeCanvas");
            canvas.transform.SetParent(transform, false);
            Transform root = canvas.transform;

            BuildHud(root);
            BuildPrompt(root);
            BuildNotification(root);
            BuildOrderPanel(root);
            BuildShopPanel(root);
            BuildPauseMenu(root);
            BuildHelpText(root);
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() == null)
            {
                new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            }
        }

        private void BuildHud(Transform root)
        {
            RectTransform hudPanel = UIFactory.CreatePanel(root, "HUD", new Color(0f, 0f, 0f, 0.35f));
            UIFactory.Place(hudPanel, new Vector2(0f, 0f), new Vector2(10f, 10f), new Vector2(320f, 190f));

            Text speed = CreateHudLine(hudPanel, "SpeedText", 0);
            Text money = CreateHudLine(hudPanel, "MoneyText", 1);
            Text fuel = CreateHudLine(hudPanel, "FuelText", 2);
            Text condition = CreateHudLine(hudPanel, "ConditionText", 3);
            Text reputation = CreateHudLine(hudPanel, "ReputationText", 4);
            Text cargo = CreateHudLine(hudPanel, "CargoText", 5);

            // Timer + distance: top center, big and visible while driving.
            Text timer = UIFactory.CreateText(root, "TimerText", string.Empty, 30, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place((RectTransform)timer.transform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(420f, 40f));

            Text distance = UIFactory.CreateText(root, "DistanceText", string.Empty, 22, TextAnchor.MiddleCenter, new Color(0.8f, 0.9f, 1f));
            UIFactory.Place((RectTransform)distance.transform, new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(420f, 30f));

            HUDController hud = gameObject.AddComponent<HUDController>();
            hud.SetTexts(speed, money, fuel, condition, timer, distance, cargo, reputation);
        }

        private Text CreateHudLine(RectTransform parent, string name, int index)
        {
            Text text = UIFactory.CreateText(parent, name, string.Empty, 20, TextAnchor.MiddleLeft, Color.white);
            UIFactory.Place((RectTransform)text.transform, new Vector2(0f, 1f),
                new Vector2(12f, -8f - index * 30f), new Vector2(300f, 28f));
            return text;
        }

        private void BuildPrompt(Transform root)
        {
            Text prompt = UIFactory.CreateText(root, "InteractionPrompt", string.Empty, 26,
                TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.5f));
            UIFactory.Place((RectTransform)prompt.transform, new Vector2(0.5f, 0f),
                new Vector2(0f, 120f), new Vector2(500f, 40f));

            InteractionPromptUI promptUi = gameObject.AddComponent<InteractionPromptUI>();
            promptUi.SetText(prompt);
        }

        private void BuildNotification(Transform root)
        {
            Text toast = UIFactory.CreateText(root, "NotificationText", string.Empty, 24,
                TextAnchor.MiddleCenter, new Color(0.7f, 1f, 0.7f));
            UIFactory.Place((RectTransform)toast.transform, new Vector2(0.5f, 1f),
                new Vector2(0f, -100f), new Vector2(720f, 36f));

            NotificationUI notification = gameObject.AddComponent<NotificationUI>();
            notification.SetText(toast);
        }

        private void BuildOrderPanel(Transform root)
        {
            RectTransform panel = UIFactory.CreatePanel(root, "OrderPanel", new Color(0.05f, 0.06f, 0.09f, 0.92f));
            UIFactory.Place(panel, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(470f, 480f));

            Text title = UIFactory.CreateText(panel, "Title", "SİPARİŞLER [Tab]", 24, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(440f, 36f));

            var listGo = new GameObject("OfferList", typeof(RectTransform));
            listGo.transform.SetParent(panel, false);
            var list = (RectTransform)listGo.transform;
            list.anchorMin = new Vector2(0f, 0f);
            list.anchorMax = new Vector2(1f, 1f);
            list.offsetMin = new Vector2(8f, 8f);
            list.offsetMax = new Vector2(-8f, -52f);

            OrderPanelController controller = gameObject.AddComponent<OrderPanelController>();
            controller.SetReferences(panel.gameObject, list);
        }

        private void BuildShopPanel(Transform root)
        {
            RectTransform panel = UIFactory.CreatePanel(root, "ShopPanel", new Color(0.05f, 0.08f, 0.06f, 0.92f));
            UIFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 520f));

            Text title = UIFactory.CreateText(panel, "Title", "MAĞAZA [B]", 24, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(460f, 36f));

            var listGo = new GameObject("ShopList", typeof(RectTransform));
            listGo.transform.SetParent(panel, false);
            var list = (RectTransform)listGo.transform;
            list.anchorMin = new Vector2(0f, 0f);
            list.anchorMax = new Vector2(1f, 1f);
            list.offsetMin = new Vector2(8f, 8f);
            list.offsetMax = new Vector2(-8f, -52f);

            ShopPanelController controller = gameObject.AddComponent<ShopPanelController>();
            controller.SetReferences(panel.gameObject, list);
        }

        private void BuildPauseMenu(Transform root)
        {
            RectTransform panel = UIFactory.CreatePanel(root, "PausePanel", new Color(0f, 0f, 0f, 0.75f));
            UIFactory.Stretch(panel);

            Text title = UIFactory.CreateText(panel, "Title", "DURAKLATILDI", 42, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place((RectTransform)title.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 130f), new Vector2(500f, 60f));

            PauseMenuController controller = gameObject.AddComponent<PauseMenuController>();
            controller.SetReferences(panel.gameObject);

            Button resume = UIFactory.CreateButton(panel, "ResumeButton", "Devam Et", controller.Resume);
            UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(240f, 52f));

            Button save = UIFactory.CreateButton(panel, "SaveButton", "Kaydet", controller.SaveGame);
            UIFactory.Place((RectTransform)save.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(240f, 52f));

            Button quit = UIFactory.CreateButton(panel, "QuitButton", "Çıkış", controller.QuitGame);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(240f, 52f));
        }

        private void BuildHelpText(Transform root)
        {
            Text help = UIFactory.CreateText(root, "HelpText",
                "WASD: Sür  |  Space: El freni  |  F: Etkileşim  |  Tab: Siparişler  |  B: Mağaza  |  Esc: Duraklat",
                16, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            UIFactory.Place((RectTransform)help.transform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(1000f, 24f));
        }
    }
}
