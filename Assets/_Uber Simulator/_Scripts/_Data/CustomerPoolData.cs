using System;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Name + type pool from which a per-offer <see cref="CustomerInstance"/> is
    /// composed at runtime (order-board redesign, spec D). ~60 first names × ~40 last
    /// names + ~30 businesses = thousands of customers from ~130 authored strings,
    /// so orders vary without hand-authoring hundreds of assets.
    /// </summary>
    [CreateAssetMenu(fileName = "CustomerPool", menuName = "DeliverySim/Customer Pool")]
    public class CustomerPoolData : ScriptableObject
    {
        [Serializable]
        public class BusinessEntry
        {
            public string name;
            public CustomerType type = CustomerType.Shop;
        }

        [Header("Individuals")]
        [SerializeField] private string[] firstNames =
        {
            "Ayşe", "Mehmet", "Deniz", "Elif", "Can", "Zeynep", "Emir", "Defne", "Yusuf", "Ecrin",
            "Mert", "Nehir", "Kerem", "Azra", "Ali", "Miray", "Ömer", "Asya", "Efe", "Ada",
            "Burak", "Selin", "Kaan", "Ela", "Arda", "Nil", "Berk", "İpek", "Poyraz", "Duru",
            "Baran", "Cansu", "Onur", "Melis", "Tuna", "Sude", "Eren", "Yaren", "Bora", "Lara"
        };

        [SerializeField] private string[] lastNames =
        {
            "Yılmaz", "Kaya", "Demir", "Çelik", "Şahin", "Yıldız", "Yıldırım", "Öztürk", "Aydın", "Özdemir",
            "Arslan", "Doğan", "Kılıç", "Aslan", "Çetin", "Kara", "Koç", "Kurt", "Özkan", "Şimşek",
            "Polat", "Korkmaz", "Çakır", "Erdoğan", "Yavuz", "Güneş", "Aksoy", "Bulut", "Taş", "Acar"
        };

        [Header("Businesses (senders)")]
        [SerializeField] private BusinessEntry[] businesses =
        {
            new BusinessEntry { name = "Kardelen Çiçekçilik", type = CustomerType.Shop },
            new BusinessEntry { name = "Lezzet Durağı", type = CustomerType.Restaurant },
            new BusinessEntry { name = "Anadolu Kitabevi", type = CustomerType.Shop },
            new BusinessEntry { name = "Merkez Eczanesi", type = CustomerType.Clinic },
            new BusinessEntry { name = "Öztürk Kuyumculuk", type = CustomerType.Shop },
            new BusinessEntry { name = "Gündoğdu Market", type = CustomerType.Shop },
            new BusinessEntry { name = "Nar Cafe & Bistro", type = CustomerType.Restaurant },
            new BusinessEntry { name = "Yıldız Teknoloji", type = CustomerType.Corporate },
            new BusinessEntry { name = "Papatya Pastanesi", type = CustomerType.Restaurant },
            new BusinessEntry { name = "Deniz Butik", type = CustomerType.Shop },
            new BusinessEntry { name = "Aile Sağlık Merkezi", type = CustomerType.Clinic },
            new BusinessEntry { name = "Kervan Lojistik", type = CustomerType.Corporate },
            new BusinessEntry { name = "Sofra Ev Yemekleri", type = CustomerType.Restaurant },
            new BusinessEntry { name = "Bereket Fırını", type = CustomerType.Shop },
            new BusinessEntry { name = "Mavi Ofis Kırtasiye", type = CustomerType.Corporate }
        };

        public bool HasContent => firstNames != null && firstNames.Length > 0 &&
                                  lastNames != null && lastNames.Length > 0;

        /// <summary>Builds a random individual recipient customer.</summary>
        public CustomerInstance RollIndividual()
        {
            string first = Pick(firstNames, "Müşteri");
            string last = Pick(lastNames, "");
            string display = string.IsNullOrEmpty(last) ? first : $"{first} {last[0]}.";
            return Make(display, CustomerType.Individual);
        }

        /// <summary>Builds a random business sender/customer.</summary>
        public CustomerInstance RollBusiness()
        {
            if (businesses == null || businesses.Length == 0)
            {
                return RollIndividual();
            }

            BusinessEntry entry = businesses[UnityEngine.Random.Range(0, businesses.Length)];
            return Make(string.IsNullOrWhiteSpace(entry.name) ? "İşletme" : entry.name, entry.type);
        }

        private static CustomerInstance Make(string display, CustomerType type)
        {
            return new CustomerInstance
            {
                DisplayName = display,
                Type = type,
                CustomerId = display.GetHashCode().ToString("X8")
            };
        }

        private static string Pick(string[] source, string fallback)
        {
            if (source == null || source.Length == 0)
            {
                return fallback;
            }

            return source[UnityEngine.Random.Range(0, source.Length)];
        }
    }
}
