# Contributing

Two people, one Unity project, binary-ish assets. Most of these rules exist to stop a merge
conflict nobody can read.

---

## Ownership

| Area | Owner | Rule |
|---|---|---|
| `Assets/Project/Script/**` | A | B does not edit code |
| `SCN_Boot` | A | Changes rarely; B never opens it |
| `Assets/Project/Art/**`, room prefabs, level scenes | B | A does not edit meshes or materials |
| `Assets/Project/ScriptableObject/**` | Either | Tuning lives here precisely so it is safe to touch |
| `Docs/**` | Either | `KitContract.md` needs both signatures |

`main` is always playable. Never push a build that does not run.

---

## Scene and prefab discipline

Unity scenes and prefabs are YAML. Git can merge them in theory and destroys them in practice.

1. **Never edit the same scene or prefab as the other person at the same time.** Say so in chat
   before opening one.
2. `.gitattributes` marks `*.unity` and `*.prefab` as `lockable`. Use `git lfs lock <path>`
   before a long editing session on a shared scene.
3. Anything that can be generated from code, is. Run `Office/Setup/...` instead of hand-editing
   `SCN_Boot`, `SCN_Sandbox` or `PF_Player` — the generator is the source of truth for those
   three, and hand edits will be overwritten without warning.
4. Prefer prefabs over scene objects. A prefab conflict affects one file; a scene conflict
   affects everything in the scene.

---

## Art pipeline

Blender files stay **outside** this repository. Only exported FBX is dropped into
`Assets/Project/Art/Models/`.

- LFS already tracks `*.fbx`, `*.png`, `*.wav` and friends — verify with `git lfs ls-files`
  after adding binaries. If a mesh shows up as a huge text diff, LFS did not pick it up and the
  commit must be redone.
- Export settings, naming and budgets: `Docs/KitContract.md` §5–6.
- **Back up your `.blend` files yourself.** This repository does not hold them.

Every two sprints, audit LFS usage:

```bash
git lfs ls-files -s | sort -k2 -h | tail -20
```

---

## Code standards

Full list in Technical Plan §11. The ones that actually get violated:

- `[SerializeField] private` instead of `public` fields — always
- `sealed` on classes not designed for inheritance
- One class per file, filename matches the class
- No `GameObject.Find`, no `SendMessage`, no `Invoke("MethodName")`
- No LINQ, no string concatenation, no `GetComponent` in `Update` — the project budgets **zero**
  GC allocation per frame (Technical Plan §8.2)
- Unsubscribe in `OnDisable` / `OnNetworkDespawn`. Leaked subscriptions on pooled objects are
  the single most common source of "it works the first time" bugs.
- `.editorconfig` is in the repository root and both machines use it. If your diff is full of
  whitespace changes, your editor is ignoring it.

### Networking rules

- If a system touches game state, its authority model is decided **before** the first line is
  written. Retrofitting authority is a rewrite, not a refactor.
- Never write a system singleplayer intending to network it later.
- `NetworkVariable` for state a late joiner needs; RPC for one-shot events. Never the reverse.
- Nothing is replicated that can be derived locally — muzzle flashes, footsteps, camera shake
  and screen effects are local reactions to replicated state.
- Use `OnNetworkSpawn()`, not `Start()`. Do not assume ordering between them.

---

## Before you push

1. It compiles with no warnings you introduced
2. `Office/Tests/Run EditMode Tests` is green
3. You tested it with **two** clients (Multiplayer Play Mode counts)
4. No new console errors on entering play mode
5. `git status` shows nothing you did not intend — especially not `Library/` or `.csproj`

**Definition of Done** for any task: it works in a build, it works with two clients, it has no
known desync, it is pushed to `main`, and the other person has seen it run.

---

## Rhythm

| Cadence | Practice |
|---|---|
| Daily | Push before you stop |
| Weekly | 30-minute sync: what shipped, what is blocked, what changed in the docs |
| Every sprint | Standalone build, play a full session together |
| Every sprint | Run the EditMode suite and profile **on the build**, not in the editor |
| Every two sprints | LFS audit |
