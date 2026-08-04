# Office Nightmare — MVP Development Planner

**Target:** the vertical slice defined in `GDD.md` §16
**Engine:** Unity 6000.3.6f1, URP, NGO
**Team:** A — systems and gameplay code. B — level design and 3D art.
**Companion documents:** `GDD.md`, `TECHNICAL_PLAN.md`, `CONTRIBUTING.md`

---

## 0. Planning Assumptions

These drive every number below. If they are wrong, rescale the whole plan rather than compressing tasks.

| Assumption | Value |
|---|---|
| Availability per person | ~15 hours per week |
| Sprint length | 2 weeks (~30 hours per person per sprint) |
| Total plan | 12 sprints ≈ 24 weeks ≈ 6 months |
| Total budget | ~360 hours each, ~720 hours combined |
| Ramp-up factor | Estimates assume this is the first networked project for the team. A 1.3–1.5× overrun on netcode and generation sprints is normal, not a failure. |

**If availability is lower**, do not cut tasks — extend the calendar. The task list is already the minimum. The only legitimate way to shrink this plan is the cut list in §15.

---

## 1. What "MVP Done" Means

The MVP is complete when two players can, in a single unbroken session:

1. Launch the game, one hosts, one joins
2. Hear each other positionally as they move apart
3. Explore a procedurally assembled office floor
4. Find and use four improvised weapons
5. Fight three enemy types, learning that one of them cannot be hit with physical weapons
6. Complete the Restore Power objective, which requires both of them
7. Reach the elevator and escape
8. Or die, and hear the survivor's panic through a monitor while spectating
9. See a results screen and start another run with a different floor layout

Running at 60 FPS in a standalone build, with no desync, no softlocks, and no frame-time spikes during combat.

**Nothing else is in the MVP.** No bosses, no crafting, no progression, no additional floors, no menus beyond what is needed to start and stop.

---

## 2. Track Structure

Two parallel tracks that must not block each other.

| Track | Owner | Rule |
|---|---|---|
| **Systems** | A | `main` is always playable. Never push a broken build. |
| **Content** | B | Never blocked waiting for code. Grey-box first, art second. |

**The single most important scheduling rule:** B must never be idle waiting for A. This means grey-box geometry ships in Sprint 0, before any gameplay exists, so A always has a space to test in and B always has the next room to build.

---

## 3. Sprint 0 — Foundations and Contracts (Weeks 1–2)

The goal is not gameplay. The goal is that nothing later has to be redone.

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-01 | Project config: URP asset, Input System, quality settings, physics layers | 3h | Layers and input map documented in `Docs/` |
| A-02 | Folder structure and all 10 assembly definitions per Technical Plan §3.2 | 3h | Project compiles, no circular references |
| A-03 | `Boot` scene, `GameBootstrap`, `ServiceLocator`, `IEventBus` | 5h | Services registered and retrievable, cleared on destroy |
| A-04 | `GameState` enum and state machine skeleton | 3h | Transitions log correctly, no gameplay attached |
| A-05 | `RunState` structure skeleton (Technical Plan §2.7.2) | 3h | Serialises and deserialises to JSON in an EditMode test |
| A-06 | `.editorconfig`, coding standards written into `CONTRIBUTING.md` | 2h | Both machines format identically |
| A-07 | Additive scene loading: `Boot` → `Lobby` → `Floor` | 4h | Scenes load and unload without leaks |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-01 | Co-author the room connector contract with A | 3h | Written in `Docs/KitContract.md`, frozen |
| B-02 | Blender scene template: 2 m grid, 0.25 m sub-grid, unit scale, export preset | 4h | One test cube round-trips to Unity at exactly 2 m |
| B-03 | Grey-box kit v0: floor, ceiling, wall, wall-with-doorway, corner, door | 8h | Ten pieces snap on the grid with zero gaps |
| B-04 | Grey-box test room hand-assembled from the kit | 3h | Loads as a prefab, walkable, no z-fighting |
| B-05 | `_Source/` folder structure and naming convention document | 2h | Written down, both agree |

### Gate 0 — do not proceed until all are true

- [ ] Project compiles with all assemblies separated
- [ ] `Boot` scene loads and registers services
- [ ] A grey-box room built from the kit is walkable in the editor
- [ ] Both developers can pull, edit different files, and push with no conflicts
- [ ] `Docs/KitContract.md` exists and is signed off by both

**The connector contract is the hard blocker.** B must not build room number two until it is frozen. Changing it after twelve roods exist costs weeks.

---

## 4. Sprint 1 — Networking Core (Weeks 3–4)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-08 | NGO installed, `NetworkManager` in `Boot`, prefab registry | 4h | Host starts without errors |
| A-09 | Unity Transport + Relay: host, join by code | 6h | Two machines connect over the internet |
| A-10 | Multiplayer Play Mode configured for 2 virtual players | 3h | Two players testable from one editor |
| A-11 | `Lobby` scene: player list, ready state, start run | 5h | Both players see each other's ready state |
| A-12 | Networked player prefab, spawn, despawn | 4h | Both clients see both bodies |
| A-13 | Replicated movement and look, client-authoritative | 8h | Movement is smooth on the remote client, no rubber-banding |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-06 | Kit expansion to ~20 grey-box pieces: T-junction, dead end, wide corridor, stairwell, elevator alcove | 10h | All snap on grid, all carry `RoomConnector` sockets |
| B-07 | Three hand-built rooms: open space, corridor segment, server room | 9h | Room prefabs, correct sockets, no lighting yet |
| B-08 | Reference board for the PS1 look: 20 images, palette swatches | 4h | Shared in `Docs/Art/` |

### Gate 1

- [ ] Two players connect over Relay from two machines
- [ ] Both walk around a hand-assembled grey-box floor and see each other move
- [ ] Multiplayer Play Mode runs two virtual players locally
- [ ] Disconnect does not crash either client

---

## 5. Sprint 2 — Player Systems (Weeks 5–6)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-14 | `PlayerMovement`: walk, sprint, crouch, stamina drain and recovery | 8h | Stamina replicated, sprint feels heavy not floaty |
| A-15 | `PlayerLook`: camera, sensitivity, view bob, head position | 4h | No jitter on remote clients |
| A-16 | `PlayerInteraction`: raycast, `IInteractable`, prompt UI | 6h | Prompt appears and disappears correctly at range |
| A-17 | `InteractCommand` server validation: distance and line of sight | 4h | Client cannot interact through a wall |
| A-18 | `PlayerFlashlight`: toggle, battery drain, light cookie | 5h | Battery replicated, light visible to all clients |
| A-19 | Test interactable: a door that opens for both players | 3h | Server-authoritative, both see the same state |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-09 | First art pass on the kit: walls, floor, ceiling with fluorescent fixture, doors | 14h | Within triangle and texture budget from GDD §12.2 |
| B-10 | Five props: desk, office chair, monitor, filing cabinet, water cooler | 8h | Modular, reusable, correct pivots |
| B-11 | Texture atlas strategy decided and documented | 3h | One atlas covers the kit |

### Gate 2

- [ ] Player walks, sprints, crouches, and runs out of stamina
- [ ] Flashlight works and drains, visible to the other client
- [ ] Both players can open the same door with no state conflict
- [ ] The floor is recognisably an office, not grey boxes

---

## 6. Sprint 3 — Items and Inventory (Weeks 7–8)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-20 | `PoolService`, `IPoolable`, `ResetState()` contract | 5h | EditMode test proves state resets on release |
| A-21 | `VfxPool` and `AudioSourcePool` | 4h | Zero instantiation during play in the profiler |
| A-22 | `ItemDefinition` ScriptableObject | 3h | Four item assets authored |
| A-23 | `PlayerInventory`: 4 slots, select, swap | 6h | Selection replicated, HUD updates |
| A-24 | Pickup and drop, server-validated | 8h | Two players cannot pick up the same item |
| A-25 | Held item visual sync, first-person and third-person | 5h | Correct item visible in both views |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-12 | Four weapon models: mug, fire extinguisher, staple gun, laser pointer | 12h | First-person and world versions, within budget |
| B-13 | First-person hand model and hold poses | 8h | One rig, four hold poses |
| B-14 | Battery, first aid kit, keycard pickup props | 4h | Consistent silhouette language for pickups |

### Gate 3

- [ ] Both players pick up, hold, swap, and drop items
- [ ] Each sees the correct item in the other's hands
- [ ] No item duplication under simultaneous pickup
- [ ] Profiler shows zero allocations during pickup and drop

---

## 7. Sprint 4 — Damage and Combat (Weeks 9–10)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-26 | `IDamageable`, `DamageInfo`, `DamageType` flags | 4h | EditMode tests cover the vulnerability matrix |
| A-27 | `Health` component, server-authoritative | 4h | Cannot be written from a client |
| A-28 | Damage resolver: vulnerabilities, immunities, multipliers | 5h | Immune target takes exactly zero |
| A-29 | `IWeaponBehaviour` + `MeleeSwingBehaviour` | 7h | Mug and extinguisher both work from one behaviour |
| A-30 | `ProjectileBehaviour` with pooled projectiles | 7h | Staple gun fires, projectiles return to pool |
| A-31 | Weapon durability and breaking | 4h | Weapon breaks, is removed, HUD reflects it |
| A-32 | Hit feedback: VFX, SFX, hit-stop, damage numbers off by default | 5h | Feels like contact, not like a raycast |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-15 | Stapler enemy model and animations: idle, walk, attack, death | 12h | Under 2000 tris, readable silhouette in the dark |
| B-16 | Printer enemy model and animations | 12h | Distinct silhouette from the Stapler at 10 m |
| B-17 | Impact VFX sheets: sparks, paper, dust | 5h | Pooled-friendly particle systems |

### Gate 4

- [ ] A player kills a test dummy with each of the four weapons
- [ ] The damage type matrix produces correct results including immunity
- [ ] Weapons break and are removed
- [ ] Combat has weight — swing, connect, react

---

## 8. Sprint 5 — Enemies (Weeks 11–12)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-33 | `EnemyStateMachine` and `EnemyContext` | 6h | States swappable, no nested booleans anywhere |
| A-34 | States: Idle, Patrol, Investigate, Chase, Attack, Stunned, Dead | 10h | Transitions visible in a debug overlay |
| A-35 | Perception: sight cone, hearing radius, light sensitivity | 8h | Player sneaking in darkness is genuinely stealthier |
| A-36 | `EnemyDefinition` SO, NavMeshAgent integration | 5h | All tuning outside prefabs |
| A-37 | Stapler behaviour: fast swarm melee | 5h | Five of them are threatening, not comical |
| A-38 | Printer behaviour: ranged, seeks cover | 6h | Uses the room, does not stand in the open |
| A-39 | Networked enemy pooling via `INetworkPrefabInstanceHandler` | 6h | No instantiate spikes when a wave spawns |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-18 | Six more rooms toward the twelve-room target | 15h | Built to the frozen contract |
| B-19 | Enemy audio sourcing: approach, attack, death for both enemies | 6h | Each identifiable by ear with eyes closed |
| B-20 | Glitch visual concept and particle prototype | 6h | Reads as digital, not as smoke |

### Gate 5

- [ ] Two players fight five Staplers and one Printer with no desync
- [ ] Enemies path correctly through a hand-built floor
- [ ] Enemy spawns cause no frame-time spike
- [ ] A player can identify an approaching enemy by sound alone

---

## 9. Sprint 6 — Level Generation (Weeks 13–14)

The highest-risk sprint. Budget the whole sprint for it and nothing else.

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-40 | `RoomConnector` component, socket matching | 6h | Sockets align with zero gaps in every combination |
| A-41 | Seeded graph generator, own `System.Random` instance | 12h | No `UnityEngine.Random` anywhere in the assembly |
| A-42 | **Determinism test** | 4h | Same seed produces identical graph 100 times |
| A-43 | Seed replication before floor load | 3h | Both clients build before any player spawns |
| A-44 | Required-room guarantees and reachability validation | 6h | Generator never produces an unwinnable floor in 1000 runs |
| A-45 | Wall caps for unused connectors | 3h | No holes to the void |
| A-46 | Runtime NavMesh baking after generation | 5h | Under 2 s, hidden by the transition screen |
| A-47 | Spawn point system: loot, enemies, emitters | 5h | Points authored in room prefabs, consumed by the generator |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-21 | Final six rooms to reach twelve | 15h | Each has sockets, spawn points, and light probe placement |
| B-22 | Elevator lobby and breaker room, hand-authored required rooms | 8h | Read as important spaces |
| B-23 | Prop density pass on all twelve rooms | 6h | Looks lived-in, stays within draw call budget |

### Gate 6 — the decision point for the generator

- [ ] Same seed produces identical geometry on both clients, verified in a build
- [ ] Determinism test green across 100 runs
- [ ] Every generated floor is completable
- [ ] Generation plus NavMesh bake completes in under 3 s

**If this gate fails or the sprint overruns by more than 50%,** invoke the fallback from Technical Plan §6.5: one hand-authored floor with randomised contents, loot, blocked routes, and enemy placement. Ninety percent of the perceived variety, and it unblocks everything downstream. Make this call at the end of Sprint 6 — never later.

---

## 10. Sprint 7 — The Objective Loop (Weeks 15–16)

This is the sprint where it becomes a game rather than a tech demo.

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-48 | Power zones, `IPowerConsumer`, `PowerStateChanged` events | 7h | Lights, doors, and enemies all react |
| A-49 | Breaker interactable requiring two simultaneous holders | 6h | Solo player physically cannot complete it |
| A-50 | Restore Power objective, replicated progress | 5h | HUD objective list updates for both |
| A-51 | Elevator exit, both players required | 5h | Run ends only when both arrive |
| A-52 | Run success and failure flow, results screen | 6h | Timer shown as a rating modifier per GDD §6.2 |
| A-53 | Downed state, bleed-out, revive | 6h | 60 s window, revive interruptible |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-24 | Breaker panel, elevator, and terminal props with clear affordances | 8h | A player knows what is interactable without a prompt |
| B-25 | Lighting pass: powered and unpowered state for every room | 10h | Unpowered rooms are dark but navigable with a flashlight |
| B-26 | Emergency lighting and exit sign materials | 4h | Red emergency state reads instantly |

### Gate 7 — the first real playtest

- [ ] A full run is playable end to end with two players
- [ ] The objective cannot be completed alone
- [ ] Both success and failure states work
- [ ] Play it three times and write down what is boring

---

## 11. Sprint 8 — Voice (Weeks 17–18)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-54 | Dissonance integrated with NGO, proximity channel | 8h | Two players hear each other positionally |
| A-55 | Wall occlusion and rolloff tuning | 5h | Voice muffles through walls, clear through doorways |
| A-56 | Push-to-talk, mute, per-player volume, global disable | 5h | All four work and persist in settings |
| A-57 | Spectator state for dead players | 4h | Free-look camera, no interaction |
| A-58 | Equipment channel: emitter registry, per-listener selection | 8h | Two living players hear the dead one from different speakers |
| A-59 | DSP chain: band-pass, bit-crush, delay | 5h | Degraded but occasionally intelligible |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-27 | Emitter props: monitor, PA grille, desk phone, printer | 8h | Placed by the generator, not hand-placed |
| B-28 | Emitter active-state VFX: flicker, static, cone movement | 6h | Visible from across a room |
| B-29 | Two more prop sets for environmental storytelling | 6h | Sticky notes, abandoned desks, spilled coffee |

### Gate 8

- [ ] Proximity voice works on two separate machines over the internet
- [ ] A dead player's voice emerges from the nearest speaker, distorted
- [ ] Voice service failure degrades to silence, never to a crash
- [ ] Mute and push-to-talk work reliably

---

## 12. Sprint 9 — PS1 Render Pipeline (Weeks 19–20)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-60 | Vertex jitter shader, snap precision exposed as a parameter | 6h | Applied to all world geometry |
| A-61 | Affine texture mapping via `noperspective` UVs | 4h | Visible warping on floors, correct on UI |
| A-62 | Low internal resolution render target via Render Graph | 7h | 480×360 upscaled point-filtered, resolution selectable |
| A-63 | Ordered dithering and colour depth reduction pass | 5h | Banding reads as intentional |
| A-64 | Fog tuning and draw distance | 4h | Corridors disappear into darkness at the right depth |
| A-65 | VHS pass: scanlines, chromatic aberration, tracking noise | 6h | Toggleable in settings |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-30 | Texture pass on every asset to PS1 budget and palette | 12h | Nothing above 256×256 except mini-boss-class assets |
| B-31 | Light probe placement in all twelve room prefabs | 8h | Props are lit consistently in generated floors |
| B-32 | Material consolidation to reduce draw calls | 5h | Under 400 draw calls in the worst room |

### Gate 9

- [ ] A build looks like the reference board from B-08
- [ ] 60 FPS on the target GPU in a build, not the editor
- [ ] Under 400 draw calls
- [ ] The style reads as deliberate, not as low quality

---

## 13. Sprint 10 — Audio and the Glitch (Weeks 21–22)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-66 | `IAudioService`, mixer groups, snapshots | 5h | Voice, SFX, ambience, music independently controlled |
| A-67 | Ambient layer system with intensity driven by nearby threat | 6h | Tension rises before the player sees anything |
| A-68 | Footsteps by surface, breathing tied to stamina and health | 5h | Audible to other players at range |
| A-69 | `IAnomalyService` with guaranteed expiry and cleanup | 6h | No effect can leak past its lifetime, tested |
| A-70 | Glitch enemy: physical immunity, light vulnerability, vision corruption, short teleport | 10h | Cannot be killed with the mug, dies to the laser pointer |
| A-71 | AI PA voice system with escalating lines | 4h | Triggered by objective progress |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-33 | Ambient beds: server hum, ventilation, fluorescent buzz | 8h | Loop seamlessly, layer cleanly |
| B-34 | Glitch VFX final | 6h | Unmistakably digital, readable in darkness |
| B-35 | UI sounds and AI voice recording or synthesis | 6h | Corporate-cheerful, degrading over the run |

### Gate 10

- [ ] A new player learns the physical/digital rule within five minutes without being told
- [ ] Anomaly effects always clean up on death, disconnect, and run end
- [ ] Darkness is playable because audio carries the information

---

## 14. Sprint 11 — Polish, Performance, Playtest (Weeks 23–24)

### Track A

| ID | Task | Est. | Done when |
|---|---|---|---|
| A-72 | Profiling pass on a build: CPU, GPU, memory | 6h | Meets every budget in Technical Plan §8.2 |
| A-73 | Zero-allocation pass on gameplay hot paths | 8h | Profiler shows 0 B/frame during combat |
| A-74 | Disconnect handling: player leaves, host leaves, graceful lobby return | 6h | No crash, no softlock, clear messaging |
| A-75 | Settings menu: sensitivity, volume, voice, resolution, VHS toggle | 5h | Persists between sessions |
| A-76 | Bug triage and fix from the Gate 7 playtest notes | 10h | No known softlocks or desyncs |

### Track B

| ID | Task | Est. | Done when |
|---|---|---|---|
| B-36 | Main menu and lobby art | 8h | Sets tone before the game starts |
| B-37 | Final prop and detail pass on all rooms | 10h | The office feels used, not decorated |
| B-38 | Capture footage for the internal review | 4h | Five minutes of representative gameplay |

### Gate 11 — MVP Acceptance

Run the checklist in §1 with **two people who are not on the team**, on their own machines, without instructions.

- [ ] They connect without help
- [ ] They complete a run
- [ ] They are audibly frightened at least once
- [ ] They ask to play again
- [ ] No crash, no desync, no softlock across three sessions

If item three fails, the problem is design, not content. More enemies will not fix it. Stop and diagnose before continuing to M4.

---

## 15. Cut List

If the plan slips, cut in this order. Never cut by shortening a sprint — cut whole features.

| Order | Cut | Saved | Cost of cutting |
|---|---|---|---|
| 1 | VHS post-processing pass | ~6h | Aesthetic only, add later |
| 2 | Laser pointer as a fourth weapon | ~8h | Glitch then needs another light source — use the flashlight |
| 3 | Printer as a second enemy | ~20h | Slice becomes thinner but still teaches its lessons |
| 4 | Procedural generation → one hand-authored floor with randomised contents | ~35h | Replayability drops; **decide at Gate 6, not later** |
| 5 | Equipment voice channel | ~15h | Loses the signature mechanic. Cut this only to save the release. |

**Never cut:** proximity voice, the physical/digital enemy rule, the two-player objective, or the determinism test. Each of those is load-bearing.

---

## 16. Working Rhythm

| Cadence | Practice |
|---|---|
| Daily | Push before you stop. `main` stays playable. |
| Weekly | 30-minute sync: what shipped, what is blocked, what changed in the docs |
| Every sprint | Build a standalone executable and play a full session together |
| Every sprint | Run the determinism test and the `RunState` snapshot test |
| Every sprint | Profile on the build, not in the editor |
| Every two sprints | `git lfs ls-files -s \| sort -k2 -h \| tail -20` and check the GitHub LFS quota |

**Tracking:** GitHub Projects board with the task IDs from this document. Columns: Backlog, Sprint, In Progress, Review, Done. One card per task ID. If a task has been In Progress for more than a week, it was estimated wrong — split it.

**Definition of Done** for every task: it works in a build, it works with two clients, it has no known desync, it is pushed to `main`, and the other person has seen it run.

---

## 17. What Comes After the MVP

Do not plan M4 in detail now. After Gate 11 the design will have changed based on what the playtest revealed, and any detailed plan written today will be wrong.

The one thing worth deciding early: **if the MVP is fun, the next milestone is the second floor and the first mini-boss. If it is not fun, the next milestone is fixing why** — and that work is design, not content.
