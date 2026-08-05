# The-Office

Working title. Cooperative first-person horror for 1–4 players, set in an office that has stopped
obeying physics. PS1-era rendering, improvised weapons, proximity voice.

> **Naming warning.** The current repository name references an existing television trademark,
> and the concept art carries a real stapler manufacturer's brand. Both must be replaced before
> any public build, storefront page or trailer. GDD §3.1.

**Unity 6000.3.6f1** · URP 17.3.0 · Netcode for GameObjects 2.13.1 · Multiplayer Services 2.3.0

---

## Running it

1. Open the project in Unity 6000.3.6f1.
2. Open `Assets/Project/Scenes/SCN_Boot.unity`. **Always start from this scene** — it is the
   composition root, and playing any other scene directly leaves every service unregistered.
3. Press Play. The lobby appears.
4. Press **HOST A SHIFT**. A six-character join code appears — that is what a friend types into
   the code field and joins with.
5. Press **READY**, then **START THE SHIFT**. The greybox sandbox loads and your player spawns.
6. WASD to move, mouse to look, Shift to sprint, Ctrl to crouch, Esc to release the cursor.
7. **F1** opens the debug overlay, which has the only way back to the lobby for now: **End run**.

If hosting fails with a services error, Relay and Lobby must be enabled for this project in the
Unity Cloud dashboard.

### Testing with two players

Window → Multiplayer → Multiplayer Play Mode, enable one virtual player, then in each editor
open `SCN_Boot` and press Play. Host in one, join with the code in the other.

Check these, none of which is covered by an automated test yet:

- [ ] Both see two rows in the roster, each labelled EMPLOYEE 01 and EMPLOYEE 02
- [ ] Pressing READY in one shows READY in the other within a moment
- [ ] START THE SHIFT stays disabled until both are ready, and only the host sees it
- [ ] Starting the run moves both into the sandbox, each with its own capsule
- [ ] Each sees the other move, with the red facing marker pointing where they look
- [ ] End run from the F1 overlay returns both to the lobby with ready flags cleared

### Regenerating the generated content

`SCN_Boot`, `SCN_Sandbox` and `PF_Player` are built from code and must not be hand-edited.

| Menu item | Effect |
|---|---|
| `Office/Setup/Run All` | Rebuilds all of the above plus the collision matrix and configs |
| `Office/Tests/Run EditMode Tests` | Runs the suite and logs a summary |

---

## Documents

| Document | What it is for |
|---|---|
| `GDD.md` | The design. What the game is. |
| `TECHNICAL_PLAN.md` | The target architecture. Read before writing a system. |
| `MVP_PLAN.md` | The schedule, sprint by sprint, with gates. |
| `Docs/Architecture.md` | What actually exists in the repository right now. |
| `Docs/KitContract.md` | The modular kit contract. **Blocks all room building until signed.** |
| `CONTRIBUTING.md` | Ownership, merge discipline, art pipeline, code standards. |

---

## Status

M0 nearly closed, first Sprint 1 task done.

- [x] Unity project configured: URP, Input System, physics layers, assembly definitions
- [x] `SCN_Boot`, composition root, service locator, event bus
- [x] NGO integrated, host and join over Relay working
- [x] Multiplayer Play Mode package installed
- [x] Networked player: movement, look, crouch, stamina, client-authoritative
- [x] `.editorconfig` and coding standards written down
- [x] `RunState` structure defined, with a JSON snapshot test
- [x] A-11: lobby with roster, ready state and start run; server-authoritative phase
- [ ] Room connector contract signed off by both — `Docs/KitContract.md`
- [ ] B: modular kit on the 2 m grid, first ten pieces
- [ ] Two-player pass by hand — see the checklist above

**Exit criterion for M0:** two players connect and walk around a static greybox office.
Nothing else.
