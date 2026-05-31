using System;
using System.Collections.Generic;

namespace MOBA.Core.Infrastructure
{
    public static class ServiceProvider
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public static void Register<T>(T service)
        {
            var type = typeof(T);
            if (!_services.ContainsKey(type))
            {
                _services.Add(type, service);
            }
            else
            {
                _services[type] = service;
            }
        }

        public static T Get<T>()
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var service))
            {
                return (T)service;
            }
            throw new Exception($"Service of type {type} not registered.");
        }

        public static bool TryGet<T>(out T service)
        {
            var type = typeof(T);
            if (_services.TryGetValue(type, out var registeredService))
            {
                service = (T)registeredService;
                return true;
            }

            service = default;
            return false;
        }

        public static void Unregister<T>(T service)
        {
            var type = typeof(T);
            if (!_services.TryGetValue(type, out var registeredService))
                return;

            if (ReferenceEquals(registeredService, service))
                _services.Remove(type);
        }

        public static void Unregister<T>()
        {
            _services.Remove(typeof(T));
        }

        public static void Clear() => _services.Clear();
    }
}
