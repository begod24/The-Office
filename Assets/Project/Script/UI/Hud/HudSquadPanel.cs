using UnityEngine;

namespace Office.UI
{
    public sealed class HudSquadPanel : MonoBehaviour
    {
        [SerializeField] private HudPlayerRow[] rows;

        public int Capacity => rows == null ? 0 : rows.Length;

        public void Bind(int index, ulong clientId, string tag, string displayName, bool isLocal)
        {
            if (!InRange(index)) return;

            rows[index].Bind(clientId, tag, displayName, isLocal);
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

        public bool SetHealth(ulong clientId, float normalised)
        {
            if (!TryFind(clientId, out var row)) return false;

            row.SetHealth(normalised);
            return true;
        }

        public bool SetDowned(ulong clientId, float bleedOutRemaining)
        {
            if (!TryFind(clientId, out var row)) return false;

            row.SetDowned(bleedOutRemaining);
            return true;
        }

        public bool SetDead(ulong clientId)
        {
            if (!TryFind(clientId, out var row)) return false;

            row.SetDead();
            return true;
        }

        public bool SetOffline(ulong clientId)
        {
            if (!TryFind(clientId, out var row)) return false;

            row.SetOffline();
            return true;
        }

        public void SetAllOffline()
        {
            if (rows == null) return;

            foreach (var row in rows)
                if (row != null && row.IsBound)
                    row.SetOffline();
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
