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
        // ---------- Shared design tokens ----------
        // One accent + one panel color keeps every runtime-built screen visually
        // consistent instead of each controller picking its own grays.
        public static readonly Color PanelColor = new Color(0.10f, 0.11f, 0.15f, 0.92f);
        public static readonly Color RowColor = new Color(0.16f, 0.18f, 0.23f, 0.95f);
        public static readonly Color AccentColor = new Color(0.25f, 0.55f, 1f, 1f);
        public static readonly Color AcceptColor = new Color(0.22f, 0.62f, 0.32f, 1f);
        public static readonly Color RejectColor = new Color(0.75f, 0.24f, 0.24f, 1f);
        public static readonly Color NeutralButtonColor = new Color(0.22f, 0.26f, 0.33f, 0.95f);
        public static readonly Color BarBackgroundColor = new Color(0f, 0f, 0f, 0.45f);
        public static readonly Color BarGoodColor = new Color(0.3f, 0.75f, 0.35f, 1f);
        public static readonly Color BarWarnColor = new Color(0.95f, 0.7f, 0.2f, 1f);
        public static readonly Color BarDangerColor = new Color(0.85f, 0.25f, 0.25f, 1f);

        private static Font cachedFont;
        private static Sprite roundedSprite;

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

        /// <summary>Shared 9-sliced rounded-rect sprite so panels/buttons/bars aren't flat rectangles, no imported art needed.</summary>
        public static Sprite RoundedSprite
        {
            get
            {
                if (roundedSprite == null)
                {
                    roundedSprite = CreateRoundedRectSprite(64, 64, 16);
                }

                return roundedSprite;
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

            Image image = go.GetComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = color;
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
            return CreateButton(parent, name, label, NeutralButtonColor, onClick);
        }

        /// <summary>Same as <see cref="CreateButton(Transform,string,string,UnityEngine.Events.UnityAction)"/> but with an explicit background color — use AcceptColor/RejectColor for semantic actions.</summary>
        public static Button CreateButton(Transform parent, string name, string label, Color backgroundColor,
            UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            Image image = go.GetComponent<Image>();
            image.sprite = RoundedSprite;
            image.type = Image.Type.Sliced;
            image.color = backgroundColor;

            Button button = go.GetComponent<Button>();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            Text text = CreateText(go.transform, "Label", label, 20, TextAnchor.MiddleCenter, Color.white);
            Stretch((RectTransform)text.transform);
            return button;
        }

        /// <summary>
        /// Background + horizontal fill bar (fuel/condition/timer gauges). Returns
        /// the fill Image so callers can drive fillAmount/color; place the whole
        /// bar via the returned fill's parent RectTransform.
        /// </summary>
        public static Image CreateBar(Transform parent, string name, Color backgroundColor, Color fillColor)
        {
            RectTransform background = CreatePanel(parent, name, backgroundColor);

            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(background, false);

            Image fill = fillGo.GetComponent<Image>();
            fill.sprite = RoundedSprite;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.color = fillColor;
            fill.fillAmount = 1f;

            RectTransform fillRect = (RectTransform)fill.transform;
            Stretch(fillRect);
            // Small inset so the fill never pokes out past the background's rounded border.
            fillRect.offsetMin = new Vector2(2f, 2f);
            fillRect.offsetMax = new Vector2(-2f, -2f);

            return fill;
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

        /// <summary>Picks green/amber/red by how close a 0..1 ratio is to running out — shared by fuel/condition/timer bars.</summary>
        public static Color BarColorForRatio(float ratio01)
        {
            if (ratio01 <= 0.25f) return BarDangerColor;
            if (ratio01 <= 0.5f) return BarWarnColor;
            return BarGoodColor;
        }

        private static Sprite CreateRoundedRectSprite(int width, int height, int radius)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Clamp-to-corner-center distance test: flat on edges/center, rounded only at the 4 corners.
                    int cx = Mathf.Clamp(x, radius, width - radius - 1);
                    int cy = Mathf.Clamp(y, radius, height - radius - 1);
                    float dx = x - cx;
                    float dy = y - cy;
                    bool inside = (dx * dx + dy * dy) <= radius * radius;
                    texture.SetPixel(x, y, inside ? Color.white : new Color(1f, 1f, 1f, 0f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f,
                0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }
    }
}
