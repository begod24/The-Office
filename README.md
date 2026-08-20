# Office Nightmare

Co-op horror in an office that does not want you to leave. Unity 6000.3.6f1 · URP 17.3.0 ·
Netcode for GameObjects 2.13.1 · Multiplayer Services 2.3.0.

Design lives in [GDD.md](GDD.md). How the code is put together — and which rules exist because
breaking them already cost a day — lives in [Docs/Architecture.md](Docs/Architecture.md).

## Known gaps

Combat lands damage, the HUD draws it, and a downed player is announced — but there is still no
way back up: `Health.ServerRevive` is waiting for something to call it, and there is no revive
interaction and no spectator camera. A downed player watches their own countdown reach zero.

The first enemy spawns from `EnemyPlacement` markers when the run starts — the sandbox carries
a test pair — and it hears: a confirmed swing publishes a server-side `NoiseRaised`, and an
enemy within both radii walks to where it came from. What an enemy still lacks is any patrol
route while idle, and any sound or animation of its own. See
[Docs/Architecture.md](Docs/Architecture.md) §13.

The lobby still does not lock during a run, so a mid-run join is allowed and gets a body
wherever the spawn points put it. Remaining findings are tracked in
[Docs/CodeReview.md](Docs/CodeReview.md) §8.

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
