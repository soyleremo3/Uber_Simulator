using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Cinemachine'in takip edeceği ARA hedef. Araç doğrudan takip edilirse,
    /// süspansiyonun ürettiği pitch/roll (yalpalama) kameraya da yansır ve
    /// rahatsız edici bir his yaratır. Bu script sadece aracın YATAY dönüşünü
    /// (yaw) yumuşak şekilde takip eder; yukarı/aşağı yalpalamayı yok sayar.
    ///
    /// Kullanım: Cinemachine Camera'nın Follow ve Look At alanlarına, aracı
    /// değil, bu script'in bağlı olduğu objeyi ata.
    /// </summary>
    public class VehicleCameraRig : MonoBehaviour
    {
        [Tooltip("Takip edilecek araç (PlayerVehicle).")]
        public Transform target;

        [Tooltip("Dönüş takibinin yumuşaklığı. Düşük değer = daha yumuşak/gecikmeli, yüksek değer = daha hızlı tepki.")]
        public float yawSmoothSpeed = 5f;

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            // Pozisyonu birebir takip et (gecikme istemiyoruz, Cinemachine'in kendi
            // damping ayarları zaten pozisyon için yumuşatma yapacak).
            transform.position = target.position;

            // Aracın ileri yönünü al ama Y bileşenini (yukarı/aşağı eğim) sıfırla —
            // böylece süspansiyon eğimi bu hesaba hiç karışmaz.
            Vector3 flatForward = target.forward;
            flatForward.y = 0f;

            if (flatForward.sqrMagnitude < 0.0001f)
            {
                flatForward = transform.forward;
            }
            flatForward.Normalize();

            Quaternion targetYawRotation = Quaternion.LookRotation(flatForward, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetYawRotation, Time.deltaTime * yawSmoothSpeed);
        }
    }
}