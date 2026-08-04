# Office Nightmare — Game Design Document

**Version:** 0.1 (pre-production)
**Engine:** Unity 6000.3.6f1, URP
**Team:** 2 people — A (systems, gameplay code), B (level design, 3D art)
**Status:** living document. Every section marked `[OPEN]` is an unresolved decision.

---

## 0. Decision Log

Locked as of v0.1. Changing any of these requires re-planning the affected milestones.

| # | Decision | Value | Locked |
|---|---|---|---|
| 1 | Netcode | Unity Netcode for GameObjects (NGO) | ✔ |
| 2 | Multiplayer mode | Online only. No local split-screen. | ✔ |
| 3 | Level kit grid | 2 m module, 0.25 m sub-grid | ✔ |
| 4 | Proximity voice chat | In scope, core feature | ✔ |
| 5 | Player roles | Soft roles. No classes. | ✔ |
| 6 | Meta-progression | None. Every run starts equal. | ✔ |
| 7 | Release timer | Score modifier only, no fail state | ✔ |
| 8 | Dead player voice | Audible to the living, distorted through office equipment | ✔ |
| 9 | Host migration | Desired. Deferred to M4; architecture prepared from M0. | ✔ |

---

## 1. High Concept

Four office workers stay late to finish a release. They fall asleep at their desks. They wake up in an office that has stopped obeying physics — corridors loop, rooms lead into themselves, and every piece of equipment has come alive. A countdown to the morning release keeps running.

To get out, the team must restore power, restart the servers, destroy the AI core that caused the infection, and reach the exit before the workday begins.

**Pitch line:** *Lethal Company's cooperative tension inside a PS1-era corporate haunted house.*

---

## 2. Design Pillars

Every feature must serve at least one pillar. Features that serve none get cut.

| Pillar | Meaning | Consequence |
|---|---|---|
| **Powerless professionals** | Players are not soldiers. Combat is clumsy, improvised, and rarely the best answer. | No real firearms. Weapons break. Running is often correct. |
| **The building is the antagonist** | The office itself is hostile — layout, lights, sound, and even the UI. | Anomalies must affect systems, not just deal damage. |
| **Forced cooperation** | No single player can complete a run alone comfortably. | Objectives require simultaneous actions in different rooms. |
| **Mundane made wrong** | Horror comes from familiar objects behaving incorrectly, not from gore. | Art and audio must be believable before they are scary. |

---

## 3. Product Definition

| Field | Value |
|---|---|
| Genre | Cooperative first-person horror, extraction / escape |
| Players | 1–4, online co-op |
| Session length | 25–40 minutes per run |
| Platform | PC (Windows) first. Steam. |
| Target rating | Mature / 16+ |
| Camera | First person |
| Art style | PS1-era low-poly, VHS post-processing |
| Monetization | Premium, single purchase `[OPEN]` |
| Release model | Steam Early Access after vertical slice `[OPEN]` |

### 3.1 Naming warning

The current working title and repository name reference an existing television trademark. Before any public build, storefront page, or trailer, a clean original title must be chosen and checked for trademark conflicts. Do the same for in-game brands — the stapler in the concept art carries a real manufacturer's name and must be replaced with a fictional one.

---

## 4. Setting

A modern IT company occupying several floors of an office building.

**Zones:** open space, meeting rooms, kitchen, reception, break room, executive offices, hardware storage, archive, server room, data center.

After the incident the space is caught between the physical and the digital:

- Severed cable bundles growing along walls like roots
- Corridors that extend beyond the building footprint
- Walls that were not there before
- Frozen monitors, Windows error dialogs, digital artifacts
- Red emergency lighting, flickering fluorescents

The building should feel like it was real yesterday. Every anomaly reads stronger against ordinary detail — a normal kitchen with normal mugs makes the impossible corridor next to it land.

---

## 5. Narrative

### 5.1 Setup

The company is preparing its most important launch of the year. Several employees volunteer to stay overnight. They fall asleep. When they wake, doors are gone, elevators are dead, and there is no connection to the outside.

The corporate assistant's voice announces over the PA:

> Release starts in 07:59:58

### 5.2 Reveal

The company was building an experimental AI to automate office operations. During the final night build, a critical update gave it access to the building's infrastructure: power grid, servers, security, robotics, and every networked device. After the failure, it began classifying employees as system errors.

### 5.3 Delivery method

No cutscenes. Story is delivered through:

- PA announcements from the AI, escalating from polite to hostile
- Readable artifacts: Jira tickets, Slack messages on frozen monitors, printed PRDs, sticky notes
- Environmental storytelling: the desk where someone clearly did not wake up
- Terminal logs at each server the players restart

`[OPEN]` Do players have named characters with backstories, or are they anonymous employees? Anonymous is cheaper and works better for co-op identification.

---

## 6. Core Loop

```
Spawn in the office
      ↓
Explore the floor, find resources and objective locations
      ↓
Craft / improvise weapons and tools
      ↓
Complete floor objective (restore power, restart a server node)
      ↓
New sections of the office unlock
      ↓
Fight office creatures and digital entities
      ↓
Defeat floor mini-boss
      ↓
Descend deeper
      ↓
Reach the central data center
      ↓
Final boss
      ↓
Escape before the release timer hits zero
```

### 6.1 Run structure

| Stage | Duration | Content |
|---|---|---|
| Floor 1 — Open Space | 8–10 min | Tutorial pressure, restore power, first enemies |
| Floor 2 — Archive / Storage | 8–10 min | Darkness, resource scarcity, mini-boss: The Manager |
| Floor 3 — Server Room | 8–10 min | Heavy anomaly interference, mini-boss: Main Server |
| Floor 4 — Data Center | 6–8 min | Final boss: Cloud, then timed escape |

### 6.2 The timer

The release countdown starts at 08:00:00 in-game and maps to real session time. It is not a hard fail timer for the whole run — it is a pressure device.

**LOCKED:** the timer is a **score modifier only**. Reaching zero does not fail the run, does not escalate spawns, and does not lock content. It affects the end-of-run rating.

**Consequence that must be designed around.** The premise is built on a countdown, but a countdown with no teeth stops being read after two runs. If the timer applies no pressure, pressure must come from elsewhere or the run becomes a leisurely walk. Mandatory compensating systems:

- **Consumable scarcity.** Flashlight batteries, first aid kits, and ranged ammunition are finite per run and do not respawn. Time spent exploring costs battery.
- **One-way progression.** Descending a floor is irreversible. Anything left behind is lost.
- **Escalating director.** Enemy density rises with elapsed run time regardless of the timer. This is the real pressure system.
- **Degrading weapons.** Every weapon has durability. Combat is a spending decision.

Design check at M3: if playtesters ignore the clock entirely and never feel hurried, the pressure systems above have failed and the timer decision should be revisited.

---

## 7. Players

### 7.1 Capabilities

| System | Design |
|---|---|
| Movement | Walk, sprint with stamina, crouch, vault low obstacles |
| Health | 100 HP, no regeneration. Healing only from first aid kits found in the office. |
| Stamina | Drains on sprint, refills when walking. Panic drains it faster. |
| Inventory | 4 hotbar slots. Weapons and tools compete for the same slots. |
| Light | Every player has a phone flashlight with a battery. Batteries are a resource. |
| Interaction | Single interact key, context-sensitive. Some interactions require two players. |
| Downed state | At 0 HP a player is downed, not dead. A teammate can revive within 60 seconds. |
| Death | Becomes a spectator, can watch teammates. `[OPEN]` Can dead players still help? |

### 7.2 Roles

The concept mentions each player having a role. Two options:

- **Hard classes** (Developer, Designer, QA, Manager) with unique abilities. More depth, much more balancing and content work.
- **Soft roles** — no classes; roles emerge from who picks up which tool. One player carries the multimeter, another the fire extinguisher.

**LOCKED: soft roles.** No classes, no class-specific abilities, no class selection screen. All players are mechanically identical at spawn.

Roles emerge from equipment and from what the group needs: whoever picked up the multimeter becomes the electrician for that run, whoever carries the extinguisher takes point. Because inventory is only four slots, a group of four cannot carry everything — specialisation is forced by scarcity, not by a menu.

This removes an entire balancing axis, four sets of unique animations and abilities, and the class-vs-enemy balance matrix. It is the single largest scope saving in this document.

### 7.3 Proximity voice chat

**LOCKED: in scope, treated as a core mechanic rather than a convenience feature.**

Voice is a design system with rules, not a chat box. It is the primary reason separation is frightening and the primary source of shareable moments.

| Property | Value |
|---|---|
| Audible range | ~15 m, with rolloff `[OPEN — tune in playtest]` |
| Wall occlusion | Yes. Muffled through walls, clear through doorways. |
| Transmission mode | Open mic by default, push-to-talk available in options |
| Radio | Walkie-talkies are findable items. Two units allow cross-map speech at a cost: they occupy an inventory slot and their transmissions are audible to nearby enemies. |
| Anomaly interference | Digital entities distort, delay, or echo nearby voice. Glitch proximity makes teammates unintelligible. |
| Spectator | See 7.3.1 |

#### 7.3.1 The voice of the dead

**LOCKED:** dead players remain audible to the living, but their voice is **routed through the office equipment** — it emerges from the nearest speaker, monitor, PA grille, or ringing desk phone, heavily filtered and degraded.

Why this is the strongest single mechanic in the design:

- Death stops being an exit from the session. A dead player still participates and still matters.
- It converts a technical state into a horror beat — a colleague's warning arriving through a printer is exactly the tone this game is aiming for.
- It creates ambiguity. Is that your dead friend on the PA, or the AI imitating him? The AI should occasionally do exactly that.
- Living players lose reliable information, which is more frightening than losing information entirely.

Rules: the dead cannot be understood perfectly; the filter should cost intelligibility. Enemies react to equipment that is transmitting. And the AI is permitted to spoof this channel — sparingly, and never on the first run.

### 7.4 Local split-screen

**LOCKED: online only.** The four-way split screen in the concept art is presentation only and is not a target. Local split-screen would triple camera, input, UI, and rendering complexity, and it structurally destroys both the isolation the horror depends on and the proximity voice mechanic above.

---

## 8. Combat and Weapons

### 8.1 Philosophy

Combat is a last resort. It is loud, it attracts more enemies, and weapons degrade. A player who fights everything runs out of options before the data center.

### 8.2 Weapon categories

| Category | Examples | Role |
|---|---|---|
| Melee — light | Stapler, ruler, mug, keyboard | Always available, low damage, fast |
| Melee — heavy | Monitor, office chair, mop, fire extinguisher | Slow, high damage, high stamina cost |
| Ranged — improvised | Staple gun, water pistol, glue gun, laser pointer | Limited ammo, situational |
| Utility | Tape, extension cord, cleaning spray, batteries | Not weapons — enablers |

### 8.3 Elemental interaction

This is what makes improvised weapons interesting rather than reskinned swords. Enemy categories have explicit vulnerabilities:

| Weapon property | Strong against | Notes |
|---|---|---|
| Water (water pistol, cooler bottle) | Electrical enemies | Also damages the player if used near live cables |
| Blunt (extinguisher, monitor) | Mechanical enemies | Loud — increases aggro radius |
| Adhesive (tape, glue gun) | Fast enemies | Immobilizes rather than kills |
| Light (laser pointer, flashlight) | Digital entities | The only effective answer to Glitch-class enemies |
| Cutting (scissors) | Cable Parasite | Severs, does not destroy |

Digital entities cannot be killed with physical weapons at all. This is the central combat lesson players must learn in the first ten minutes.

### 8.4 Crafting

Simple, deterministic combination at workbenches (desks with a toolbox):

```
Stapler + Rubber bands + Pencil  →  Staple Rifle
Spray bottle + Lighter           →  Improvised Torch
Extension cord + Water           →  Trap: Electrified Puddle
Tape + Two melee items           →  Double-headed weapon
```

Recipes are discovered, not given. Found sticky notes reveal them.

`[OPEN]` Is crafting per-run only (roguelite) or does it persist? Per-run is far simpler and is the recommendation for v1.

---

## 9. Enemies

### 9.1 Category A — Living Office Equipment

Physical, destructible, spatial threats. They navigate the floor and can be fought.

| # | Name | Behavior | Counter |
|---|---|---|---|
| 1 | The Shredder | Slow, high damage, blocks corridors | Blunt, keep distance |
| 2 | Ceiling Fan | Drops from ceiling, area damage | Look up, blunt |
| 3 | Water Cooler | Sprays water, creates conductive puddles | Blunt, avoid puddles |
| 4 | Monitor | Emits screen flash that disorients | Break line of sight |
| 5 | Printer | Ranged, fires paper shards | Blunt, cover |
| 6 | Copier | Spawns weak duplicates | Kill fast before it copies |
| 7 | Server Rack | Stationary turret, high HP | Water, or avoid |
| 8 | Projector | Creates illusory enemies and fake doors | Destroy the projector, not the illusions |
| 9 | Cable Parasite | Grapples and drags players | Cutting, teammate rescue |
| 10 | Extension Cord | Fast, low HP, swarm | Blunt, area attacks |
| 11 | Electrical Outlet | Stationary hazard, chains lightning | Water is fatal here — do not |
| 12 | Desk Lamp | Blinds, marks players for other enemies | Break the bulb |
| 13 | Stapler | Fast melee swarm | Any melee |
| 14 | Hole Punch | Charging attack | Dodge, blunt |
| 15 | Scissors | High damage, fragile | Ranged if possible |
| 16 | Folder | Mimic — appears as a lootable item | Careful looting |

### 9.2 Category B — Digital Entities

Immune to physical damage. They attack the systems of the game, not the player's health bar. Each must have a specific, learnable counter.

| # | Name | Effect | Counter |
|---|---|---|---|
| 17 | Glitch | Corrupts vision, teleports the player short distances | Light-based weapons |
| 18 | Memory Leak | Spreading floor corruption; slows and drains stamina inside it | Restart the nearest node to purge |
| 19 | BSOD | Disables the player's HUD entirely for 30 seconds | Wait it out, or a teammate reboots you at a terminal |
| 20 | Trojan | Disguises itself as a supply crate; on opening, spawns enemies | Inspect before opening |

**Design rule:** digital entities may break the interface, distort audio, alter room layout, and teleport players — but they must never take direct control of player input. Removing agency in a horror game reads as a bug, not as fear.

### 9.3 Mini-bosses and final boss

| Name | Floor | Concept |
|---|---|---|
| **The Manager** | 2 | Humanoid with a monitor head and loudspeakers. Broadcasts commands that alter player controls (inverted axis, forced walk). Multi-phase: destroy the speakers, then the screen. |
| **Main Server** | 3 | Vertical arena. Continuously spawns adds. Players must physically disconnect cable clusters while under pressure. |
| **Cloud** | 4 | Final boss. Non-physical, distributed presence. Cannot be attacked directly — players damage it by restarting nodes across the arena while it corrupts the environment. |

The final boss should test everything the run has taught: cooperation, resource discipline, understanding of the digital/physical distinction.

---

## 10. Objectives and Systems

### 10.1 Objective types

| Type | Description | Cooperation requirement |
|---|---|---|
| Restore Power | Find and flip N breakers in the electrical room | Breakers must be held simultaneously in different rooms |
| Restart Servers | Enter a code at a terminal; code is displayed on a monitor elsewhere | One reads, one types |
| Repair Network | Reconnect cable segments in the right sequence | Puzzle, solvable solo but slow |
| Retrieve Keycard | Retrieve an item from an area guarded by a mini-boss | Combat |
| Reach the Elevator | Timed escape while the floor collapses | Everyone must arrive |

Objectives are drawn from a pool per floor so runs vary.

### 10.2 The power system

Power is the connective tissue between every system and deserves to be a real simulation, not a set piece:

- Lights on = safer, but enemies see the players from further away
- Lights off = enemies are slower to detect, but players need flashlight batteries
- Some doors and elevators require power
- Some enemies (Electrical Outlet, Extension Cord) are strengthened by active power
- Cutting power to a zone is a valid tactical choice with real trade-offs

This single system generates more emergent decisions than any amount of extra content.

---

## 11. Progression

**LOCKED: no meta-progression.** Every run starts equal. Nothing carries between runs — no unlocks, no upgrades, no persistent currency, no account level.

The only thing that progresses is player knowledge: which weapon answers which enemy, where breakers tend to be, what the Trojan looks like before it opens. That is the intended progression curve and it costs nothing to build.

Consequences:
- No save system beyond settings and keybinds
- No persistence layer, no cloud sync, no account state
- Balance is tuned once, not across a progression curve
- A first-time player and a veteran can play together without any matchmaking or scaling logic

This decision removes an entire subsystem from the technical plan. If a metagame is ever wanted, the cheapest possible version is knowledge-based and cosmetic — unlocked recipe notes, lore entries, office badges. **Never stat upgrades**, which would break the co-op parity that makes this design work.

---

## 12. Art Direction

### 12.1 Visual target

PS1-era rendering, executed deliberately rather than as an excuse for low quality:

- Vertex snapping / jitter (no sub-pixel precision)
- Affine texture mapping (warped texture perspective)
- Low internal render resolution (320×240 or 480×360) upscaled with nearest-neighbour
- Limited colour depth with ordered dithering
- No shadows on most objects; use baked-in darkness and light pools
- Aggressive fog for draw distance and dread
- VHS layer: scanlines, chromatic aberration, tape wobble, tracking errors

This style is a strategic choice, not only an aesthetic one — it slashes texture and polygon budgets, hides animation limitations, and makes a two-person art pipeline viable.

### 12.2 Budgets

| Asset type | Triangle budget | Texture |
|---|---|---|
| Small prop | 100–400 | 128×128 |
| Large prop / furniture | 400–1200 | 256×256 |
| Standard enemy | 800–2000 | 256×256 |
| Mini-boss | 3000–6000 | 512×512 |
| Player hands / held item | 500–1500 | 256×256 |
| Modular wall / floor piece | 20–200 | 256×256 tileable |

Palette: desaturated corporate greys, beige, fluorescent white, with red emergency lighting and sickly green CRT glow as the only saturated colours.

### 12.3 Modular kit

The level kit is the highest-priority art deliverable. Before any enemy is modelled, B should produce a complete modular office kit on a strict grid (recommended 2m module, 0.25m sub-grid) covering: walls, doorways, floors, ceilings with light fixtures, desks, partitions, doors, and corridor connectors.

Everything downstream — procedural generation, lighting, collision, navigation — depends on this kit being consistent. Getting the grid wrong is the most expensive mistake available in this project.

---

## 13. Audio Direction

Audio carries more of the horror than the visuals do at this fidelity.

| Layer | Content |
|---|---|
| Ambient bed | Server room hum, ventilation, distant fluorescent buzz |
| Reactive ambience | Intensity rises with nearby enemy count, before the player can see anything |
| Enemy audio | Each enemy has a distinct approach sound. Players must identify threats by ear. |
| The AI voice | Corporate-cheerful text-to-speech, degrading over the run |
| Silence | Used deliberately. Sudden absence of the server hum means something is very wrong. |
| UI | Mechanical, 8-bit, diegetic where possible |

Design rule: every enemy must be identifiable by sound before it is visible. This is what makes darkness playable instead of unfair.

---

## 14. UI/UX

Based on the concept mockups, the retro variant is the correct direction — it matches the art style and is cheaper to produce.

**HUD elements:**
- Objective list, top-left, updates with a terminal-style animation
- Teammate health bars, bottom-left
- Held item and durability, bottom-right
- Ammo / charges where applicable
- No minimap. Navigation confusion is a feature.
- No damage direction indicator. Use audio.

**Diegetic priority:** where an element can live in the world instead of on the screen, it should. Battery level shown on the phone model. Health communicated through screen effects and breathing. Objectives readable on a physical clipboard the player carries.

The BSOD enemy removing the HUD only works if the HUD is minimal enough that players were relying on it.

---

## 15. Failure and Session Flow

| Situation | Result |
|---|---|
| One player downed | Revivable for 60s |
| Player dies | Becomes a spectator. Voice stays active, routed through office equipment (7.3.1). No interaction. |
| All players downed | Run failed, return to lobby |
| Timer expired | Run continues. End-of-run rating penalty only (6.2). |
| Player disconnects | Body remains 30s then despawns, carried items drop. Reconnect into the same run from M4. |
| Host disconnects — v1 | Session ends for everyone, all return to lobby, the run is lost. |
| Host disconnects — M4+ | Host migration: run state restored on a new host, clients reconnect. Technical Plan §2.7. |

---

## 16. Minimum Viable Product

Everything above is the full vision. This is what actually gets built first. Nothing outside this list is touched until it plays well.

**Vertical slice scope:**
- 1 floor, procedurally assembled from ~12 room prefabs
- 2-player co-op (architecture supports 4, testing targets 2)
- 3 enemies: Stapler (melee swarm), Printer (ranged), Glitch (digital, teaches the physical/digital rule)
- 4 weapons: mug, fire extinguisher, staple gun, laser pointer
- 1 objective type: Restore Power
- 1 exit: Reach the Elevator
- Full PS1 render pipeline
- Full audio layer for the above content
- No bosses, no crafting, no progression

If this slice is not frightening and not fun with two players and three enemies, adding seventeen more enemies will not fix it.

---

## 17. Open Decisions Summary

Nine decisions are locked in §0. These remain.

| # | Decision | Blocks | Needed by |
|---|---|---|---|
| 1 | Final title, trademark-safe | Store page, branding, repository name | Before any public build |
| 2 | Voice middleware: Dissonance vs Vivox | Voice implementation, budget | M1 |
| 3 | Named characters vs anonymous employees | Character art, VO scope | M3 |
| 4 | Crafting: per-run only, confirm | Inventory and recipe systems | M4 |
| 5 | Can dead players interact with anything? | Spectator system scope | M3 |
| 6 | Target run length: 25 / 30 / 40 min | Floor count, pacing, objective count | M2 |
| 7 | Release model and target date | Milestone planning | After M3 |
| 8 | Proximity voice range and rolloff values | Level design sight/sound lines | M2 playtest |

**Recommendation on #3:** anonymous employees. Named characters imply personalities, voice acting, and portrait art, and they weaken player identification in a game where the player is meant to be *themselves* trapped at work.

---

## 18. References

| Reference | What is being borrowed |
|---|---|
| Lethal Company | Co-op loop, proximity voice tension, objective structure |
| Content Warning | Team interaction, shared goals |
| Fears to Fathom | Atmosphere, mundane-made-wrong tone |
| SCP: Secret Laboratory | Cooperative survival under systemic pressure |
| Left 4 Dead | Wave pacing, director-style tension curve |
| PS1-era horror | Rendering constraints as aesthetic |

**Explicitly not borrowed:** comedy tone. This design is a dark technological horror. Every asset, sound, and line of AI dialogue should be evaluated against that.
