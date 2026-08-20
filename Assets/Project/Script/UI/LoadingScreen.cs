using Office.Core;
using Office.Data;
using TMPro;
using UnityEngine;

namespace Office.UI
{
    [DisallowMultipleComponent]
    public sealed class LoadingScreen : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private HudSegmentBar bar;
        [SerializeField] private TMP_Text percentLabel;
        [SerializeField] private TMP_Text statusLabel;

        [Header("Feel")]
        [Tooltip("Seconds the panel takes to fade in and out. A hard cut reads as a stutter.")]
        [Min(0f)]
        [SerializeField] private float fadeSeconds = 0.25f;

        [Tooltip("How fast the bar chases its target. Scene loads arrive in jumps; the bar " +
                 "should not.")]
        [Min(0.1f)]
        [SerializeField] private float fillSpeed = 1.6f;

        [Tooltip("Seconds between status line changes while waiting on the other player.")]
        [Min(0.1f)]
        [SerializeField] private float statusInterval = 1.1f;

        [Header("Copy")]
        [SerializeField] private string[] bootLines =
        {
            "SYSTEM BOOT...",
            "MOUNTING FLOOR...",
            "LINKING TERMINALS...",
            "AWAITING PERSONNEL..."
        };

        private const float HandshakeCeiling = 0.92f;

        private const float CreepRate = 0.15f;

        private IEventBus bus;

        private bool shown;
        private float alpha;
        private float target;
        private float displayed;
        private float statusTimer;
        private int statusIndex;
        private int shownPercent = -1;

        private void Awake()
        {
            if (group == null) group = GetComponent<CanvasGroup>();

            alpha = 0f;
            ApplyAlpha();
            SetInteractive(false);
        }

        private void OnEnable()
        {
            if (!ServiceLocator.TryGet(out bus)) return;

            bus.Subscribe<GameStateChanged>(OnGameStateChanged);
            bus.Subscribe<SceneLoadProgressChanged>(OnSceneProgress);
        }

        private void OnDisable()
        {
            bus?.Unsubscribe<GameStateChanged>(OnGameStateChanged);
            bus?.Unsubscribe<SceneLoadProgressChanged>(OnSceneProgress);
            bus = null;
        }

        private void OnGameStateChanged(GameStateChanged evt)
        {
            switch (evt.Current)
            {
                case GameState.Generating:
                case GameState.FloorTransition:
                    Show();
                    break;

                default:
                    Hide();
                    break;
            }
        }

        private void OnSceneProgress(SceneLoadProgressChanged evt)
        {
            if (!shown) return;

            target = Mathf.Max(target, Mathf.Min(evt.Progress, 1f) * HandshakeCeiling);
        }

        private void Show()
        {
            if (shown) return;

            shown = true;
            target = 0f;
            displayed = 0f;
            shownPercent = -1;
            statusIndex = 0;
            statusTimer = 0f;

            SetInteractive(true);
            ApplyProgress(0f);
            ApplyStatus();
        }

        private void Hide()
        {
            if (!shown) return;

            shown = false;

            target = 1f;
            displayed = 1f;
            ApplyProgress(1f);

            SetInteractive(false);
        }

        private void Update()
        {
            var wantedAlpha = shown ? 1f : 0f;

            if (!Mathf.Approximately(alpha, wantedAlpha))
            {
                alpha = fadeSeconds <= 0f
                    ? wantedAlpha
                    : Mathf.MoveTowards(alpha, wantedAlpha, Time.unscaledDeltaTime / fadeSeconds);

                ApplyAlpha();
            }

            if (!shown) return;

            AdvanceBar();
            AdvanceStatus();
        }

        private void AdvanceBar()
        {
            if (target < HandshakeCeiling)
                target = Mathf.MoveTowards(target, HandshakeCeiling,
                    (HandshakeCeiling - target) * CreepRate * Time.unscaledDeltaTime);

            if (Mathf.Approximately(displayed, target)) return;

            displayed = Mathf.MoveTowards(displayed, target, fillSpeed * Time.unscaledDeltaTime);
            ApplyProgress(displayed);
        }

        private void AdvanceStatus()
        {
            if (bootLines == null || bootLines.Length <= 1) return;

            statusTimer += Time.unscaledDeltaTime;
            if (statusTimer < statusInterval) return;

            statusTimer = 0f;

            if (statusIndex >= bootLines.Length - 1) return;

            statusIndex++;
            ApplyStatus();
        }

        private void ApplyProgress(float value)
        {
            if (bar != null) bar.SetValue(value);

            if (percentLabel == null) return;

            var percent = Mathf.RoundToInt(value * 100f);
            if (percent == shownPercent) return;

            shownPercent = percent;
            percentLabel.text = $"{percent}%";
        }

        private void ApplyStatus()
        {
            if (statusLabel == null || bootLines == null || bootLines.Length == 0) return;

            statusLabel.text = bootLines[Mathf.Clamp(statusIndex, 0, bootLines.Length - 1)];
        }

        private void ApplyAlpha()
        {
            if (group != null) group.alpha = alpha;
        }

        private void SetInteractive(bool interactive)
        {
            if (group == null) return;

            group.blocksRaycasts = interactive;
            group.interactable = interactive;
        }
    }
}
