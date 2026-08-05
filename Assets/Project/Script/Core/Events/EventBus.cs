using System;
using System.Collections.Generic;
using UnityEngine;

namespace Office.Core
{
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, IHandlerList> lists = new(32);

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));

            Resolve<T>(create: true).Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            if (handler == null) return;

            Resolve<T>(create: false)?.Remove(handler);
        }

        public void Publish<T>(in T evt) where T : struct
        {
            Resolve<T>(create: false)?.Invoke(evt);
        }

        public void Clear()
        {
            foreach (var list in lists.Values) list.Clear();
            lists.Clear();
        }

        public int SubscriberCount<T>() where T : struct => Resolve<T>(create: false)?.Count ?? 0;

        private HandlerList<T> Resolve<T>(bool create) where T : struct
        {
            if (lists.TryGetValue(typeof(T), out var existing)) return (HandlerList<T>)existing;
            if (!create) return null;

            var created = new HandlerList<T>();
            lists[typeof(T)] = created;
            return created;
        }

        private interface IHandlerList
        {
            void Clear();
        }

        private sealed class HandlerList<T> : IHandlerList where T : struct
        {
            private readonly List<Action<T>> handlers = new(4);

            private readonly List<Action<T>> snapshot = new(4);

            public int Count => handlers.Count;

            public void Add(Action<T> handler)
            {
                if (handlers.Contains(handler))
                {
                    Debug.LogWarning($"[EventBus] Handler already subscribed to {typeof(T).Name}. " +
                                     "Ignored — check for a missing Unsubscribe.");
                    return;
                }

                handlers.Add(handler);
            }

            public void Remove(Action<T> handler) => handlers.Remove(handler);

            public void Invoke(in T evt)
            {
                if (handlers.Count == 0) return;

                snapshot.Clear();
                snapshot.AddRange(handlers);

                for (var i = 0; i < snapshot.Count; i++)
                {
                    try
                    {
                        snapshot[i](evt);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }

                snapshot.Clear();
            }

            public void Clear()
            {
                handlers.Clear();
                snapshot.Clear();
            }
        }
    }
}
