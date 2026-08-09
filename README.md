# Office Nightmare

Co-op horror in an office that does not want you to leave. Unity 6000.3.6f1 · URP 17.3.0 ·
Netcode for GameObjects 2.13.1 · Multiplayer Services 2.3.0.

Design lives in [GDD.md](GDD.md). How the code is put together — and which rules exist because
breaking them already cost a day — lives in [Docs/Architecture.md](Docs/Architecture.md).

## First run

Clone with Git LFS available (`git lfs install` once per machine), open the project, then:

1. `Office/Setup/Import TextMeshPro Essentials` — one-time. The lobby and menu render nothing
   without it, and the failure looks like missing UI rather than a missing package.
2. `Office/Content/Build All (samples, carrier, registry)` — item definitions, `PF_WorldItem`,
   `REG_Definitions`.
3. `Office/Setup/Run All (physics, configs, prefab, scenes)` — collision matrix, config assets,
   prefabs, the generated scenes, build settings.

`Run All` already calls `Build All` in the right order, so step 3 alone is enough on a fresh
clone. Run them separately when you only touched content.

**Enter play mode from `SCN_Boot` only.** It is build index 0, it holds the composition root,
and it owns the single `EventSystem`. Pressing Play inside any other scene gives you a screen
with dead mouse input and no services — that is the rule working, not a bug.

## Two clients on one machine

The project ships with `com.unity.multiplayer.playmode`. Use **Window → Multiplayer → Multiplayer
Play Mode**, enable one virtual player, and let it finish its first domain reload before testing.
The main editor and the virtual player are separate processes, so the host is whichever one you
press Host in.

Nothing networked is verified until it has run on a **second** client. The host has every object
locally and cannot see a spawn that failed to resolve remotely — that class of bug shows up only
on the joining side.

## Manual two-client checklist

Run this after touching spawning, inventory, scene flow or the session phase. F1 toggles the
debug session panel in any scene; it shows connection phase, roster, ready state and role.

| # | Step | Passes when |
|---|---|---|
| 1 | Client A: Play from `SCN_Boot`, Host | Main menu → lobby, F1 shows `role: host` |
| 2 | Copy the join code from the lobby | Code is non-empty |
| 3 | Client B: Play, Join with that code | Both clients show 2 players in the roster |
| 4 | Both press Ready | `ready: True` on both |
| 5 | Host presses Start | Both load the run scene and spawn a body each |
| 6 | Each client walks and looks | Movement of the other player is smooth, not teleporting |
| 7 | Client B picks an item up | It leaves the world and appears in B's hand **on both screens** |
| 8 | Client B drops it | It lands in the same place on both screens |
| 9 | Both pick up and drop the same item several times | No hitch on later pickups — the carrier is pooled after the first |
| 10 | Host presses End run, then starts another | Items are laid out again; no leftovers from the previous run |
| 11 | Host presses End run | Both return to the lobby with the roster intact |
| 12 | Start a run, then join with a third client | The late joiner gets a body instead of a camera stuck in the floor |
| 13 | Kill the host process mid-run | Every other client lands back in the main menu with a reason logged, not stranded in the run scene |

Step 7 is the one that catches registry and prefab-resolution mistakes. If the item vanishes for
B but stays for the host, check the console on B for `NetworkPrefab could not be found`.

Steps 9 and 10 exercise the object pool, which is the newest thing here and the least verified:
it registers a prefab handler on both ends, and a mistake there shows up only on the client.

## Known gaps

Combat lands damage and downs a player, but nothing yet turns being downed into a visible state:
there is no revive interaction, no spectator camera and no health readout. `Health` publishes
`LocalVitalsChanged` for whatever draws it first, and `Health.ServerRevive` is waiting for
something to call it.

The lobby still does not lock during a run, so a mid-run join is allowed and gets a body
wherever the spawn points put it. Remaining findings are tracked in
[Docs/CodeReview.md](Docs/CodeReview.md) §8.

**Two players on different builds cannot connect** — that is deliberate. The handshake compares
`Application.version` and a fingerprint of `REG_Definitions`, so after changing content both
machines need the same build. A rejected join logs what each side expected.

## Working in this repo

Scenes and prefabs are YAML that does not merge. Anything that can be generated from code is —
see [Docs/Architecture.md](Docs/Architecture.md) §7.1 for which assets are owned by a builder and
which are owned by a person. Editing a generated scene by hand loses the edit on the next
regeneration, without a warning.

Authored levels go in `Assets/Project/Scenes/Levels/`. Build settings pick that folder up
automatically, so adding a level never means editing a builder.
