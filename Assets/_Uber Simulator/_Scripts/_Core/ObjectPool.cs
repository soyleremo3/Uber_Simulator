using System.Collections.Generic;
using UnityEngine;

namespace DeliverySim
{
    /// <summary>
    /// Simple GameObject pool for frequently spawned objects (particles, skid marks,
    /// order markers...). Attach one pool per prefab type. Get() activates a pooled
    /// instance or instantiates a new one; Release() deactivates and stores it.
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private int initialSize = 8;

        private readonly Queue<GameObject> pool = new Queue<GameObject>();

        private void Awake()
        {
            if (prefab == null)
            {
                Debug.LogWarning("[ObjectPool] Prefab atanmadı, havuz boş başlatılıyor.");
                return;
            }

            for (int i = 0; i < initialSize; i++)
            {
                GameObject instance = Instantiate(prefab, transform);
                instance.SetActive(false);
                pool.Enqueue(instance);
            }
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            GameObject instance = pool.Count > 0 ? pool.Dequeue() : Instantiate(prefab, transform);
            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform, false);
            pool.Enqueue(instance);
        }
    }
}
