using System;

namespace DeliverySim
{
    /// <summary>
    /// Tiny static event hub for player-facing toast messages
    /// ("Sipariş kabul edildi", "Yetersiz bakiye" ...). Gameplay code raises,
    /// UI (NotificationUI) listens. Keeps gameplay decoupled from UI.
    /// </summary>
    public static class NotificationService
    {
        public static event Action<string> OnNotification;

        public static void Raise(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            OnNotification?.Invoke(message);
        }
    }
}
