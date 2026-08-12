# Office Nightmare

Co-op horror in an office that does not want you to leave. Unity 6000.3.6f1 · URP 17.3.0 ·
Netcode for GameObjects 2.13.1 · Multiplayer Services 2.3.0.

Design lives in [GDD.md](GDD.md). How the code is put together — and which rules exist because
breaking them already cost a day — lives in [Docs/Architecture.md](Docs/Architecture.md).

## Known gaps

Combat lands damage and downs a player, but nothing yet turns being downed into a visible state:
there is no revive interaction, no spectator camera and no health readout. `Health` publishes
`LocalVitalsChanged` for whatever draws it first, and `Health.ServerRevive` is waiting for
something to call it.

The first enemy exists as a definition, a carrier prefab and a server-side brain, and **nothing
spawns one** — there is no `EnemyPlacement` marker and no `EnemySpawner`. It also cannot hear:
`EnemyDefinition.HearingRadius` and every weapon's `NoiseRadius` are both authored and nothing
publishes a noise between them. See [Docs/Architecture.md](Docs/Architecture.md) §13.

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
