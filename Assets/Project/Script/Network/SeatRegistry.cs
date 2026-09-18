using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Office.Network
{
    /// <summary>
    /// Which seat a connected client holds, and the name that follows from it.
    ///
    /// Static and server-side because three things need the answer — the lobby roster, the
    /// persistent player and the body spawner — and each asks from its own connection callback,
    /// in whatever order those happen to be subscribed. A registry that assigns on first ask is
    /// the only version of this where that order cannot produce two different answers.
    ///
    /// The seat is the player's identity for the whole session. The display name is derived from
    /// it rather than stored, so there is one naming rule and it cannot drift.
    /// </summary>
    public static class SeatRegistry
    {
        public const int HostSeat = 0;

        private static readonly Dictionary<ulong, int> Seats = new(4);

        // Domain reload is off in this project, so statics survive exiting play mode.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Seats.Clear();

        public static int Take(ulong clientId, bool isHost)
        {
            if (Seats.TryGetValue(clientId, out var seat)) return seat;

            // The host is always seat 0 — GDD §7 puts them at the desk the run starts from, and
            // the even seats take the man prefab.
            seat = isHost ? HostSeat : HostSeat + 1;

            while (Seats.ContainsValue(seat)) seat++;

            Seats[clientId] = seat;
            return seat;
        }

        public static bool TryGet(ulong clientId, out int seat) =>
            Seats.TryGetValue(clientId, out seat);

        public static void Release(ulong clientId) => Seats.Remove(clientId);

        public static void Clear() => Seats.Clear();

        public static FixedString32Bytes NameForSeat(int seat)
        {
            var ordinal = seat + 1;

            FixedString32Bytes name = default;
            name.Append("EMPLOYEE ");
            if (ordinal < 10) name.Append('0');
            name.Append(ordinal);

            return name;
        }
    }
}
