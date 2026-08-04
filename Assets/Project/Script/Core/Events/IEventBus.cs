using System;

namespace Office.Core
{
    /// <summary>
    /// Cross-system notification. Technical Plan §5.3.
    ///
    /// Events are structs so publishing allocates nothing — this project has a zero
    /// allocation-per-frame budget (§8.2) and a GC spike during a scare is a ruined moment.
    ///
    /// The bus never crosses the network. A networked change is replicated first through a
    /// NetworkVariable or an RPC, and each client raises its own local bus event in response.
    ///
    /// Use C# events on the object itself for object-scoped notifications; use this only when
    /// the publisher genuinely must not know who is listening.
    /// </summary>
    public interface IEventBus
    {
        void Subscribe<T>(Action<T> handler) where T : struct;
        void Unsubscribe<T>(Action<T> handler) where T : struct;
        void Publish<T>(in T evt) where T : struct;
    }
}
