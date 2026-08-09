using Office.Core;
using Office.Data;
using TMPro;
using UnityEngine;

namespace Office.UI
{
    /// <summary>
    /// The terminal-boot screen that covers a load into a session.
    /// </summary>
    /// <remarks>
    /// Lives in the boot scene and survives every scene swap, because the thing it hides is
    /// the scene swap itself — a loading screen inside the scene being loaded can only appear
    /// after the wait it was meant to cover.
    /// <para>
    /// <b>Two inputs, one of them authoritative.</b> Visibility follows
    /// <see cref="GameState"/>: up on <see cref="GameState.Generating"/>, down on
    /// <see cref="GameState.InRun"/>. The bar follows
    /// <see cref="SceneLoadProgressChanged"/>, which finishes long before the run does —
    /// every client still has to report its scene ready before the session moves. So the bar
    /// is deliberately not allowed to reach the end on scene progress alone: it holds at
    /// <see cref="HandshakeCeiling"/> and creeps, which is the honest reading of "your machine
    /// is done, we are waiting for the other one".
    /// </para>
    /// </remarks>
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

        /// <summary>
        /// Where scene progress alone tops out. The rest belongs to the ready handshake.
        /// </summary>
        private const float HandshakeCeiling = 0.92f;

        /// <summary>Fraction of the remaining gap the bar creeps through per second while waiting.</summary>
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

            // Hidden immediately and without a fade: the first frame of the game must not be
            // a loading screen dissolving.
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

        // ------------------------------------------------------------------ inputs

        private void OnGameStateChanged(GameStateChanged evt)
        {
            switch (evt.Current)
            {
                // Generating is the moment the host commits to a run; FloorTransition is the
                // same wait between floors. Both are covered by the same screen.
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

            // Snapped to full rather than left where it was: the screen fades out over the
            // next quarter second and a bar frozen at 92% is the last thing it shows.
            target = 1f;
            displayed = 1f;
            ApplyProgress(1f);

            SetInteractive(false);
        }

        // ------------------------------------------------------------------ presentation

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
            // Creeps towards the ceiling even with no scene load running, so a client waiting
            // on the handshake never sees a bar that has simply stopped.
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

            // Stops on the last line rather than looping. Cycling forever reads as a hang.
            if (statusIndex >= bootLines.Length - 1) return;

            statusIndex++;
            ApplyStatus();
        }

        private void ApplyProgress(float value)
        {
            if (bar != null) bar.SetValue(value);

            if (percentLabel == null) return;

            // Only on a whole-percent change: TMP rebuilds its mesh on every assignment, and
            // this runs during the one moment the frame budget is already under pressure.
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

        // Blocks clicks reaching the menu behind it while it is up, and stops swallowing them
        // the moment it is not.
        private void SetInteractive(bool interactive)
        {
            if (group == null) return;

            group.blocksRaycasts = interactive;
            group.interactable = interactive;
        }
    }
}
