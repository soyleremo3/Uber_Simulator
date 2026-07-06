using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Oyuncunun raycast ile etkileşime girebileceği tüm objeler bu arayüzü uygular.
    /// Forgotten Island projesindeki Interaction System ile aynı mantığı izler.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Etkileşim tetiklendiğinde çağrılır (ör. oyuncu E tuşuna bastığında).</summary>
        void Interact(GameObject interactor);

        /// <summary>Ekranda gösterilecek etkileşim ipucu metni (ör. "Paketi Al [E]").</summary>
        string GetInteractionPrompt();
    }
}