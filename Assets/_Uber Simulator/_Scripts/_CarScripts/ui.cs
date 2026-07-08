using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class ui : MonoBehaviour
{
    // public TextMeshPro text
    public TextMeshProUGUI text;
    private Car car;
    float[] wheelSlip;
    void Start()
    {
        car = GetComponent<Car>();
        wheelSlip = new float[car.wheels.Length];
        // set all to 0
        for (int i = 0; i < wheelSlip.Length; i++)
        {
            wheelSlip[i] = 0.0f;
        }
    }
    public void SetText(string newText)
    {
        if (text != null)
        {
            text.text = newText;
        }
        else
        {
            Debug.LogWarning("TextMeshProUGUI component is not assigned.");
        }
    }

    void Update()
    {
        String wheelStates = "";
        int at = 0;
        foreach (WheelProperties wheel in car.wheels)
        {
            float slip = float.IsNaN(wheel.slip) ? 0f : wheel.slip;
            wheelSlip[at] = slip;

            string slipText = wheelSlip[at].ToString("F2");
            if (wheelSlip[at] > 1f)
                slipText = "<color=blue>" + slipText + "</color>";
            else if (wheelSlip[at] > 0.9f)
                slipText = "<color=red>" + slipText + "</color>";
            else if (wheelSlip[at] > 0.7f)
                slipText = "<color=yellow>" + slipText + "</color>";
            else
                slipText = "<color=green>" + slipText + "</color>";

            wheelStates += slipText + " ";
            at++;
        }

        float currentRPM = car.e.getRPM();
        float maxRPM = car.e.maxRPM;  // Assumes you have this accessible
        string rpmText = car.e.getCurrentGear() + " " + currentRPM.ToString("F0");
        if (car.e.isSwitchingGears())
        {
            rpmText = "<color=blue>" + rpmText + "</color>";
        }
        else if (currentRPM > 0.8f * maxRPM)
            rpmText = "<color=red>" + rpmText + "</color>";
        else if (currentRPM > 0.6f * maxRPM)
            rpmText = "<color=yellow>" + rpmText + "</color>";
        else
            rpmText = "<color=green>" + rpmText + "</color>";

        rpmText += " " + car.e.GetCurrentPower(this).ToString("F2");

        string tcsFactor = "TCS: ";
        for (int i = 0; i < car.wheels.Length; i++)
        {
            tcsFactor += car.wheels[i].tcsReduction.ToString("F2") + " ";
        }
        text.text =
                    // (xV = Mathf.Lerp(xV, car.userInput.x, 0.05f)).ToString("F2") + "\n" +
                    // (yV = Mathf.Lerp(yV, (float)(car.userInput.y - (car.isBraking ? 1.0 : 0.0)), 0.05f)).ToString("F2") + "\n" +
                    (car.rb.linearVelocity.magnitude * 3.6f).ToString("F0") + " kph \n" +
                    rpmText + "\n" +
                    wheelStates +
                    "\n" + tcsFactor;
    }
}
