# Modular Kit Contract

**Status:** DRAFT — awaiting sign-off from B.
**Blocks:** every room prefab that will ever be built.

This is the single most expensive document in the project to get wrong. Technical Plan §14 names
it the first thing to resolve in M0, and Risk Register §13 prices a late grid change at weeks of
rework. **B must not build room number two until this is signed off.**

Once signed, change it only by rebuilding every room that already exists.

---

## 1. Grid

| Property | Value | Rationale |
|---|---|---|
| Module | 2.0 m × 2.0 m footprint | Locked, GDD §0 decision 3 |
| Sub-grid | 0.25 m | Props, trim, detail |
| Ceiling height | 3.0 m | 2 m base wall + 1 m header section |
| Wall thickness | 0.25 m | One sub-grid unit |
| Doorway width | 1.0 m | Centred on a module edge |
| Doorway height | 2.0 m | Header occupies the remaining 1 m |
| Corridor width | 2.0 m | Exactly one module |

**Verified in greybox.** `SCN_Sandbox` is built to these numbers. Walk it before signing:
a 1 m doorway against a 0.32 m player radius, a 2 m corridor with two players passing, and a
3 m ceiling read at 68° FOV.

---

## 2. Pivot and orientation

| Property | Value |
|---|---|
| Pivot | Bottom **corner** of the floor footprint, never centred |
| Up axis | +Y |
| Room forward | +Z |
| Connector forward | +Z **out of** the room, on every connector without exception |
| Unit scale | 1 Blender unit = 1 metre, applied on export |
| Rotation | Zero on export. No baked −90° X from Blender. |

A centred pivot makes grid snapping arithmetic instead of placement, and every off-by-one-metre
bug in a generated floor traces back to it.

---

## 3. Socket classes

| Class | Opening | Use |
|---|---|---|
| `Corridor` | 2.0 m wide × 3.0 m high | Open module edge, no door frame |
| `Door` | 1.0 m wide × 2.0 m high | Standard office door |
| `Wide` | 2.0 m wide × 2.4 m high | Double doors, lobby and server room entries |
| `Vent` | 0.6 m × 0.6 m, centred at 2.2 m | Crawl route, not walkable |

The generator only joins sockets of the same class. Unused sockets are closed with a wall cap
sized to the class, so a cap prefab is required for each.

---

## 4. Naming

| Kind | Pattern | Example |
|---|---|---|
| Static mesh | `SM_<Category>_<Name>_<NN>` | `SM_Wall_Corner_01` |
| Room prefab | `RM_<Zone>_<Name>_<NN>` | `RM_OpenSpace_Central_01` |
| Material | `M_<Surface>_<Variant>` | `M_Carpet_Grey` |
| Texture | `T_<Name>_<Channel>` | `T_Carpet_Grey_D` |
| Config asset | `CFG_<Name>` | `CFG_PlayerMovement` |

Channel suffixes: `_D` albedo, `_N` normal, `_M` mask/ORM, `_E` emissive.

---

## 5. Export workflow

Confirmed with A: **Blender sources stay outside the repository.** Only exported FBX is dropped
into `Assets/Project/Art/Models/`, which Git LFS already covers.

This differs from Technical Plan §9, which asked for a tracked `_Source/` folder. The trade is
accepted knowingly: no `.blend` in LFS keeps the repository small and avoids reimports on every
pull, at the cost of the source files existing only on B's machine.

**Consequence B must accept:** the `.blend` files are not backed up by this repository. Keep
them in a synced folder or a personal drive. Losing them means remodelling, not restoring.

FBX export settings:

- Selected objects only, apply transform, forward `-Z`, up `+Y`
- Apply modifiers, no cameras, no lights, no animation on static meshes
- Scale 1.0, units metres
- Smoothing: face
- Mesh names must match the FBX filename

---

## 6. Budgets

From GDD §12.2. Anything over budget is rejected at review, not fixed later.

| Asset type | Triangles | Texture |
|---|---|---|
| Modular wall / floor piece | 20–200 | 256×256 tileable |
| Small prop | 100–400 | 128×128 |
| Large prop / furniture | 400–1200 | 256×256 |
| Standard enemy | 800–2000 | 256×256 |
| Mini-boss | 3000–6000 | 512×512 |
| Player hands / held item | 500–1500 | 256×256 |

---

## 7. Definition of done for a kit piece

- [ ] Snaps on the 2 m grid with zero gap and zero overlap in every rotation
- [ ] Pivot at the bottom corner, zero rotation, unit scale
- [ ] Within the triangle and texture budget
- [ ] Collider present and on layer `LevelGeometry` (11)
- [ ] `RoomConnector` components on every opening, correct socket class, +Z outward
- [ ] No z-fighting when placed against every other piece of the same class
- [ ] Reviewed by A in a generated floor, not only in isolation

---

## 8. Sign-off

| Role | Name | Date | Signed |
|---|---|---|---|
| Systems (A) | | | ☐ |
| Art / level design (B) | | | ☐ |

Until both boxes are ticked, only the greybox kit may be built.
