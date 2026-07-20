using System;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Handles the vehicle's engine/gearbox simulation. Produces power based on RPM
    /// and shifts gears automatically.
    /// </summary>
    [System.Serializable]
    public class VehicleEngine
    {
        public float idleRPM = 2400f;
        public float maxRPM = 7000f;
        public float[] gearRatios = { 3.50f, 2.80f, 2.30f, 1.90f, 1.60f, 1.30f, 1.00f, 0.85f };
        public float finalDriveRatio = 4.0f;
        public bool automaticTransmission = true;

        private int currentGear = 0;
        private bool switchingGears = false;
        private float gearChangeTime = 0.18f; // seconds to switch gears
        private float rpm = 0f;

        public void SetRPM(float averageWheelAngularVelocity)
        {
            float averageWheelRPM = (averageWheelAngularVelocity * 60f) / (2f * Mathf.PI);
            float totalRatio = Math.Abs(gearRatios[currentGear] * finalDriveRatio);
            float transmissionRPM = averageWheelRPM * totalRatio;
            float targetRPM = Mathf.Max(idleRPM, transmissionRPM);
            rpm = Mathf.Clamp(targetRPM, idleRPM, maxRPM);
        }

        /// <summary>Returns a 0-1 power ratio based on current RPM.</summary>
        public float GetCurrentPower(MonoBehaviour context)
        {
            if (switchingGears) return 0.3f; // Less power during gear switch
            return Mathf.Clamp01(rpm / maxRPM);
        }

        public void UpGear(MonoBehaviour context)
        {
            if (currentGear < gearRatios.Length - 1 && !switchingGears)
            {
                currentGear++;
                switchingGears = true;
                context.StartCoroutine(ResetSwitchingGearsCoroutine());
            }
        }

        public void DownGear(MonoBehaviour context)
        {
            if (currentGear > 0 && !switchingGears)
            {
                currentGear--;
                switchingGears = true;
                context.StartCoroutine(ResetSwitchingGearsCoroutine());
            }
        }

        private System.Collections.IEnumerator ResetSwitchingGearsCoroutine()
        {
            yield return new WaitForSeconds(gearChangeTime);
            switchingGears = false;
        }

        /// <summary>Returns the current gear as a 1-based number (for display).</summary>
        public int GetCurrentGear()
        {
            return currentGear + 1;
        }

        public void CheckGearSwitching(MonoBehaviour context)
        {
            if (switchingGears) return;

            if (rpm > maxRPM * 0.95f && currentGear < gearRatios.Length - 1)
            {
                UpGear(context);
            }
            else if (rpm < maxRPM * 0.6f && currentGear > 0)
            {
                DownGear(context);
            }
        }

        public float GetRPM() => rpm;
        public bool IsSwitchingGears() => switchingGears;

        /// <summary>Resets gear/RPM state (used when the vehicle is reset or swapped).</summary>
        public void ResetState()
        {
            currentGear = 0;
            switchingGears = false;
            rpm = idleRPM;
        }
    }

    /// <summary>
    /// Holds the physical state and tuning values for a single wheel.
    /// Every VehicleController has an array (wheels[]) of at least 4 of these.
    /// </summary>
    [Serializable]
    public class VehicleWheel
    {
        [HideInInspector] public TrailRenderer skidTrail;
        [HideInInspector] public GameObject skidTrailGameObject;

        public GameObject wheelPrefab;
        public Vector3 localPosition;
        public float turnAngle = 30f;
        public float suspensionLength = 0.5f;

        [HideInInspector] public float lastSuspensionLength = 0.0f;
        public float mass = 16f;
        public float size = 0.5f;
        public float engineTorque = 40f;
        public float brakeStrength = 0.5f;
        public bool slidding = false;

        [HideInInspector] public Vector3 worldSlipDirection;
        [HideInInspector] public Vector3 suspensionForceDirection;
        [HideInInspector] public Vector3 wheelWorldPosition;
        [HideInInspector] public float wheelCircumference;
        [HideInInspector] public float torque = 0.0f;
        [HideInInspector] public GameObject wheelObject;
        [HideInInspector] public Vector3 localVelocity;
        [HideInInspector] public float normalForce;
        [HideInInspector] public float angularVelocity;
        [HideInInspector] public float slip;
        [HideInInspector] public Vector2 input = Vector2.zero;
        [HideInInspector] public float brake = 0;
        [HideInInspector] public float slipHistory = 0f;
        [HideInInspector] public float tcsReduction = 0f; // Traction control reduction factor
        [HideInInspector] public bool isGrounded = false;
        [HideInInspector] public float suspensionCompression = 0f; // 0..1, normalized by suspensionLength
    }

    /// <summary>
    /// Main vehicle physics and driving controller. Uses raycast-based suspension
    /// (does NOT use WheelCollider — see project chat notes for why).
    ///
    /// NOTE: This controller no longer supports a VehicleData ScriptableObject.
    /// All tuning values are set directly on this component's Inspector fields.
    /// </summary>
    public class VehicleController : MonoBehaviour
    {
        [Header("Engine")]
        public VehicleEngine engine;

        [Header("Wheels")]
        public GameObject skidMarkPrefab;
        public VehicleWheel[] wheels;

        [Header("Physics Settings")]
        public float smoothTurn = 0.03f;
        private float coefStaticFriction = 1.95f;
        private float coefKineticFriction = 0.95f;
        public float wheelGripX = 8f;
        public float wheelGripZ = 42f;
        public float suspensionForce = 90f;
        public float dampAmount = 2.5f;
        public float suspensionForceClamp = 200f;
        public float downforce = 0.16f;
        public Vector3 centerOfMassOffset = new Vector3(0, -0.2f, 0);
        public float inertiaMultiplier = 1.2f;

        [Header("Driving Assists")]
        public bool steeringAssist = true;
        [Range(0f, 1f)] public float steeringAssistStrength = 0.2f;
        public bool throttleAssist = true;
        public bool brakeAssist = true;

        [Header("Stability (Anti-Rollover)")]
        [Tooltip("Anti-roll bar stiffness per axle. 0 = disabled. Realistic cars: high value relative to suspensionForce (see Setup 6 menu).")]
        public float antiRollStiffness = 0f;
        [Tooltip("Where the LATERAL grip force is applied: 0 = at the contact patch (max rollover torque), 1 = at center-of-mass height (no rollover torque). Realistic roll-center: ~0.6.")]
        [Range(0f, 1f)] public float lateralForceHeight = 0.6f;

        [HideInInspector] public Rigidbody rb;
        [HideInInspector] public bool forwards = true;
        [HideInInspector] public Vector2 userInput = Vector2.zero;
        [HideInInspector] public float isBraking = 0f; // 0-1: can be read by fuel/wear systems later

        /// <summary>Set by VehicleUpgradeApplier (Engine upgrade). Multiplies wheel torque.</summary>
        [HideInInspector] public float upgradePowerMultiplier = 1f;
        /// <summary>Set by VehicleFuel: 1 = has fuel, 0 = tank empty (engine cut).</summary>
        [HideInInspector] public float fuelPowerMultiplier = 1f;

        /// <summary>Current speed in km/h. Useful for UI and economy systems.</summary>
        public float CurrentSpeedKph => rb != null ? rb.linearVelocity.magnitude * 3.6f : 0f;

        private void Start()
        {
            rb = GetComponent<Rigidbody>();
            if (!rb) rb = gameObject.AddComponent<Rigidbody>();

            if (engine == null)
            {
                engine = new VehicleEngine();
            }

            foreach (var w in wheels)
            {
                w.wheelObject = Instantiate(w.wheelPrefab, transform);
                w.wheelObject.transform.localPosition = w.localPosition;
                w.wheelObject.transform.eulerAngles = transform.eulerAngles;
                w.wheelObject.transform.localScale = 2f * new Vector3(w.size, w.size, w.size);
                w.wheelCircumference = 2f * Mathf.PI * w.size;

                if (skidMarkPrefab != null)
                {
                    w.skidTrailGameObject = Instantiate(skidMarkPrefab, w.wheelObject.transform);
                    w.skidTrailGameObject.transform.localPosition = Vector3.zero;
                    w.skidTrailGameObject.transform.localRotation = Quaternion.identity;
                    w.skidTrailGameObject.transform.parent = null;

                    w.skidTrail = w.skidTrailGameObject.GetComponent<TrailRenderer>();
                    if (w.skidTrail != null)
                        w.skidTrail.emitting = false;
                }

                w.tcsReduction = 0f;
                w.slipHistory = 0f;
            }

            rb.centerOfMass += centerOfMassOffset;
            rb.inertiaTensor *= inertiaMultiplier;
        }

        private void Update()
        {
            userInput.x = Mathf.Lerp(userInput.x, Input.GetAxisRaw("Horizontal") / (1 + rb.linearVelocity.magnitude / 28f), 0.2f);
            userInput.y = Mathf.Lerp(userInput.y, Input.GetAxisRaw("Vertical"), 0.2f);

            bool brakingNow = Input.GetKey(KeyCode.S) && forwards;
            isBraking = brakingNow ? 1f : 0f;
            if (brakingNow) userInput.y = 0;

            for (int i = 0; i < wheels.Length; i++)
            {
                var w = wheels[i];

                if (float.IsNaN(w.slip) || float.IsInfinity(w.slip))
                    w.slip = 0f;

                if (throttleAssist)
                {
                    float targetSlip = 0.85f;
                    float slipTolerance = 0.05f;
                    if (w.slip > targetSlip + slipTolerance)
                    {
                        float overshoot = w.slip - targetSlip;
                        float reduction = Mathf.Clamp01(overshoot * 2.0f);
                        w.tcsReduction = Mathf.Lerp(w.tcsReduction, 1, reduction / 5f);
                    }
                    else if (w.slip < targetSlip - slipTolerance)
                    {
                        w.tcsReduction = Mathf.Lerp(w.tcsReduction, 0f, 0.6f * Time.deltaTime);
                    }
                    w.tcsReduction = Mathf.Clamp01(w.tcsReduction);
                }
                w.brake = (brakingNow ? 1f : 0f) * (1 - w.tcsReduction);

                float s = Mathf.Clamp01(w.slip);
                w.input.x = Mathf.Lerp(w.input.x, userInput.x, Time.deltaTime * 60f);
                if (s > 0.3f && s < 1.5f && steeringAssist) w.input.x = Mathf.Lerp(w.input.x, 0, s * Time.deltaTime * steeringAssistStrength);

                float finalThrottle = userInput.y * (1f - w.tcsReduction);
                if (float.IsNaN(finalThrottle) || float.IsInfinity(finalThrottle))
                    finalThrottle = 0f;
                w.input.y = Mathf.Lerp(w.input.y, finalThrottle, 0.95f * Time.deltaTime * 60f);
                if (float.IsNaN(w.input.y) || float.IsInfinity(w.input.y))
                    w.input.y = 0f;
            }

            if (Input.GetKeyDown(KeyCode.E)) engine.UpGear(this);
            else if (Input.GetKeyDown(KeyCode.Q)) engine.DownGear(this);

            engine.CheckGearSwitching(this);
        }

        private void FixedUpdate()
        {
            // IMPORTANT: all physics math reads rb.position / rb.rotation, NOT transform.
            // With Rigidbody interpolation enabled (needed for a smooth camera), the
            // Transform can show an interpolated RENDER pose during FixedUpdate; using
            // it for suspension rays injects positional error into every step, which
            // accumulates energy and flips the car. rb.* is always the true physics pose.
            Quaternion bodyRotation = rb.rotation;
            Vector3 bodyPosition = rb.position;
            Vector3 bodyUp = bodyRotation * Vector3.up;

            rb.AddForce(-bodyUp * rb.linearVelocity.magnitude * downforce);
            float averageWheelAngularVelocity = 0f;

            foreach (var w in wheels)
            {
                float rayLen = w.size * 2f + w.suspensionLength;
                Transform wheelObj = w.wheelObject.transform;
                Transform wheelVisual = wheelObj.GetChild(0);

                // Steering rotation computed in physics space; the wheelObj transform
                // below is only cosmetic (it lives under the interpolated parent).
                Quaternion steerRotation = bodyRotation * Quaternion.Euler(0f, w.turnAngle * w.input.x, 0f);
                Quaternion inverseSteer = Quaternion.Inverse(steerRotation);
                wheelObj.localRotation = Quaternion.Euler(0, w.turnAngle * w.input.x, 0);

                w.wheelWorldPosition = bodyPosition + bodyRotation * w.localPosition;
                Vector3 velocityAtWheel = rb.GetPointVelocity(w.wheelWorldPosition);
                w.localVelocity = inverseSteer * velocityAtWheel;
                forwards = w.localVelocity.z > 0.1f;
                w.torque = w.engineTorque * w.input.y * engine.GetCurrentPower(this)
                    * upgradePowerMultiplier * fuelPowerMultiplier;

                float inertia = w.mass * w.size * w.size / 2f;
                float lateralVel = w.localVelocity.x;

                // QueryTriggerInteraction.Ignore is CRITICAL: delivery/pickup points use big
                // trigger spheres. Without this flag the suspension ray hits the invisible
                // trigger shell, treats it as ground and catapults/flips the car on area entry.
                bool grounded = Physics.Raycast(w.wheelWorldPosition, -bodyUp, out RaycastHit hit, rayLen,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                w.isGrounded = grounded;
                Vector3 worldVelAtHit = rb.GetPointVelocity(hit.point);
                float lateralHitVel = (inverseSteer * worldVelAtHit).x;

                float lateralFriction = -wheelGripX * lateralVel - 2f * lateralHitVel;
                float longitudinalFriction = -wheelGripZ * (w.localVelocity.z - w.angularVelocity * w.size);

                w.angularVelocity += (w.torque - longitudinalFriction * w.size) / inertia * Time.fixedDeltaTime;
                w.angularVelocity *= 1 - w.brake * w.brakeStrength * Time.fixedDeltaTime;

                if (Input.GetKey(KeyCode.Space)) // Handbrake
                {
                    w.angularVelocity = 0;
                }

                Vector3 totalLocalForce = new Vector3(lateralFriction, 0f, longitudinalFriction)
                    * w.normalForce * coefStaticFriction * Time.fixedDeltaTime;
                float currentMaxFrictionForce = w.normalForce * coefStaticFriction;

                w.slidding = totalLocalForce.magnitude > currentMaxFrictionForce;
                w.slip = totalLocalForce.magnitude / currentMaxFrictionForce;
                totalLocalForce = Vector3.ClampMagnitude(totalLocalForce, currentMaxFrictionForce);
                totalLocalForce *= w.slidding ? (coefKineticFriction / coefStaticFriction) : 1;

                Vector3 totalWorldForce = steerRotation * totalLocalForce;
                w.worldSlipDirection = totalWorldForce;

                if (grounded)
                {
                    float compression = rayLen - hit.distance;
                    w.suspensionCompression = w.suspensionLength > 0f
                        ? Mathf.Clamp01(compression / w.suspensionLength)
                        : 0f;
                    float damping = (w.lastSuspensionLength - hit.distance) * dampAmount;
                    w.normalForce = (compression + damping) * suspensionForce;
                    w.normalForce = Mathf.Clamp(w.normalForce, 0f, suspensionForceClamp);

                    Vector3 springDir = hit.normal * w.normalForce;
                    w.suspensionForceDirection = springDir;

                    // Roll-center: the LATERAL grip component is applied part-way up
                    // toward center-of-mass height instead of at the contact patch.
                    // At the patch every cornering force becomes maximum rollover
                    // torque; real suspension geometry transfers it higher. Only the
                    // HEIGHT changes — the horizontal point stays at the wheel, so
                    // yaw (steering) response is unaffected.
                    Vector3 lateralWorld = steerRotation * new Vector3(totalLocalForce.x, 0f, 0f);
                    Vector3 longitudinalWorld = steerRotation * new Vector3(0f, 0f, totalLocalForce.z);
                    float comHeight = Vector3.Dot(rb.worldCenterOfMass - hit.point, bodyUp);
                    Vector3 lateralApplyPoint = hit.point + bodyUp * (Mathf.Max(0f, comHeight) * lateralForceHeight);

                    rb.AddForceAtPosition(springDir + longitudinalWorld, hit.point);
                    rb.AddForceAtPosition(lateralWorld, lateralApplyPoint);
                    w.lastSuspensionLength = hit.distance;
                    wheelObj.position = hit.point + bodyUp * w.size;

                    UpdateSkidTrail(w, wheelObj, hit);
                }
                else
                {
                    w.suspensionCompression = 0f;
                    wheelObj.position = w.wheelWorldPosition + bodyUp * (w.size - rayLen);
                    StopSkidTrail(w);
                }

                averageWheelAngularVelocity += w.angularVelocity;

                wheelVisual.Rotate(
                    Vector3.right,
                    w.angularVelocity * Mathf.Rad2Deg * Time.fixedDeltaTime,
                    Space.Self
                );
            }

            // Anti-roll bars: wheels 0-1 = front axle, 2-3 = rear axle
            // (matches the scene wheel order: z=+1.7 front pair, z=-1.7 rear pair).
            ApplyAntiRollBar(0, 1, bodyUp);
            ApplyAntiRollBar(2, 3, bodyUp);

            averageWheelAngularVelocity /= wheels.Length;
            engine.SetRPM(averageWheelAngularVelocity);
        }

        /// <summary>
        /// Standard anti-roll bar: pushes the body up on the compressed side and
        /// down on the extended side, resisting roll in corners. Produces zero force
        /// when both wheels of the axle compress equally (straight-line driving),
        /// so it only affects cornering/rollover behavior.
        /// </summary>
        private void ApplyAntiRollBar(int indexA, int indexB, Vector3 up)
        {
            if (antiRollStiffness <= 0f || wheels == null || wheels.Length <= Mathf.Max(indexA, indexB))
            {
                return;
            }

            VehicleWheel a = wheels[indexA];
            VehicleWheel b = wheels[indexB];

            float travelA = a.isGrounded ? a.suspensionCompression : 0f;
            float travelB = b.isGrounded ? b.suspensionCompression : 0f;
            float force = (travelA - travelB) * antiRollStiffness;

            if (a.isGrounded)
            {
                rb.AddForceAtPosition(up * force, a.wheelWorldPosition);
            }

            if (b.isGrounded)
            {
                rb.AddForceAtPosition(-up * force, b.wheelWorldPosition);
            }
        }

        private void UpdateSkidTrail(VehicleWheel w, Transform wheelObj, RaycastHit hit)
        {
            if (skidMarkPrefab == null) return;

            if (w.slidding)
            {
                if (w.skidTrail == null)
                {
                    GameObject skidTrailObj = Instantiate(skidMarkPrefab, transform);
                    skidTrailObj.transform.SetParent(wheelObj);
                    skidTrailObj.transform.localPosition = Vector3.zero;
                    w.skidTrail = skidTrailObj.GetComponent<TrailRenderer>();
                    w.skidTrail.time = 3f;
                    w.skidTrail.autodestruct = true;
                    w.skidTrail.emitting = true;
                    w.skidTrail.transform.position = hit.point;
                }
                else
                {
                    w.skidTrail.emitting = true;
                    w.skidTrail.transform.position = hit.point;

                    Vector3 skidDir = Vector3.ProjectOnPlane(w.worldSlipDirection.normalized, hit.normal);
                    if (skidDir.sqrMagnitude < 0.001f)
                        skidDir = Vector3.ProjectOnPlane(wheelObj.forward, hit.normal).normalized;

                    Quaternion flatRot = Quaternion.LookRotation(skidDir, hit.normal) * Quaternion.Euler(90f, 0f, 0f);
                    w.skidTrail.transform.rotation = flatRot;
                }
            }
            else if (w.skidTrail != null && w.skidTrail.emitting)
            {
                w.skidTrail.emitting = false;
                w.skidTrail.transform.parent = null;
                Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                w.skidTrail = null;
            }
        }

        private void StopSkidTrail(VehicleWheel w)
        {
            if (w.skidTrail != null && w.skidTrail.emitting)
            {
                w.skidTrail.emitting = false;
                w.skidTrail.transform.parent = null;
                Destroy(w.skidTrail.gameObject, w.skidTrail.time);
                w.skidTrail = null;
            }
        }
    }
}