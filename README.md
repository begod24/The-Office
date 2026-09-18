# Office Nightmare

Co-op horror in an office that does not want you to leave. Unity 6000.3.6f1 · URP 17.3.0 ·
Netcode for GameObjects 2.13.1 · Multiplayer Services 2.3.0.

Design lives in [GDD.md](GDD.md). How the code is put together — and which rules exist because
breaking them already cost a day — lives in [Docs/Architecture.md](Docs/Architecture.md).

## Known gaps

*Checked against the code on 18 September 2026. The three items above this line in the previous
revision were all fixed and the text had not caught up — if something here reads as impossible,
check the code before believing it.*

**An enemy is silent.** It patrols its spawn circle, hears a swing, investigates, chases and
swings back, and does every bit of it without a footstep, a servo or an idle. This is the
largest hole left in what exists: a co-op horror the player cannot *hear* coming is not
testable, and GDD §2's "the building is the antagonist" has nothing to stand on until it is.
See [Docs/Architecture.md](Docs/Architecture.md) §13.

**`SCN_Level_1` is a floor plan, not a level.** No baked NavMesh and no lights, and it is in
build settings. Nothing that walks can path in it — the only baked mesh in the project is
`NavMesh_SCN_Sandbox` — so every enemy placed there logs the snap warning and stands still. Bake
it with `Office/Setup/Bake Navigation In Open Scene` and give it light before testing anything
there.

**The lobby still does not lock during a run**, so a mid-run join is allowed and gets a body
wherever the spawn points put it. `RunState.IsLocked` and `ConnectionApproval` both exist;
nothing connects them.

**Power has switches but no power.** A switch can be thrown and it can complete a run. Nothing
in the building is dark because one is off, and nothing else asks whether it is.

Remaining findings are tracked in [Docs/CodeReview.md](Docs/CodeReview.md) §8.

## What landed recently

- **Revive and spectator.** `DownedPlayer` is an `IInteractable` with a `REVIVE [countdown]`
  prompt; a standing teammate calls `Health.ServerRevive`, and `SpectatorCamera` takes over for
  a player whose own vitals ran out.
- **Enemy patrol.** An idle enemy now walks a circle around where it spawned rather than
  standing on its marker until something makes a noise.
- **The player record.** `PF_PersistentPlayer` is one per connected client and outlives the
  body: seat, display name and status survive the end of a run. Seats moved out of
  `PlayerSpawner` into `SeatRegistry`, which also fixes two players being handed the same name
  after someone in a middle seat dropped. See [Docs/Architecture.md](Docs/Architecture.md) §4.2.

After pulling any of these, run `Office/Setup/Build Persistent Player Prefab`,
`Office/Setup/Upgrade Player Prefab For Persistent Player` and `Office/Setup/Build Session
Prefab` — the record's prefab and its spawner are generated assets, and without them the session
runs with no player records at all.

Rendering is stock URP. The PS1 screen layer was removed on 20 August 2026 — the custom pass,
its shader and the builder that wired them together are gone, and nothing in the project renders
through shader code of its own any more. What is left on `PC_Renderer` is URP's own ambient
occlusion, and post-processing is still a generated URP volume profile
([Docs/Architecture.md](Docs/Architecture.md) §14).

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
