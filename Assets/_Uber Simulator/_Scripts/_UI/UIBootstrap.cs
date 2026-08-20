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
            // Stats card: top-left, money/reputation prominent, fuel+condition as bars.
            RectTransform hudPanel = UIFactory.CreatePanel(root, "HUD", UIFactory.PanelColor);
            UIFactory.Place(hudPanel, new Vector2(0f, 0f), new Vector2(14f, 14f), new Vector2(300f, 200f));

            Text money = UIFactory.CreateText(hudPanel, "MoneyText", string.Empty, 24, TextAnchor.MiddleLeft,
                new Color(0.45f, 0.95f, 0.55f));
            UIFactory.Place((RectTransform)money.transform, new Vector2(0f, 1f), new Vector2(14f, -12f), new Vector2(150f, 32f));

            Text reputation = UIFactory.CreateText(hudPanel, "ReputationText", string.Empty, 16, TextAnchor.MiddleRight,
                new Color(1f, 0.85f, 0.4f));
            UIFactory.Place((RectTransform)reputation.transform, new Vector2(1f, 1f), new Vector2(-14f, -16f), new Vector2(120f, 28f));

            Image fuelFill = UIFactory.CreateBar(hudPanel, "FuelBar", UIFactory.BarBackgroundColor, UIFactory.BarGoodColor);
            RectTransform fuelBarRoot = (RectTransform)fuelFill.transform.parent;
            UIFactory.Place(fuelBarRoot, new Vector2(0f, 1f), new Vector2(14f, -58f), new Vector2(272f, 22f));
            Text fuel = UIFactory.CreateText(fuelBarRoot, "FuelText", string.Empty, 14, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch((RectTransform)fuel.transform);

            Image conditionFill = UIFactory.CreateBar(hudPanel, "ConditionBar", UIFactory.BarBackgroundColor, UIFactory.BarGoodColor);
            RectTransform conditionBarRoot = (RectTransform)conditionFill.transform.parent;
            UIFactory.Place(conditionBarRoot, new Vector2(0f, 1f), new Vector2(14f, -88f), new Vector2(272f, 22f));
            Text condition = UIFactory.CreateText(conditionBarRoot, "ConditionText", string.Empty, 14, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch((RectTransform)condition.transform);

            Text cargo = UIFactory.CreateText(hudPanel, "CargoText", "Sipariş yok", 16, TextAnchor.UpperLeft,
                new Color(0.85f, 0.9f, 1f));
            UIFactory.Place((RectTransform)cargo.transform, new Vector2(0f, 1f), new Vector2(14f, -122f), new Vector2(272f, 56f));

            // Speedometer: bottom-right, big — dashboard convention for driving games.
            Text speed = UIFactory.CreateText(root, "SpeedText", "0 km/s", 46, TextAnchor.LowerRight, Color.white);
            UIFactory.Place((RectTransform)speed.transform, new Vector2(1f, 0f), new Vector2(-24f, 24f), new Vector2(260f, 56f));

            // Timer + its bar + distance: top center, the thing you glance at mid-delivery.
            Text timer = UIFactory.CreateText(root, "TimerText", string.Empty, 30, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place((RectTransform)timer.transform, new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(420f, 40f));

            Image timerFill = UIFactory.CreateBar(root, "TimerBar", UIFactory.BarBackgroundColor, UIFactory.BarGoodColor);
            RectTransform timerBarRoot = (RectTransform)timerFill.transform.parent;
            UIFactory.Place(timerBarRoot, new Vector2(0.5f, 1f), new Vector2(0f, -54f), new Vector2(360f, 14f));
            timerFill.fillAmount = 0f;

            Text distance = UIFactory.CreateText(root, "DistanceText", string.Empty, 22, TextAnchor.MiddleCenter, new Color(0.8f, 0.9f, 1f));
            UIFactory.Place((RectTransform)distance.transform, new Vector2(0.5f, 1f), new Vector2(0f, -78f), new Vector2(420f, 30f));

            // Turn-by-turn cue: the ground route ribbon alone doesn't read as guidance
            // from the driver's eye level, so this is the explicit "which way" text.
            Text turn = UIFactory.CreateText(root, "TurnText", string.Empty, 26, TextAnchor.MiddleCenter,
                new Color(1f, 0.85f, 0.3f));
            UIFactory.Place((RectTransform)turn.transform, new Vector2(0.5f, 1f), new Vector2(0f, -110f), new Vector2(420f, 36f));

            HUDController hud = gameObject.AddComponent<HUDController>();
            hud.SetTexts(speed, money, fuel, condition, timer, distance, cargo, reputation);
            hud.SetBars(fuelFill, conditionFill, timerFill);
            hud.SetTurnIndicator(turn);
        }

        private void BuildPrompt(Transform root)
        {
            // Backdrop pill so the prompt reads over any background, not naked floating text.
            RectTransform backdrop = UIFactory.CreatePanel(root, "InteractionPromptBackdrop", new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Place(backdrop, new Vector2(0.5f, 0f), new Vector2(0f, 108f), new Vector2(520f, 52f));

            Text prompt = UIFactory.CreateText(backdrop, "InteractionPrompt", string.Empty, 26,
                TextAnchor.MiddleCenter, new Color(1f, 0.95f, 0.5f));
            UIFactory.Stretch((RectTransform)prompt.transform);

            InteractionPromptUI promptUi = gameObject.AddComponent<InteractionPromptUI>();
            promptUi.SetText(prompt);
            promptUi.SetBackdrop(backdrop.gameObject);
        }

        private void BuildNotification(Transform root)
        {
            // Backdrop pill behind the toast — plain floating text is hard to read over gameplay.
            RectTransform backdrop = UIFactory.CreatePanel(root, "NotificationBackdrop", new Color(0.05f, 0.06f, 0.08f, 0.85f));
            UIFactory.Place(backdrop, new Vector2(0.5f, 1f), new Vector2(0f, -96f), new Vector2(760f, 48f));

            Text toast = UIFactory.CreateText(backdrop, "NotificationText", string.Empty, 22,
                TextAnchor.MiddleCenter, new Color(0.75f, 1f, 0.8f));
            UIFactory.Stretch((RectTransform)toast.transform);

            NotificationUI notification = gameObject.AddComponent<NotificationUI>();
            notification.SetText(toast);
            notification.SetBackdrop(backdrop.GetComponent<Image>());
        }

        private void BuildOrderPanel(Transform root)
        {
            RectTransform panel = UIFactory.CreatePanel(root, "OrderPanel", UIFactory.PanelColor);
            UIFactory.Place(panel, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(470f, 480f));

            Text title = UIFactory.CreateText(panel, "Title", "SİPARİŞLER [Tab]", 24, TextAnchor.MiddleCenter, UIFactory.AccentColor);
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
            RectTransform panel = UIFactory.CreatePanel(root, "ShopPanel", UIFactory.PanelColor);
            UIFactory.Place(panel, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 520f));

            Text title = UIFactory.CreateText(panel, "Title", "MAĞAZA [B]", 24, TextAnchor.MiddleCenter, UIFactory.AcceptColor);
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

            Button resume = UIFactory.CreateButton(panel, "ResumeButton", "Devam Et", UIFactory.AcceptColor, controller.Resume);
            UIFactory.Place((RectTransform)resume.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(240f, 52f));

            Button save = UIFactory.CreateButton(panel, "SaveButton", "Kaydet", UIFactory.AccentColor, controller.SaveGame);
            UIFactory.Place((RectTransform)save.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -24f), new Vector2(240f, 52f));

            Button quit = UIFactory.CreateButton(panel, "QuitButton", "Çıkış", UIFactory.RejectColor, controller.QuitGame);
            UIFactory.Place((RectTransform)quit.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, -88f), new Vector2(240f, 52f));
        }

        private void BuildHelpText(Transform root)
        {
            Text help = UIFactory.CreateText(root, "HelpText",
                "WASD: Sür  |  Space: El freni  |  E: Etkileşim  |  Tab: Siparişler  |  B: Mağaza  |  C: Kamera  |  R: Aracı Düzelt  |  LShift/LCtrl: Vites  |  Esc: Duraklat",
                16, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.55f));
            UIFactory.Place((RectTransform)help.transform, new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(1000f, 24f));
        }
    }
}
