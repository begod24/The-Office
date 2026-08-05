using UnityEngine;

namespace Office.Network
{
    public sealed class PlayerSpawnPoints : MonoBehaviour
    {
        [Tooltip("Ordered spawn points. Assign at least one; the transform's own position is " +
                 "used as a fallback.")]
        [SerializeField] private Transform[] points;

        private static PlayerSpawnPoints active;
        private static int cursor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            active = null;
            cursor = 0;
        }

        private void OnEnable()
        {
            active = this;
            cursor = 0;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(active, this)) active = null;
        }

        public static void Next(out Vector3 position, out float yaw)
        {
            if (active == null || active.points == null || active.points.Length == 0)
            {
                position = active != null ? active.transform.position : Vector3.zero;
                yaw = 0f;
                return;
            }

            var point = active.points[cursor % active.points.Length];
            cursor++;

            if (point == null)
            {
                position = active.transform.position;
                yaw = 0f;
                return;
            }

            position = point.position;
            yaw = point.eulerAngles.y;
        }

        private void OnDrawGizmos()
        {
            if (points == null) return;

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.8f);

            foreach (var point in points)
            {
                if (point == null) continue;

                Gizmos.DrawWireSphere(point.position + Vector3.up * 0.9f, 0.32f);
                Gizmos.DrawLine(point.position, point.position + point.forward * 1.2f);
            }
        }
    }
}
