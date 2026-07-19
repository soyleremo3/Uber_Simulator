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
        [SerializeField] private float displaySeconds = 3f;
        [SerializeField] private float fadeSeconds = 0.6f;

        private Coroutine activeRoutine;

        public void SetText(Text text)
        {
            notificationText = text;
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
            if (notificationText != null)
            {
                notificationText.enabled = false;
            }
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
            notificationText.enabled = true;

            Color baseColor = notificationText.color;
            baseColor.a = 1f;
            notificationText.color = baseColor;

            // Realtime wait so toasts still work while the game is paused (timeScale 0).
            yield return new WaitForSecondsRealtime(displaySeconds);

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                Color color = notificationText.color;
                color.a = Mathf.Lerp(1f, 0f, elapsed / fadeSeconds);
                notificationText.color = color;
                yield return null;
            }

            notificationText.enabled = false;
            Color reset = notificationText.color;
            reset.a = 1f;
            notificationText.color = reset;
            activeRoutine = null;
        }
    }
}
