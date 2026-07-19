using UnityEngine;
using UnityEngine.UI;

namespace DeliverySim
{
    /// <summary>
    /// Runtime uGUI construction helpers, shared by UIBootstrap and panel
    /// controllers that build rows dynamically. Uses legacy Text on purpose:
    /// it needs zero imported assets (TMP essentials), so the game runs out of
    /// the box. Swap to TMP during the polish phase if desired.
    /// </summary>
    public static class UIFactory
    {
        private static Font cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (cachedFont == null)
                {
                    // Unity 6 built-in font (Arial.ttf was removed).
                    cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return cachedFont;
            }
        }

        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            return canvas;
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static Text CreateText(Transform parent, string name, string content,
            int fontSize, TextAnchor alignment, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            Text text = go.GetComponent<Text>();
            text.font = DefaultFont;
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = new Color(0.2f, 0.25f, 0.3f, 0.95f);

            Button button = go.GetComponent<Button>();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            Text text = CreateText(go.transform, "Label", label, 20, TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)text.transform);
            return button;
        }

        /// <summary>Anchors a RectTransform to fully stretch inside its parent.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Anchors + positions a RectTransform with a fixed size.</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 anchoredPosition, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;
        }
    }
}
