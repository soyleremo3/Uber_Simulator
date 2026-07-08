using System;
using TMPro;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Test/geliştirme amaçlı debug ekranı: hız, RPM, vites, teker kayma (slip)
    /// ve TCS değerlerini bir TextMeshProUGUI üzerinde gösterir.
    /// Gerçek oyun HUD'u değildir — sürüş hissini ayarlarken (Adım: "sürüş hissini test et")
    /// sayısal geri bildirim almak içindir.
    /// </summary>
    [RequireComponent(typeof(VehicleController))]
    public class VehicleDebugHUD : MonoBehaviour
    {
        public TextMeshProUGUI text;

        private VehicleController vehicle;
        private float[] wheelSlip;

        private void Start()
        {
            vehicle = GetComponent<VehicleController>();

            if (vehicle == null || vehicle.wheels == null)
            {
                Debug.LogError("[VehicleDebugHUD] VehicleController veya wheels dizisi bulunamadı.");
                enabled = false;
                return;
            }

            wheelSlip = new float[vehicle.wheels.Length];
        }

        private void Update()
        {
            if (text == null)
            {
                return;
            }

            string wheelStates = "";
            for (int i = 0; i < vehicle.wheels.Length; i++)
            {
                VehicleWheel wheel = vehicle.wheels[i];
                float slip = float.IsNaN(wheel.slip) ? 0f : wheel.slip;
                wheelSlip[i] = slip;

                string slipText = wheelSlip[i].ToString("F2");
                if (wheelSlip[i] > 1f)
                    slipText = "<color=blue>" + slipText + "</color>";
                else if (wheelSlip[i] > 0.9f)
                    slipText = "<color=red>" + slipText + "</color>";
                else if (wheelSlip[i] > 0.7f)
                    slipText = "<color=yellow>" + slipText + "</color>";
                else
                    slipText = "<color=green>" + slipText + "</color>";

                wheelStates += slipText + " ";
            }

            float currentRPM = vehicle.engine.GetRPM();
            float maxRPM = vehicle.engine.maxRPM;
            string rpmText = vehicle.engine.GetCurrentGear() + " " + currentRPM.ToString("F0");

            if (vehicle.engine.IsSwitchingGears())
                rpmText = "<color=blue>" + rpmText + "</color>";
            else if (currentRPM > 0.8f * maxRPM)
                rpmText = "<color=red>" + rpmText + "</color>";
            else if (currentRPM > 0.6f * maxRPM)
                rpmText = "<color=yellow>" + rpmText + "</color>";
            else
                rpmText = "<color=green>" + rpmText + "</color>";

            rpmText += " " + vehicle.engine.GetCurrentPower(this).ToString("F2");

            string tcsFactor = "TCS: ";
            for (int i = 0; i < vehicle.wheels.Length; i++)
            {
                tcsFactor += vehicle.wheels[i].tcsReduction.ToString("F2") + " ";
            }

            text.text =
                vehicle.CurrentSpeedKph.ToString("F0") + " kph \n" +
                rpmText + "\n" +
                wheelStates +
                "\n" + tcsFactor;
        }
    }
}