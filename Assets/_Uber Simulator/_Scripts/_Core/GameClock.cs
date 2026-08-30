using System;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// In-game clock. One in-game day is compressed into <see cref="dayLengthRealMinutes"/>
    /// of real time; a fresh day starts at 06:00. Drives the order-board demand
    /// curve (OrderManager) and later the day/night visuals (TODO #36).
    /// Scene-local like RouteManager — NO DontDestroyOnLoad, rebuilt per scene.
    /// </summary>
    public class GameClock : MonoBehaviour
    {
        public static GameClock Instance { get; private set; }

        [SerializeField] private float dayLengthRealMinutes = 20f;
        [SerializeField] private float startHour = 6f;
        [Tooltip("Gece kabul edilen saat aralığı [nightStartHour, nightEndHour). Sipariş akışını ve (sonra) ışığı etkiler.")]
        [SerializeField] private float nightStartHour = 22f;
        [SerializeField] private float nightEndHour = 6f;

        private float hours; // 0..24
        private int dayIndex;
        private int lastWholeHour = -1;

        /// <summary>Current time of day, 0..24.</summary>
        public float Hour => hours;
        /// <summary>Current time of day normalised to 0..1.</summary>
        public float Hours01 => Mathf.Repeat(hours, 24f) / 24f;
        public int DayIndex => dayIndex;
        public bool IsNight => IsNightHour(hours);

        /// <summary>Fires when the integer hour ticks over; arg = the new whole hour (0..23).</summary>
        public event Action<int> OnHourChanged;
        /// <summary>Fires at the 24h wrap; arg = the new day index.</summary>
        public event Action<int> OnDayRolled;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            hours = Mathf.Repeat(startHour, 24f);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            if (dayLengthRealMinutes <= 0f)
            {
                return;
            }

            float hoursPerRealSecond = 24f / (dayLengthRealMinutes * 60f);
            hours += hoursPerRealSecond * Time.deltaTime;

            if (hours >= 24f)
            {
                hours -= 24f;
                dayIndex++;
                OnDayRolled?.Invoke(dayIndex);
            }

            int whole = Mathf.FloorToInt(hours);
            if (whole != lastWholeHour)
            {
                lastWholeHour = whole;
                OnHourChanged?.Invoke(whole);
            }
        }

        public bool IsNightHour(float h)
        {
            h = Mathf.Repeat(h, 24f);
            return nightStartHour < nightEndHour
                ? (h >= nightStartHour && h < nightEndHour)
                : (h >= nightStartHour || h < nightEndHour);
        }

        /// <summary>"HH:MM" for the HUD.</summary>
        public string TimeLabel()
        {
            int h = Mathf.FloorToInt(Mathf.Repeat(hours, 24f));
            int m = Mathf.FloorToInt((hours - Mathf.Floor(hours)) * 60f);
            return $"{h:00}:{m:00}";
        }

        // ---------- Save / load ----------

        public float GetClockHours() => hours;
        public int GetDayIndex() => dayIndex;

        public void RestoreClock(float clockHours, int day)
        {
            hours = Mathf.Repeat(clockHours <= 0f ? startHour : clockHours, 24f);
            dayIndex = Mathf.Max(0, day);
            lastWholeHour = -1;
        }
    }
}
