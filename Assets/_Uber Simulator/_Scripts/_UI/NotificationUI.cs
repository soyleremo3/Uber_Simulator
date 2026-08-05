using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DeliverySim
{
    /// <summary>
    /// Toast messages from NotificationService. Shows one message at a time for a
    /// few seconds with a simple fade-out.
    /// </summary>
    public class NotificationUI : MonoBehaviour
    {
        [SerializeField] private Text notificationText;
        [SerializeField] private Image backdrop;
        [SerializeField] private float displaySeconds = 3f;
        [SerializeField] private float fadeSeconds = 0.6f;

        private Coroutine activeRoutine;

        public void SetText(Text text)
        {
            notificationText = text;
        }

        /// <summary>Optional pill background that fades in/out together with the toast text.</summary>
        public void SetBackdrop(Image backdropImage)
        {
            backdrop = backdropImage;
        }

        private void OnEnable()
        {
            NotificationService.OnNotification += HandleNotification;
        }

        private void OnDisable()
        {
            NotificationService.OnNotification -= HandleNotification;
        }

        private void Start()
        {
            SetVisualsEnabled(false);
        }

        private void HandleNotification(string message)
        {
            if (notificationText == null)
            {
                return;
            }

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
            }

            activeRoutine = StartCoroutine(ShowRoutine(message));
        }

        private IEnumerator ShowRoutine(string message)
        {
            notificationText.text = message;
            SetVisualsEnabled(true);
            SetAlpha(1f);

            // Realtime wait so toasts still work while the game is paused (timeScale 0).
            yield return new WaitForSecondsRealtime(displaySeconds);

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(1f, 0f, elapsed / fadeSeconds));
                yield return null;
            }

            SetVisualsEnabled(false);
            SetAlpha(1f);
            activeRoutine = null;
        }

        private void SetVisualsEnabled(bool visible)
        {
            if (notificationText != null)
            {
                notificationText.enabled = visible;
            }

            if (backdrop != null)
            {
                backdrop.enabled = visible;
            }
        }

        private void SetAlpha(float alpha)
        {
            if (notificationText != null)
            {
                Color color = notificationText.color;
                color.a = alpha;
                notificationText.color = color;
            }

            if (backdrop != null)
            {
                Color color = backdrop.color;
                color.a = alpha * 0.85f;
                backdrop.color = color;
            }
        }
    }
}
