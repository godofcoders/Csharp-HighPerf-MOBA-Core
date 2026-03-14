using System.Collections.Generic;
using UnityEngine;

namespace MOBA.Core.Simulation.AI
{
    public class AIBlackboard
    {
        private Dictionary<string, object> _data = new Dictionary<string, object>();

        public void Set(string key, object value) => _data[key] = value;

        public T Get<T>(string key)
        {
            if (_data.TryGetValue(key, out var value)) return (T)value;
            return default;
        }

        public bool Has(string key) => _data.ContainsKey(key);
    }
}