using System;
using System.Collections.Generic;
using UnityEngine;

namespace Office.Core
{
    /// <inheritdoc cref="IEventBus"/>
    /// <remarks>
    /// Handlers are kept in a <see cref="List{T}"/> per event type rather than in one multicast
    /// delegate. A multicast delegate stops invoking the moment a handler throws, so a single
    /// broken HUD element would silently stop lights and doors from ever hearing about a power
    /// change. Iterating a list lets each handler fail on its own.
    ///
    /// Publishing allocates nothing once the buffers have grown: the snapshot list is reused,
    /// and indexing a <see cref="List{T}"/> does not allocate an enumerator. That matters —
    /// Technical Plan §8.2 budgets zero GC allocation per frame, and this bus sits on the power,
    /// damage and objective paths.
    /// </remarks>
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

        /// <summary>Drops every subscription. Called by the composition root on teardown.</summary>
        public void Clear()
        {
            foreach (var list in lists.Values) list.Clear();
            lists.Clear();
        }

        /// <summary>Subscriber count for a given event type. Test and diagnostics only.</summary>
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

            // Handlers are allowed to subscribe or unsubscribe while being notified — an enemy
            // dying inside a damage handler is exactly that. Invoking from a reused snapshot
            // keeps the iteration valid without allocating one per publish.
            private readonly List<Action<T>> snapshot = new(4);

            public int Count => handlers.Count;

            public void Add(Action<T> handler)
            {
                if (handlers.Contains(handler))
                {
                    // Almost always a missing Unsubscribe in OnDisable or a pooled object that
                    // subscribed twice. Silently allowing it makes the handler run twice per
                    // event, which reads as a damage or audio bug far from its cause.
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
