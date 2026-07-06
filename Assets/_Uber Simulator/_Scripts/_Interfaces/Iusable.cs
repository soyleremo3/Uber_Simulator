using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Yük alma/bırakma gibi "kullanım" eylemlerini destekleyen objeler bu arayüzü uygular.
    /// IInteractable'dan farkı: bu, bir ön koşula bağlı olabilen (CanUse) aktif bir eylemi temsil eder.
    /// Örnek: bir teslimat noktası, sadece oyuncu doğru paketi taşıyorsa "kullanılabilir" olur.
    /// </summary>
    public interface IUsable
    {
        /// <summary>Bu obje şu anda kullanıcı tarafından kullanılabilir mi?</summary>
        bool CanUse(GameObject user);

        /// <summary>Kullanım eylemini gerçekleştirir (ör. yükü teslim noktasına bırakma).</summary>
        void Use(GameObject user);
    }
}