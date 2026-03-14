using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Infrastructure
{
    public class SimpleObjectPool : MonoBehaviour
    {
        [SerializeField] private GameObject _prefab;
        [SerializeField] private int _initialSize = 20;

        private Queue<GameObject> _pool = new Queue<GameObject>();

        private void Awake()
        {
            // Pre-warm the pool
            for (int i = 0; i < _initialSize; i++)
            {
                CreateNewInstance();
            }
        }

        private GameObject CreateNewInstance()
        {
            GameObject obj = Instantiate(_prefab, transform);
            obj.SetActive(false);
            _pool.Enqueue(obj);
            return obj;
        }

        public GameObject Get()
        {
            if (_pool.Count == 0) CreateNewInstance();

            GameObject obj = _pool.Dequeue();
            obj.SetActive(true);
            return obj;
        }

        public void ReturnToPool(GameObject obj)
        {
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }
    }
}