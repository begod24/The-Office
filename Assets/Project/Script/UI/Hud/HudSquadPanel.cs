using UnityEngine;

namespace Office.UI
{
    /// <summary>
    /// Bottom-left squad list: who is on the floor with you and how much health they have left.
    ///
    /// The panel is addressed by client id rather than by row index. Rows are reordered whenever
    /// somebody disconnects, and a health update that lands on the wrong row is worse than no
    /// health display at all — it tells a player their teammate is fine while they are bleeding
    /// out two rooms away.
    /// </summary>
    public sealed class HudSquadPanel : MonoBehaviour
    {
        [SerializeField] private HudPlayerRow[] rows;

        public int Capacity => rows == null ? 0 : rows.Length;

        public void Bind(int index, ulong clientId, string tag, bool isLocal)
        {
            if (!InRange(index)) return;

            rows[index].Bind(clientId, tag, isLocal);
        }

        /// <summary>Hides every row from <paramref name="index"/> onwards. Call after the last bind.</summary>
        public void HideFrom(int index)
        {
            if (rows == null) return;

            for (var i = Mathf.Max(0, index); i < rows.Length; i++) rows[i].Hide();
        }

        /// <summary>
        /// Fills the panel with unbound rows. Used when there is no session — the sandbox is
        /// entered directly far more often than it is entered through the lobby.
        /// </summary>
        public void ShowPlaceholders(int count)
        {
            if (rows == null) return;

            for (var i = 0; i < rows.Length; i++)
            {
                if (i < count) rows[i].ShowPlaceholder($"P{i + 1}");
                else rows[i].Hide();
            }
        }

        /// <summary>Returns false when no visible row belongs to that client, so callers can log it.</summary>
        public bool SetHealth(ulong clientId, float normalized)
        {
            if (!TryFind(clientId, out var row)) return false;

            row.SetHealth(normalized);
            return true;
        }

        public bool SetDowned(ulong clientId, bool downed)
        {
            if (!TryFind(clientId, out var row)) return false;

            row.SetDowned(downed);
            return true;
        }

        private bool TryFind(ulong clientId, out HudPlayerRow found)
        {
            found = null;
            if (rows == null) return false;

            foreach (var row in rows)
            {
                if (row == null || !row.IsBound || row.ClientId != clientId) continue;

                found = row;
                return true;
            }

            return false;
        }

        private bool InRange(int index) => rows != null && index >= 0 && index < rows.Length;
    }
}
