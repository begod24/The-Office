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
2. Open `Assets/Project/Scenes/SCN_Boot.unity`.
3. Press Play. The greybox sandbox loads and a fallback camera shows the room.
4. Press **Host** in the panel at the top left. A six-character join code appears — that is what
   a friend types into **Join**.
5. WASD to move, mouse to look, Shift to sprint, Ctrl to crouch, Esc to release the cursor.
6. **F1** hides and shows the session panel.

Two players from one machine: Window → Multiplayer → Multiplayer Play Mode, enable a virtual
player, host in one and join in the other.

If hosting fails with a services error, Relay and Lobby must be enabled for this project in the
Unity Cloud dashboard.

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

M0 in progress.

- [x] Unity project configured: URP, Input System, physics layers, assembly definitions
- [x] `SCN_Boot`, composition root, service locator, event bus
- [x] NGO integrated, host and join over Relay working
- [x] Multiplayer Play Mode package installed
- [x] Networked player: movement, look, crouch, stamina, client-authoritative
- [x] `.editorconfig` and coding standards written down
- [x] `RunState` structure defined, with a JSON snapshot test
- [ ] Room connector contract signed off by both — `Docs/KitContract.md`
- [ ] B: modular kit on the 2 m grid, first ten pieces

**Exit criterion for M0:** two players connect and walk around a static greybox office.
Nothing else.
