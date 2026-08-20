using UnityEngine;

namespace Office.Data
{
    public abstract class ContentDefinition : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable network id. Assigned by 'Office/Content/Rebuild Definition Registry' — " +
                 "never edit it by hand, and never reuse the id of a deleted definition.")]
        [SerializeField] private int id;

        [Tooltip("Shown in the HUD. Keep it short and upper case — the terminal font has no lower case.")]
        [SerializeField] private string displayName = "UNNAMED";

        [Header("Presentation")]
        [Tooltip("Plain prefab: mesh, collider, nothing networked. It is instantiated as a child " +
                 "of the networked carrier, which is why adding content never touches the " +
                 "network prefab registry.")]
        [SerializeField] private GameObject viewPrefab;

        [Tooltip("Hotbar icon. Optional — an empty slot draws the frame and the count only.")]
        [SerializeField] private Sprite icon;

        public const int NoId = 0;

        public int Id => id;

        public string DisplayName => displayName;

        public GameObject ViewPrefab => viewPrefab;

        public Sprite Icon => icon;

        public bool HasValidId => id != NoId;
    }
}
