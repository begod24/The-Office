using System;
using System.Collections.Generic;
using UnityEngine;

namespace Office.Core
{
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> Services = new(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Services.Clear();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));

            if (Services.ContainsKey(typeof(T)))
                Debug.LogWarning($"[ServiceLocator] {typeof(T).Name} is being replaced. " +
                                 "Two composition roots in the same session is almost always a bug.");

            Services[typeof(T)] = service;
        }

        public static void Unregister<T>() where T : class => Services.Remove(typeof(T));

        public static bool IsRegistered<T>() where T : class => Services.ContainsKey(typeof(T));

        public static bool TryGet<T>(out T service) where T : class
        {
            if (Services.TryGetValue(typeof(T), out var found))
            {
                service = (T)found;
                return true;
            }

            service = null;
            return false;
        }

        public static T Get<T>() where T : class
        {
            if (Services.TryGetValue(typeof(T), out var found)) return (T)found;

            throw new InvalidOperationException(
                $"[ServiceLocator] {typeof(T).Name} is not registered. " +
                "Either the Boot scene was not loaded first, or an installer did not run.");
        }

        public static void Clear() => Services.Clear();
    }
}
