# Factory Shovel Visual + Digging Animation — Investigation

**Date:** 2026-09-24  
**Client:** `FreeRealms.exe` 1.910.1.530630  
**Path:** `%LOCALAPPDATA%\OSFRLauncher\Servers\Sanctuary Local\Client\FreeRealms.exe`  
**ImageBase:** `0x400000`  
**Branch / checkpoint:** `farming-prototype` @ `a5581a0`  
**Scope:** Research only. No application code changes. No commit/push/rebuild. No Sanctuary service restart/stop.

Confidence labels: **LIVE-PROVEN** / **PROVEN (static)** / **STRONGLY SUPPORTED** / **UNKNOWN**.

Related prior reports:

- [`factory-equip-tool-investigation.md`](factory-equip-tool-investigation.md) — 188/7 + 188/23 wire + EquipToolResponse handler
- [`factory-list-tools-response-investigation.md`](factory-list-tools-response-investigation.md) — 188/22 ListTools + `FactoryTools.txt` column map

---

## 0. Executive verdict

| Question | Verdict | Label |
|----------|---------|-------|
| Should Shovel appear in hands **immediately** on 188/23 success? | **No** (not from EquipToolResponse alone) | **STRONGLY SUPPORTED** (+ **LIVE-PROVEN** absence) |
| Original held Shovel **model asset** identified? | **Yes as asset name** `tool_ar_ag_weapon_farmingshovel.adr` (and related); **no proven Factory ModelId / TOOL_ITEM_ID** | Asset name **PROVEN**; ID mapping **UNKNOWN** |
| Original digging **animation** identified? | **Yes:** `farm_dig` **id=3900003** (+ long `farm_dig_long`=3900009); GR2s `*_farm_digging*.gr2` | Slot IDs **PROVEN (static)**; retail use-on-rock **STRONGLY SUPPORTED**, not live-captured |
| Required animation **packet / client call** identified? | **Candidate** `PlayerUpdatePacketSetAnimation` with `AnimationId=3900003`; retail Factory obstacle path **UNKNOWN** | Candidate **STRONGLY SUPPORTED**; retail path **UNKNOWN** |
| Ready to implement authentic visuals? | **NO** | — |

---

## 1. Current live-tested Sanctuary behavior (baseline)

**LIVE-PROVEN** (user session / working prototype):

| Step | Result |
|------|--------|
| Tool Shed | Opens via Factory **188/26** |
| List tools | **188/22** shows Shovel `ToolId=4` |
| Click Shovel | C2S **188/7** `ToolId=4` |
| Server reply | **188/23** `Success=1`, `ToolId=4` |
| Server state | Tracks selected Shovel (`SelectedFarmToolId=4`) |
| Rock clear gate | Rocks require Shovel; clear persists across farm exit/reentry |
| Held mesh | Character does **not** visibly hold the Shovel after equip |
| Rock click | Generic interact; rock **immediately despawns**; **no** dig animation |

Prototype rock path is intentionally **not** retail Factory misfortune protocol (`FarmingPrototypeConfig` / `FarmingService` comments). Rocks are `IsInteractable` NPCs cleared via `CommandPacketInteractRequest` → `Npc.OnInteract` → `HandleDebugRockInteract` → `Dispose()` + DB persist.

---

## 2. TASK 1 — Equipped Shovel visual (188/23 handler)

### 2.1 EquipToolResponse handler (recap + extension)

From prior static RE (**PROVEN**), handler VA `0x9E5800` (RVA `0x5E5800`):

| Step | VA / detail | Effect |
|------|-------------|--------|
| Dispatch | Factory jump table → `0x9E5800` | |
| Deserialize | `0x9E04A0` | `uint8 Success` + `int32 ToolId` |
| Success gate | `cmp byte [packet+0x0C], 0` / je skip | Failure = no side effects |
| Set CurrentToolId | `mov [eax+0xC14], ToolId` (`eax=[edi+0x2270]`) | **CurrentToolId ← 4** |
| Tool lookup | `call 0x10343E0(ToolId)` on list `[factory+0x21C]` | Optional |
| UI invoke | `Housing:SetEditButtonToExitToolMode` @ string `0x18108F8` / file off ~`0x1410900` | Tool-mode UI |

**Not present in this handler (PROVEN absence for these calls):**

- Character attachment / wield-slot update
- `PlayerUpdatePacketEquipItemChange` / attachment serialize
- Animation state machine / `farm_dig`
- Composite effect play
- Any follow-up Factory C2S send
- Tool Shed close (not proven in native body)

Getter `0xA72200` / setter `0x9D9A20` only touch `+0xC14`. HUD/charges consumer example `0xA72512` reads CurrentToolId for definition lookup — **not** mesh attach.

### 2.2 FactoryTools catalog for Shovel ToolId=4

**PROVEN** local file  
`%LOCALAPPDATA%\OSFRLauncher\Servers\Sanctuary Local\Client\Resources\FactoryTools.txt`:

```text
58^4^31036^34958^970^0^0^0^435253^0^197^0^FARMING^0^0^0^
```

| Column | Value | Relevance to held mesh |
|--------|------:|------------------------|
| COMPOSITE_EFFECT_ID | **0** | No equip-time effect from catalog |
| TOOL_ITEM_ID | **0** | No inventory item id to attach from catalog |
| ICON_ID | 34958 | Tool Shed / DS icon (UI), not 3D prop |
| NOTIFICATION_TYPE | 197 | Maps to `NotificationImages.txt` row `197^9374^…` — **UI notification image**, not Models.txt 197 |

**PROVEN:** Every farming tool row (ids 1–15) has `COMPOSITE_EFFECT_ID=0` and `TOOL_ITEM_ID=0`. Equip-time held mesh is **not** driven by those FactoryTools columns for farming tools.

Sanctuary experimental ListTools node mirrors these zeros (`FactoryToolDefinitionNode.CreateExperimentalShovel`).

### 2.3 Immediate vs action-time visibility

| Hypothesis | Assessment |
|------------|------------|
| 188/23 alone should spawn held Shovel | **Contradicted** by handler body + TOOL_ITEM/COMPOSITE=0 + **LIVE-PROVEN** no visible shovel after successful equip |
| Shovel mesh appears only during dig / tool-use action | **STRONGLY SUPPORTED** by separate `farm_dig` animation system + dedicated farming shovel ADR assets (see §3) |
| Cursor / Exit-Tool-Mode UI changes on equip | **PROVEN** intent via `SetEditButtonToExitToolMode`; live UI details still partly **UNKNOWN** |

**Answer to Task 1:** The client is **not** expected to put a Shovel model in the character’s hands solely from a successful **188/23**. Equip selects Factory Tool Mode (`CurrentToolId=4`). Visible shovel is expected (if at all in retail) in a **later tool-use / dig** path, not at selection time.

---

## 3. TASK 2 — Original Shovel assets

Evidence sources: client `Assets_manifest.txt`, `Resources\FactoryTools.txt`, `Resources\AnimationTypes.xml`, `Resources\Models.txt`, Sanctuary `src\Resources\Models.txt` / `ClientItemDefinitions.json`. IDs reported **only** when evidenced.

### 3.1 3D shovel / tool meshes (asset names — PROVEN)

| Asset | Role | Notes |
|-------|------|-------|
| `tool_ar_ag_weapon_farmingshovel.adr` (+ `.dma` / `.dme` / `.dsk`) | **Farming-specific** shovel actor | Present in Assets_manifest; **not** found as `ModelName` in `ClientItemDefinitions.json` this pass |
| `farming_ar_ag_weapon_shovel.dds` | Farming shovel texture | Manifest |
| `tool_ar_ag_weapon_shovel.adr` | Generic/miner shovel weapon | Used by many inventory “Shovel” items (e.g. Id 1910, Slot **7**) |
| `icon_farming_shovel_{32,64,128}.dds` | Farming UI icons | Manifest; likely Tool Shed tile art for ICON_ID 34958 (**STRONGLY SUPPORTED**, not file-proven ID→DDS map this pass) |
| `icon_item_tool_ar_ag_weapon_farmingshovel_freestyle-farming-M_*.dds` | Freestyle farming shovel icons | Manifest |
| `sh_shovel_01.adr` | World / NPC prop | **Models.txt Id 197**; Npcs.json “Trusty Shovel” — **not** Factory ToolId 4 held tool |

**Not proven:** a numeric **Models.txt ModelId** for `tool_ar_ag_weapon_farmingshovel.adr`. Closest related Models entry is `3919^tool_ar_ag_weapon_bugsprayer.adr` (“weapon animation test”) — **do not** treat as shovel.

### 3.2 Digging animation assets (PROVEN)

**AnimationTypes.xml** (`Resources\AnimationTypes.xml`, Farm branch, priority 100):

| Slot name | Animation id | Notes |
|-----------|-------------:|-------|
| `farm_dig` | **3900003** | Primary dig |
| `farm_dig_long` | **3900009** | Long dig |
| `farm_hoe` | 3900002 | Hoe (not shovel) |
| `farm_pick` | 3900011 | Pickaxe |
| `farm_tree_chop` | 3900005 | Axe/chop |
| `farm_tree_saw` / `_long` | 3900006 / 3900010 | Saw |

**GR2 motion files (Assets_manifest — PROVEN names):**

- `human_m_farm_digging.gr2` / `human_m_farm_digging_long.gr2`
- `human_f_farm_digging.gr2` / `human_f_farm_digging_long.gr2`
- `fairy_m_farm_digging.gr2` / `fairy_m_farm_digging_long.gr2`
- `fairy_f_farm_digging.gr2` / `fairy_f_farm_digging_long.gr2`

`AnimationGroups.xml` has **no** `farm_dig` group entries (only unrelated `pet_digging` id 5525). Character farm anims are typed via AnimationTypes slots, not that pet group.

**Exe strings:** ASCII `farm_dig` was **not** found inside `FreeRealms.exe` (slot names live in AnimationTypes.xml / packs). RTTI/name strings for EquipTool packets exist at file offsets ~`0x1749048` / `0x1749074`.

### 3.3 Effects (PROVEN names; shovel-equip link UNKNOWN)

From `ActorCompositeEffectDefinitions.xml` (farming tool-related):

| Effect name | Id | Relevance |
|-------------|---:|-----------|
| `WFX_fruit_multi_farming-tool-attack_loop` | **16777** | Generic farming tool attack VFX — **not** proven tied to Shovel ToolId 4 |
| `WFX_farming-tool_clouds_red_bugsprayer_loop` | 16780 | Sprayer |
| `WFX_farming-tool_fungus_green_fungalsprayer_loop` | 16782 | Fungal sprayer |

Shovel `COMPOSITE_EFFECT_ID=0` ⇒ catalog does **not** select these on equip.

### 3.4 Inventory shovel vs Factory shovel

| System | Evidence |
|--------|----------|
| Inventory miner shovels | `ClientItemDefinitions` “Shovel” items use `ModelName: tool_ar_ag_weapon_shovel.adr`, `Slot: 7` — **PROVEN** |
| Equipment slot 7 | `EquipmentSlotDefinitions.txt`: id **7** = **Weapon**, `IS_WEAPON=1` — **PROVEN** (miner shovel slot; Factory bind still **UNKNOWN**) |
| Factory ToolId 4 | `TOOL_ITEM_ID=0` — **PROVEN**; zero `ClientItemDefinitions` hits for `farmingshovel` this pass |
| Attachment packet | Sanctuary already has `PlayerUpdatePacketEquipItemChange` + `CharacterAttachmentData` (`ModelName`, `Slot`, `CompositeEffectId`) — usable for **experiments**, **not** proven retail Factory equip path |

**Do not guess** a Factory TOOL_ITEM_ID or Models.txt id for the farming shovel. Asset **names** are proven; Factory→attachment **binding** is **UNKNOWN**.

---

## 4. TASK 3 — Rock-clearing animation / packet paths

### 4.1 Current Sanctuary path (generic NPC interact)

**PROVEN (code) + LIVE-PROVEN (behavior):**

```
Client click rock (IsInteractable NPC)
  → CommandPacketInteractRequest (BaseCommandPacket)
  → CommandPacketInteractRequestHandler
  → entity.OnInteract(player)
  → Npc.InteractAction = HandleDebugRockInteract
  → Shovel gate (SelectedFarmToolId == 4)
  → rock.Dispose() + PersistObstacleCleared
```

No animation packet is sent. No Factory misfortune packet is involved. Config explicitly: “NOT retail Factory 188”.

### 4.2 Named Factory opcodes (Sanctuary name map)

`PacketReaderExtensions` Factory 188 subopcodes include ListTools / EquipTool / OpenToolshed / blueprint / plot / experience / upsell — **no** `UseTool`, `ClearObstacle`, `ClearMisfortune`, or similar names.

Client rdata (prior equip report + this pass): `Misfortune` table filenames (`FactoryMisfortunes.txt`, `FactoryMisfortuneGroups.txt`), `ExitToolMode`, `SelectTool`, `RequestToolData`, `IsInFactory` — but **no** proven `UseTool` / `ClearObstacle` string for a C2S Factory subopcode.

### 4.3 Factory misfortunes tables (static)

`FactoryMisfortunes.txt` columns: `ID`, `MISFORTUNE_TYPE`, `FOUNDATION_CATEGORY`, `TYPE_BASED_ID`, …  

`TYPE_BASED_ID` values like 1000–1011 are **not** the same as Models.txt ids 1000–1011 (those are town signs). Exact meaning of `TYPE_BASED_ID` for rock obstacles remains **UNKNOWN** without further RE / live capture.

Prototype rock visual uses Models.txt **3441** `farming_obstacles_rockpatch_big_01.adr` — **PROVEN** as obstacle art, **not** proven as a Factory misfortune `TYPE_BASED_ID`.

### 4.4 How retail likely plays dig (hypothesis tiers)

| Mechanism | Confidence |
|-----------|------------|
| Server sends **PlayerUpdate** animation (`SetAnimation` / `QueueAnimation` / sync) with `AnimationId=3900003` (`farm_dig`) during tool use | **STRONGLY SUPPORTED** as the client’s farm dig slot; Sanctuary already implements `PlayerUpdatePacketSetAnimation` (op 8) for other systems |
| Dig GR2 / tool ADR attach shovel mesh for duration of anim | **STRONGLY SUPPORTED** (assets exist; TOOL_ITEM=0 implies non-inventory attach) |
| Client locally plays dig from CurrentToolId on misfortune click without S2C anim | **UNKNOWN** |
| Dedicated Factory S2C “start tool use” packet beyond 188/23 | **UNKNOWN** — additional Factory protocol reconstruction may be required |
| Generic `CommandPacketInteractRequest` is the retail Factory rock path | **Contradicted** for authentic Factory objects; it is only the prototype path |

### 4.5 Distinction summary

| | Prototype rock (current) | Authentic Factory obstacle (retail) |
|--|--------------------------|-------------------------------------|
| Entity | Debug NPC, Models 3441 | Factory misfortune / Factory entity (**UNKNOWN** spawn packet) |
| Click packet | `CommandPacketInteractRequest` | **UNKNOWN** (possibly Factory C2S, possibly interact with different server handling) |
| Tool check | Server `SelectedFarmToolId` | Client `CurrentToolId` + server authority (**UNKNOWN** exact exchange) |
| Visual | Instant despawn | Expected dig anim ± shovel prop (**not** live-captured here) |
| Persist | `DbFarmObstacle` | Factory plot / misfortune state (**UNKNOWN**) |

---

## 5. TASK 4 — Minimal safe implementation plan (research only)

### 5.1 What is **not** ready

- Authentic Factory misfortune click/clear protocol (wire layout **UNKNOWN**)
- Proven Factory binding from ToolId 4 → held mesh (`TOOL_ITEM_ID=0`)
- Live proof that `PlayerUpdatePacketSetAnimation(3900003)` alone shows shovel prop
- Proof of which effect id (if any) accompanies dig

### 5.2 Smallest **controlled experiment** (if later authorized — do not implement now)

Preserve crops, obstacles gating, Tool Shed, Shovel selection, Bandit, housing.

**Experiment A — dig anim only on prototype rock clear (lowest risk to Factory protocol):**

1. Keep existing Shovel gate + persist + despawn.
2. Before `Dispose()`, send tunneled `PlayerUpdatePacketSetAnimation` to player (and visible):
   - `Guid = player.Guid`
   - `AnimationId = 3900003` (`farm_dig`) — **PROVEN** slot id
   - `Flags` = play-now (not base/idle bit) per existing Sanctuary comments
3. Optional short delay before despawn so anim is visible.
4. Observe: dig body motion? shovel mesh? neither?

**Experiment B — only if A shows dig without shovel:**

1. Temporary `PlayerUpdatePacketEquipItemChange` with `Attachment.ModelName = "tool_ar_ag_weapon_farmingshovel.adr"` and a candidate Slot (inventory shovels use Slot **7** — **PROVEN** for miners, **UNKNOWN** for Factory).
2. Remove attachment after anim.
3. Label as **non-retail guess**; revert if wrong.

**Do not:**

- Invent Factory TOOL_ITEM_ID / COMPOSITE_EFFECT_ID nonzero values without evidence
- Replace rock interact with unproven Factory opcodes
- Use `sh_shovel_01` / Model 197 as held tool
- Use combat/miner shovel item ids as “Factory Shovel” without proof

### 5.3 Next evidence that unlocks authentic implementation

1. **Live packet capture** on retail (or closest authentic) Factory farm: equip Shovel → click rock/misfortune → record C2S/S2C until obstacle removes (Factory family 188 and PlayerUpdate anim/effect/attach).
2. **Static RE:** xrefs to CurrentToolId getter `0xA72200` beyond HUD; misfortune click handler; any attach using tool definition `+0x18` (TOOL_ITEM) even when 0.
3. **Unpack / inspect** `tool_ar_ag_weapon_farmingshovel.adr` and `human_*_farm_digging.gr2` for embedded prop references.
4. Optional: binary watch of attachment list when forcing `SetAnimation(3900003)` in a disposable debug build.

---

## 6. Why current Shovel selection has no visible effect

| Cause | Label |
|-------|-------|
| 188/23 handler only sets `CurrentToolId` + Exit-Tool-Mode UI | **PROVEN (static)** |
| FactoryTools Shovel `TOOL_ITEM_ID=0`, `COMPOSITE_EFFECT_ID=0` | **PROVEN** |
| No Sanctuary code sends dig animation or tool attachment on equip or rock clear | **PROVEN (code)** |
| Rock clear instantly despawns via generic interact | **LIVE-PROVEN** |
| Retail held-shovel path (if any) not yet reconstructed | **UNKNOWN** |

Server “selected Shovel” is logical gating only — it does not drive client mesh/anim by itself.

---

## 7. Address / RVA quick reference

| Item | VA | RVA / note |
|------|-----|------------|
| EquipToolResponse handler | `0x9E5800` | `0x5E5800` |
| EquipToolResponse deser | `0x9E04A0` | `0x5E04A0` |
| CurrentToolId getter | `0xA72200` | `0x672200` |
| CurrentToolId setter | `0x9D9A20` | `0x5D9A20` |
| SelectTool send | `0x9E8CD0` | `0x5E8CD0` |
| `Housing:SetEditButtonToExitToolMode` | string ~`0x18108F8` | file ~`0x1410900` |
| RTTI EquipTool Request/Response names | file ~`0x1749048` / `0x1749074` | |

---

## 8. Confidence summary

| Claim | Label |
|-------|-------|
| Live 188/23 success does not show held Shovel | **LIVE-PROVEN** |
| 188/23 does not attach mesh / play dig | **PROVEN (static)** |
| Immediate held shovel is not required for correct EquipTool | **STRONGLY SUPPORTED** |
| `farm_dig` = 3900003; GR2 `*_farm_digging*` exist | **PROVEN (static)** |
| `tool_ar_ag_weapon_farmingshovel.adr` is farming shovel mesh asset | **PROVEN (static)** name |
| Factory ToolId 4 → that ADR via TOOL_ITEM_ID | **UNKNOWN** (field is 0) |
| Retail rock clear uses `SetAnimation(3900003)` | **STRONGLY SUPPORTED** candidate; **not** LIVE-PROVEN |
| Retail Factory obstacle C2S/S2C layout | **UNKNOWN** |
| Prototype rock = CommandPacketInteractRequest | **PROVEN** |

---

## 9. Preserve / non-goals

**Preserve:** crops, obstacles, Tool Shed, Shovel selection + shovel-required rock clearing, Bandit, housing, all uncommitted work.

**Non-goals this report:** implementation, commits, rebuilds, service restarts, guessed IDs, substituting unrelated animations.

---

## 10. Ready to implement?

**NO** — for authentic Factory shovel visuals / dig protocol.

**YES (later, if explicitly requested)** — only for a **labeled experimental** dig-anim (and maybe temporary attachment) on the **existing prototype rock clear**, without claiming retail Factory fidelity, after accepting residual unknowns in §5.3.
