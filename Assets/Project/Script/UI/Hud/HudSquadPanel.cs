using UnityEngine;

namespace Office.UI
{
    public sealed class HudSquadPanel : MonoBehaviour
    {
        [SerializeField] private HudPlayerRow[] rows;

        public int Capacity => rows == null ? 0 : rows.Length;

        public void Bind(int index, ulong clientId, string tag, bool isLocal)
        {
            if (!InRange(index)) return;

            rows[index].Bind(clientId, tag, isLocal);
        }

        public void HideFrom(int index)
        {
            if (rows == null) return;

            for (var i = Mathf.Max(0, index); i < rows.Length; i++) rows[i].Hide();
        }

        public void ShowPlaceholders(int count)
        {
            if (rows == null) return;

            for (var i = 0; i < rows.Length; i++)
            {
                if (i < count) rows[i].ShowPlaceholder($"P{i + 1}");
                else rows[i].Hide();
            }
        }

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
