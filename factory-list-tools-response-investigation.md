# Factory 188/22 ListToolsResponse — Wire Layout Investigation

**Date:** 2026-09-21  
**Client:** `FreeRealms.exe` 1.910.1.530630  
**Path:** `%LOCALAPPDATA%\OSFRLauncher\Servers\Sanctuary Local\Client\FreeRealms.exe`  
**ImageBase:** `0x400000` (VA − ImageBase = file offset / RVA)  
**Scope:** Research only. No Sanctuary Factory 188/22 implementation. No farming/Bandit/housing changes.

Confidence labels used below: **PROVEN** / **STRONGLY SUPPORTED** / **UNKNOWN**.

---

## 1. Executive summary

S2C **188/22** `FactoryPacketListToolsResponse` is the packet that fills `BaseClient.Factory.Tools` / Lua `FactoryTools_DS` after the client sends C2S **188/6** `BC000600`.

**LIVE PROVEN:** OpenToolshed `BC001A0000000000` → ListToolsRequest `BC000600` → zero-tool ListToolsResponse `BC00160000000000` works; blank toolshed UI expected.

**PROVEN wire shape (header + body container):**

| Piece | Layout |
|-------|--------|
| Header | `int16` family **188**, `int16` sub **22** → `BC 00 16 00` |
| Body | **Only** a `HashListMap<int, FactoryToolDefinitionNode>`: `int32 count`, then `count` records |
| Per record | `int32` map key → node `+0x54`, then node body, then `uint8` → node `+0x48` |
| Zero tools | `count == 0` → body is exactly `00 00 00 00` → full packet `BC00160000000000` |

**PROVEN (GetData `0xA722E0`):** DS columns map to node object offsets — see §17–19. Lua `PopulateFarmTools` uses `GetData` for `Name` / `IconId` / `ToolId` / charges / useable / tooltip.

**Meanings (follow-up §25):** previously unknown body slots map to `FactoryTools.txt` columns via local `FactoryToolDefinition` filler `0x1059020` + layout match to wire node — **STRONGLY SUPPORTED**. **No fully proven one-tool packet bytes** (blocked on `+0x48` value and live wire=txt proof).

**Safe to implement a guessed retail one-tool response?** **NO.**  
**Safe controlled live experiment?** **YES** for structural trials; exact retail field values remain blocked (see §24–§25).

---

## 2. Proven 188/22 packet header

**PROVEN**

| Offset | Type | Value | Bytes (LE) |
|--------|------|-------|------------|
| 0 | `int16` | 188 | `BC 00` |
| 2 | `int16` | 22 | `16 00` |

Evidence:

- Sanctuary `PacketReaderExtensions`: family 188 / sub 22 → `FactoryPacketListToolsResponse`.
- Client RTTI: `.?AUFactoryPacketListToolsResponse@@` @ VA `0x1B49834`.
- Ctor `0x9E5580`: `push 0x16` → `BaseFactoryPacket` `0x9D90D0` (sets family/`sub` at `+4`/`+8`).
- Family dispatcher `0x9E9D40`: `subopcode - 1` jump table; **sub 22** → handler `0x9E8DE0`.

---

## 3. Exact packet body layout (PROVEN structure)

After `BC 00 16 00`, the body is **only** the HashList at packet object `+0x0C`.

**No other top-level fields** (handler `0x9E8DE0`: base header read `0x9E0340`, then HashList read `0x9E7100` on `packet+0xC`).

```
ListToolsResponse
├── int16 OpCode = 188
├── int16 SubOpCode = 22
└── HashListMap<int, FactoryToolDefinitionNode>   // wire starts here
    ├── int32 count
    └── repeat count times:
        ├── int32 key                    // ToolId (stored at node +0x54)
        ├── FactoryToolDefinitionNode body  // see §4
        └── uint8 flag                   // stored at node +0x48
```

RTTI (**PROVEN** names):

- `HashListMap<int, FactoryToolDefinitionNode nested in FactoryPacketListToolsResponse, $0BAA, …>`
- `HashList<FactoryToolDefinitionNode nested in FactoryPacketListToolsResponse, …>`

Template capacity `$0BAA` demangles as a compile-time bucket/capacity constant (runtime init `memset`/`fill` of `0x400` bytes at list `+0x14`). **Not required for wire encoding.**

---

## 4. Field-by-field table

### 4.1 Top-level / list

| Order | Type/width | Meaning | Evidence | Confidence |
|-------|------------|---------|----------|------------|
| 0 | `int16` | Family 188 | ctor / base read | **PROVEN** |
| 1 | `int16` | Sub 22 | ctor `push 0x16` | **PROVEN** |
| 2 | `int32` | Tool count | `0x9E7100` reads dword → `ebp`; `test ebp; jle` skip | **PROVEN** |
| 3… | records | See below | loop in `0x9E7100` | **PROVEN** |

### 4.2 Per-record map key

| Order | Type/width | Object member | Meaning | Evidence | Confidence |
|-------|------------|---------------|---------|----------|------------|
| 0 | `int32` | node `+0x54` | HashMap **key** (tool lookup key) | `0x9E0730` / `0x9DAE70` store at `+0x54`; list walk uses nodes linked at `+0x4C` | **PROVEN** as map key |

**Important separation:** DS column `ToolId` is **not** read from `+0x54`. GetData case 0 reads **`[node+0x00]`**. Retail almost certainly sets map key == body `+0x00`; that equality is **STRONGLY SUPPORTED** by design, not byte-proven from a live capture.

### 4.3 `FactoryToolDefinitionNode` body (`0x9E1010`) — widths PROVEN; DS-backed names PROVEN where noted

Deserializer writes into the node object starting at `+0x00` (map key remains at `+0x54`; trailing useable flag at `+0x48` is **outside** this function).

| Order | Node offset | Wire type/width | Object member | Eventual UI / DS meaning | Evidence | Confidence |
|-------|-------------|-----------------|---------------|--------------------------|----------|------------|
| 1 | `+0x00` | `int32` | body field 0 | **FactoryTools_DS.ToolId** (GetData case 0: `mov eax,[node]`) | `0xA7236A` | meaning **PROVEN** as DS ToolId; debug also labels “Id” |
| 2 | `+0x04` | `int32` | body field 1 | debug “Housing Inst Id” | handler format `0x9E8F3x` + labels `0x1810A5C` | **STRONGLY SUPPORTED** |
| 3 | `+0x08` | `int32` | body field 2 | **DS.Name** via string-id resolve (GetData case 1) | `0xA72383` reads `[node+0x08]` | **PROVEN** as Name string id input |
| 4 | `+0x0C` | `int32` | body field 3 | **DS.IconId** (GetData case 2) | `0xA723EB`: `mov eax,[node+0x0C]` | **PROVEN** |
| 5 | `+0x10` | `int32` | body field 4 | (none in GetData / debug line) | deser only | width **PROVEN**; meaning **UNKNOWN** |
| 6 | `+0x14` | `int32` | body field 5 | debug “Composite Effect” | debug labels | **STRONGLY SUPPORTED** |
| 7 | `+0x18` | `int32` | body field 6 | debug “Tool Item” | debug labels | **STRONGLY SUPPORTED** |
| 8 | `+0x1C` | `int32` | body field 7 | charge **item id**: `UsesCharges=(!=0)`; `ChargesRemaining`=inventory count lookup of this id | GetData cases 4–5 `0xA72417`/`0xA72446` + helper `0x98F850` | **PROVEN** formula; item-id vs raw-charges naming **STRONGLY SUPPORTED** as inventory item key |
| 9 | `+0x20` | `int32` | body field 8 | **DS.TooltipStringId** | GetData case 6 `0xA72461`: `mov eax,[node+0x20]` | **PROVEN** |
| 10 | `+0x24` | `uint8` bool | body field 9 | | deser `setne` | width **PROVEN**; meaning **UNKNOWN** |
| 11 | `+0x28` | `int32` | body field 10 | | | width **PROVEN**; meaning **UNKNOWN** |
| 12 | `+0x2C` | `uint8` bool | body field 11 | | | width **PROVEN**; meaning **UNKNOWN** |
| 13 | `+0x30` | SoeUtil string | body field 12 | | `int32 length` + bytes (`0x894B10`) | encoding **PROVEN**; semantic **UNKNOWN** |
| 14 | `+0x40` | `int32` | body field 13 | | | width **PROVEN**; meaning **UNKNOWN** |
| 15 | `+0x44` | `uint8` bool | body field 14 | | | width **PROVEN**; meaning **UNKNOWN** |
| 16 | `+0x45` | `uint8` bool | body field 15 | | | width **PROVEN**; meaning **UNKNOWN** |

### 4.4 Trailing per-record flag (after node body)

| Order | Node offset | Wire type | Object member | Eventual UI / DS meaning | Evidence | Confidence |
|-------|-------------|-----------|---------------|--------------------------|----------|------------|
| 17 | `+0x48` | `uint8` bool | flag after body | **DS.IsCurrentlyUseable** (GetData case 3: `add eax,0x48` then bool setter) | `0xA72405`; debug “Useable” | **PROVEN** |

### 4.5 Explicitly NOT assumed on the wire

Do **not** treat as proven wire fields unless listed above:

- Display **Name** text, **Icon** asset path  
- Unlock / requirement / hidden flags from `FactoryTools.txt`  
- Consumable item IDs as required for deserialize  

Local `FactoryTools.txt` is a **separate** client table (see §12–13).

---

## 5. List/vector encoding

**PROVEN** (`0x9E7100`):

1. Clear existing list nodes.  
2. Read **`int32 count`** (little-endian).  
3. If `count <= 0`, stop (empty list is legal).  
4. Else loop `i = 0 .. count-1`:  
   - read `int32` key  
   - allocate/link node (`0x9E0730`), key → `+0x54`  
   - deserialize node body (`0x9E1010`)  
   - read `uint8` → `+0x48` (`0`/`nonzero` → bool)  
5. No sentinel after the last record.  
6. Records are **variable length** only because of the embedded string (`int32 len` + bytes). All other fields are fixed width.

Count width: **`int32`**, appears **before** records.  
Wrapper: HashListMap itself (no extra envelope beyond count + records).

---

## 6. Zero-tool response bytes (PROVEN)

Because `count <= 0` skips the loop, a valid empty list is:

```
BC 00 16 00 00 00 00 00
```

| Bytes | Meaning |
|-------|---------|
| `BC 00` | family 188 |
| `16 00` | sub 22 |
| `00 00 00 00` | count = 0 |

**PROVEN** by deserializer control flow (not live-captured S2C). Live OpenToolshed → ListToolsRequest path is already proven; this is the matching empty S2C body.

---

## 7. Single-tool response structure (structure PROVEN; field values NOT)

Minimum **structurally valid** one-tool body shape is fully determined by §17.  

**Do not treat any filled-in hex as retail-proven** while §15 unknowns remain (see §22).

Earlier all-zero templates in this file are **deserialize experiments only**, not “minimum retail shovel packets.”

---

## 8. Client deserializer addresses / RVAs

ImageBase `0x400000`. RVA = VA − `0x400000`.

| Role | VA | RVA |
|------|-----|-----|
| ListToolsResponse ctor | `0x9E5580` | `0x5E5580` |
| BaseFactoryPacket ctor | `0x9D90D0` | `0x5D90D0` |
| HashList member init (`packet+0xC`) | `0x9E4860` | `0x5E4860` |
| Family-188 inbound dispatch | `0x9E9D40` | `0x5E9D40` |
| Sub 22 jump → handler | `0x9E9E37` → `0x9E8DE0` | |
| ListToolsResponse handler | `0x9E8DE0` | `0x5E8DE0` |
| Base header read | `0x9E0340` | `0x5E0340` |
| HashList deserialize | `0x9E7100` | `0x5E7100` |
| Insert node by key | `0x9E0730` | `0x5E0730` |
| Node key init (`+0x54`) | `0x9DAE70` | `0x5DAE70` |
| Node body deserialize | `0x9E1010` | `0x5E1010` |
| String read helper | `0x894B10` | `0x494B10` |
| Copy list → Factory tools DS | `0x9E6C50` | `0x5E6C50` |
| RTTI `FactoryPacketListToolsResponse` | `0x1B49834` | |
| DS name `BaseClient.Factory.Tools` | `0x1815DCC` | |
| Column strings cluster | `0x1815DF8`…`0x1815E3C` | |
| Debug format / labels | `0x1810A3C` / `0x1810A5C`… | |

Related proven chain (prior RE, still valid):

| Role | VA |
|------|-----|
| OpenToolshed deser | `0x9E05B0` |
| OpenToolshed handler → ShowTools | `0x9E5E00` |
| `Factory.RequestToolData` | `0xBFEC90` |
| ListToolsRequest serialize (header only) | `0x9E6860` path / ctor `0x9D9630` |

---

## 9. Handler / control-flow trace

```
S2C tunneled Factory family
  → dispatch 0x9E9D40 (sub = word after family)
  → sub 22 case → 0x9E8DE0
       ├─ stack-construct ListToolsResponse (0x9E5580)
       ├─ bind read cursor to payload
       ├─ 0x9E0340  read/verify header (188/22)
       ├─ 0x9E7100  deserialize HashList into packet+0xC
       ├─ (debug) iterate nodes; format "Available Tools: %d" / per-tool line
       ├─ resolve UI object; 0x9E6C50 copy nodes into Factory tools store (+0x25C)
       └─ virtual notify → script BaseClient_Factory_Tools_OnDataUpdate
            → FarmingBrowser.PopulateFarmTools
```

Preceding live client path (**PROVEN**):

```
S2C 188/26 OpenToolshed (empty type BC001A0000000000)
  → FactoryHandler.ShowTools
  → FarmingBrowser.ShowFarmTools
  → enable FactoryTools_DS; Factory.RequestToolData
  → C2S 188/6 BC000600
  → (server should reply 188/22)
```

---

## 10. FactoryTools_DS population trace

Three layers (keep separate):

### A. Wire → internal node (`FactoryToolDefinitionNode`)

HashList record → node with key at `+0x54` and body at `+0x00…+0x45`, flag `+0x48`.

### B. Internal store → `BaseClient.Factory.Tools` / native `FactoryToolsDataSource`

Handler copies the linked list into the client Factory tools container (`0x9E6C50`).  
RTTI: `FactoryToolsDataSource`.  
Registered script binding name: **`BaseClient.Factory.Tools`**.

### C. Lua `FactoryTools_DS` → Flash UI

`ScriptsBase.bin` / `@FarmingBrowser.lua`:

- DS alias **`FactoryTools_DS`** ↔ `BaseClient.Factory.Tools`
- Declared columns (**PROVEN** native GetColumnCount=7 + ScriptsBase):  
  `ToolId`, `Name`, `IconId`, `IsCurrentlyUseable`, `ChargesRemaining`, `UsesCharges`, `TooltipStringId`
- `BaseClient_Factory_Tools_OnDataUpdate` → `PopulateFarmTools`
- `PopulateFarmTools` uses `GetDS` / `GetRowCount` / `GetData` / `AddFarmTool` / `GetStringById`

**PROVEN:** 188/22 is what feeds this DS (not OpenToolshed, not EquipTool).

---

## 11. UI fields required to display one tool

From ScriptsBase string pool around `AddFarmTool` / `PopulateFarmTools` (**PROVEN** adjacency at ScriptsBase ~offset 2711068):

Flash `AddFarmTool` is driven with values obtained via `GetData` / `tostring` / `Ui.GetStringById`:

- `Name` (DS column — native GetData case 1 ← node `+0x08` string id)
- `IconId` (DS column — GetData case 2 ← node `+0x0C`)
- `ToolId` (DS column — GetData case 0 ← node `+0x00`)
- `ChargesRemaining` (GetData case 4 ← inventory count for item id at node `+0x1C`)
- `UsesCharges` (GetData case 5 ← `node+0x1C != 0`)
- tooltip text from `Ui.GetStringById(TooltipStringId)` (GetData case 6 ← node `+0x20`)
- gated using `IsCurrentlyUseable` (GetData case 3 ← node `+0x48`)

**Native `FactoryToolsDataSource` GetColumnCount = 7** (`0xA726A0` returns 7):  
`ToolId`, `Name`, `IconId`, `IsCurrentlyUseable`, `ChargesRemaining`, `UsesCharges`, `TooltipStringId`.

Lua binding cluster also lists the five runtime columns plus Name/Icon usage in PopulateFarmTools.

Whether `IsCurrentlyUseable == 0` **hides** the tile vs shows disabled = **UNKNOWN** without a live one-tool trial.

---

## 12. Icon / name metadata: wire DS vs local file

| Data | Source | Confidence |
|------|--------|------------|
| DS `ToolId` | Wire body `int32` at node `+0x00` (GetData case 0) | **PROVEN** |
| HashMap key | Wire `int32` before body → node `+0x54` | **PROVEN** as map key |
| DS `Name` | Wire `int32` at `+0x08` resolved to string in GetData case 1 | **PROVEN** path |
| DS `IconId` | Wire `int32` at `+0x0C` | **PROVEN** |
| DS `TooltipStringId` | Wire `int32` at `+0x20` | **PROVEN** |
| DS `IsCurrentlyUseable` | Wire `uint8` after body → `+0x48` | **PROVEN** |
| DS `UsesCharges` / `ChargesRemaining` | Derived from wire `int32` at `+0x1C` (nonzero flag + inventory count lookup) | **PROVEN** |
| Local `FactoryTools.txt` | Parallel static table (housing 58 farming tools 1–15, etc.); used for definition checks / fallbacks (`FactoryTools.txt` @ `0x187E030`; wide `<Tool definition data not found>`) | **PROVEN** file exists |
| Whether retail wire duplicates txt NAME/ICON/TOOLTIP/CONSUMABLE ids | Same numeric columns exist in txt; likely authors of wire values | **STRONGLY SUPPORTED** alignment; **not** live-captured |

**Revised conclusion:** Name/Icon for Flash **do** come through `FactoryTools_DS` GetData from **wire** node fields. `FactoryTools.txt` remains the offline catalog used to pick **candidate values** for those wire ints (and for requirements/hidden/etc. not proven on this packet).

---

## 13. Relevant Lua / resource references

| Asset | Notes |
|-------|-------|
| `UI\ScriptsBase.bin` | `@FarmingBrowser.lua`, `PopulateFarmTools`, `AddFarmTool`, `FactoryTools_DS`, `ShowFarmTools`, `RequestToolData` |
| `UI\UiModules\Main\wndFarmingBrowser.xml` | Farming browser SWF host |
| `Resources\FactoryTools.txt` | Local tool definitions (housing instance **58** / `FARMING`, tools 1–15) |
| Exe strings | `BaseClient.Factory.Tools`, column names, `<Tool definition data not found>` (wide), `Available Tools: %d`, debug field labels |

---

## 14. Sanctuary git-history findings

**PROVEN (history search, working tree preserved):**

- `PacketReaderExtensions` has named 188/6 and 188/22 since initial commits (`76e683d` / `3a9b6c4`) — **names only**, no serializers.
- **No** historical `FactoryPacketListToolsResponse` class, handler, or struct body in git.
- `git log -S"FactoryPacketListToolsResponse"` / `ListToolsResponse` → only those name-map commits.
- Uncommitted experimental code exists for **OpenToolshed 188/26 only** (`FactoryPacketOpenToolshed`, `!farmtest toolshed`) — explicitly **does not** implement 188/22 or handle 188/6.

Nothing in git history supplies the wire layout recovered here.

---

## 15. Remaining unknowns

1. Retail values / meanings for node `+0x10`, `+0x24`, `+0x28`, `+0x2C`, string `@+0x30`, `+0x40`, `+0x44`, `+0x45` (not read by GetData).  
2. Whether map key (`+0x54`) must equal DS ToolId (`+0x00`) in retail (assumed yes; not live-captured).  
3. Whether `IsCurrentlyUseable == 0` hides vs disables the Flash tile.  
4. Whether zero-filled unknown fields still allow a stable named/icon tile when DS-backed ints are set from `FactoryTools.txt`.  
5. Client **serialize** path for 188/22 (server-only in retail).  
6. Live capture of a real non-empty 188/22 for ground-truth of unknown fields.  
7. Exact EquipTool 188/7 / 188/23 bodies (out of scope beyond minimal relationship).

---

## 16. Safest next controlled live experiment

1. Keep zero-tool reply as baseline (already live-proven).  
2. Do **not** ship a “complete” one-tool serializer as production.  
3. Optional **experiment only**: one HashList record with  
   - map key = body `+0x00` = safest ToolId (**4**)  
   - DS-backed ints aligned to `FactoryTools.txt` row for id 4 (see §20)  
   - `+0x1C = 0` (no charges)  
   - `+0x48 = 1` (useable)  
   - empty string (`len=0`)  
   - **all remaining unknown fields left 0** — not claimed retail  
4. Observe: no fault; `OnDataUpdate`; whether Name/Icon appear; whether unknown zeros matter.  
5. To finish unknowns: live-capture retail 188/22 or reverse any users of `+0x10`/`+0x24`/string/`+0x40`/`+0x44`/`+0x45`.

---

## 17. One-node wire structure (byte-for-byte widths)

After header `BC 00 16 00`:

```
int32 count
repeat count:
  int32 mapKey                 → node+0x54
  int32 f00                    → node+0x00   // DS ToolId
  int32 f04                    → node+0x04   // Housing Inst Id (debug)
  int32 f08                    → node+0x08   // Name string id
  int32 f0C                    → node+0x0C   // IconId
  int32 f10                    → node+0x10   // UNKNOWN
  int32 f14                    → node+0x14   // Composite Effect (debug)
  int32 f18                    → node+0x18   // Tool Item (debug)
  int32 f1C                    → node+0x1C   // charge item id
  int32 f20                    → node+0x20   // TooltipStringId
  uint8 b24                    → node+0x24   // UNKNOWN bool
  int32 f28                    → node+0x28   // UNKNOWN
  uint8 b2C                    → node+0x2C   // UNKNOWN bool
  int32 strLen                 → string at node+0x30
  bytes[strLen]                → string payload
  int32 f40                    → node+0x40   // UNKNOWN
  uint8 b44                    → node+0x44   // UNKNOWN bool
  uint8 b45                    → node+0x45   // UNKNOWN bool
  uint8 useable                → node+0x48   // IsCurrentlyUseable
```

**Record size with `strLen == 0`:**  
`4` (key) + `36` (nine int32s) + `1` + `4` + `1` + `4` (len) + `0` + `4` + `1` + `1` + `1` = **57 bytes** per record.  
One-tool body = `4` (count) + `57` = **61 bytes**. Full packet = **65 bytes**.

---

## 18. Wire → object → DS (PROVEN GetData map)

| DS column (index) | Object offset | Wire field | GetData VA | Notes |
|-------------------|---------------|------------|------------|-------|
| ToolId (0) | `+0x00` | body int32 #1 | `0xA7236A` | `mov eax,[eax]` |
| Name (1) | `+0x08` | body int32 #3 | `0xA72383` | string-id resolve |
| IconId (2) | `+0x0C` | body int32 #4 | `0xA723EB` | raw int |
| IsCurrentlyUseable (3) | `+0x48` | trailing uint8 | `0xA72405` | `add eax,0x48` |
| ChargesRemaining (4) | derived from `+0x1C` | body int32 #8 | `0xA72417` | inventory count via `0x98F850` |
| UsesCharges (5) | derived from `+0x1C` | body int32 #8 | `0xA72446` | `setne` if dword != 0 |
| TooltipStringId (6) | `+0x20` | body int32 #9 | `0xA72461` | raw int |

Vtable: GetColumnCount `0xA726A0` → **7**; GetColumnName `0xA72210`; GetData `0xA722E0`; jump table `0xA724B0`.  
RTTI: `.?AVFactoryToolsDataSource@@` @ `0x1B45270`.

---

## 19. Required vs optional for UI

| Need | Status |
|------|--------|
| Structurally complete node (all widths) | **Required** for deser — omit nothing |
| `+0x00` ToolId + map key | **Required** for identity / DS ToolId |
| `+0x08` Name string id | **Required** for DS Name → Flash name |
| `+0x0C` IconId | **Required** for DS IconId → Flash icon |
| `+0x20` TooltipStringId | **Required** for tooltip path (0 may yield empty tip) |
| `+0x1C` charge item id | **Required** encoding; `0` ⇒ no charges (**PROVEN** formula) |
| `+0x48` useable | **Required** encoding; UI effect of 0 vs 1 **UNKNOWN** |
| `+0x04` / `+0x14` / `+0x18` | Encoded always; UI/DS not proven to need nonzero |
| Unknown fields | Encoded always; values **UNKNOWN** — do not invent |

---

## 20. FactoryTools.txt resolution (candidate values only)

File: `%LOCALAPPDATA%\OSFRLauncher\Servers\Sanctuary Local\Client\Resources\FactoryTools.txt`

Header (PROVEN):  
`HOUSING_INSTANCE_ID^ID^NAME_STRING_ID^ICON_ID^REQUIREMENT_ID^COMPOSITE_EFFECT_ID^TOOL_ITEM_ID^CONSUMABLE_ITEM_ID^TOOLTIP_STRING_ID^...^HIDDEN^FACTORY_CATEGORY^...`

Farming (`58`) rows useful for experiments:

| ID | NAME_STRING_ID | ICON_ID | CONSUMABLE | TOOLTIP_STRING_ID | HIDDEN | Notes |
|----|----------------|---------|------------|-------------------|--------|-------|
| 4 | 31036 | 34958 | 0 | 435253 | 0 | Shovel (icon-proven identity) |
| 6 | 31038 | 34946 | 0 | 435255 | 0 | Axe |
| 8 | 434670 | 35566 | 0 | 435257 | 0 | Pickaxe |

Aligning wire `+0x08/+0x0C/+0x20/+0x1C` (and debug `+0x04=58`, `+0x14=0`, `+0x18=0`) to these columns is **STRONGLY SUPPORTED** by parallel schemas — **not** a live wire proof.

---

## 21. Safest ToolId

**ToolId 4** (Shovel): `CONSUMABLE_ITEM_ID=0`, `HIDDEN=0`, farming category, commonly needed for obstacle clearing, no charge-item dependency.

Alternates: **6** (Axe), **8** (Pickaxe) — same charge-free property. Avoid **14** (`HIDDEN=1`).

---

## 22. Exact one-tool packet bytes

**Not published as proven.** Unknown fields (`+0x10`, `+0x24`, `+0x28`, `+0x2C`, string, `+0x40`, `+0x44`, `+0x45`) have no proven retail values. Zero-fill is an experiment, not evidence.

§7’s earlier all-zero body hex remains a **structural template only**.

---

## 23. EquipTool relationship (minimal)

| Packet | Role |
|--------|------|
| 188/6 + 188/22 | List available tools → fill `FactoryTools_DS` |
| 188/7 Request / 188/23 Response | EquipTool — activate a listed tool (Factory Tool Mode); RTTI `FactoryPacketEquipToolRequest/Response` @ `0x1B49044` / `0x1B49070` |

Bodies of 7/23 are documented in [`factory-equip-tool-investigation.md`](factory-equip-tool-investigation.md) (supersedes this row’s prior UNKNOWN). ListTools does not equip.

---

## 24. Defaults / remaining blocker / next live step

| Topic | Verdict |
|-------|---------|
| Defaults for unknown fields | Local ctor zeros definition fields (**PROVEN**); retail wire values still not live-captured |
| Exact one-tool bytes | **Blocked** — see §25 (meanings resolved; `+0x48` + live wire values remain) |
| Safe live experiment | **YES** — structural one-tool with documented unproven zeros + txt-aligned DS ints (optional); production serializer **NO** |
| Remaining blocker | `IsCurrentlyUseable` value; live proof wire body == ToolId 4 txt row; map key equality |

---

## Appendix A — Separation reminder

| Layer | What it is |
|-------|------------|
| **Wire** | `BC0016` + `int32 count` + records `(int32 mapKey + node body + uint8 useable)` |
| **Internal C++ node** | `FactoryToolDefinitionNode` (body `+0x00…+0x45`, flag `+0x48`, key `+0x54`, next `+0x4C`) |
| **Lua / native DS** | 7 columns: ToolId, Name, IconId, IsCurrentlyUseable, ChargesRemaining, UsesCharges, TooltipStringId |
| **Flash UI** | `AddFarmTool(Name, IconId, ToolId, ChargesRemaining, UsesCharges, tooltipText)` |

DS columns are **not** 1:1 with wire order. Name/Icon **are** DS columns fed from wire ints (`+0x08` / `+0x0C`), not only from `FactoryTools.txt`.

---

## Appendix B — Comparison to proven siblings

| Packet | Body |
|--------|------|
| 188/6 ListToolsRequest | **Empty** after header (`BC000600`) — PROVEN |
| 188/26 OpenToolshed | `int32` string length + bytes; empty = `BC001A0000000000` — PROVEN live |
| 188/22 ListToolsResponse | HashList count + records — **structure PROVEN** this report |
| 188/7 / 188/23 EquipTool | Out of scope; bodies still UNKNOWN |

---

## 25. ToolId 4 Shovel — Exact Record Reconstruction

**Date:** 2026-09-21 (follow-up)  
**Scope:** Research only. No runtime implementation. No Sanctuary process changes.  
**Verdict:** Field **meanings** for all previously unknown body slots are resolved via local `FactoryToolDefinition` ↔ `FactoryTools.txt` column order + layout isomorphism with wire `FactoryToolDefinitionNode`. An **exact retail-proven one-Shovel 188/22 packet is NOT published** — blocked on runtime `IsCurrentlyUseable` value and on live proof that wire body values duplicate the txt row.

### 25.1 Previously unknown body fields (enumerate first)

Doc §§1/15/17 listed **eight** unknown body slots (user “seven” ≈ same set; trailing bools sometimes grouped). GetData does **not** read any of these.

| # | Node offset | Wire width | Prior status | Meaning after this section | Confidence |
|---|-------------|------------|--------------|----------------------------|------------|
| 1 | `+0x10` | `int32` | UNKNOWN | **REQUIREMENT_ID** | **STRONGLY SUPPORTED** (txt col → local struct → wire layout) |
| 2 | `+0x24` | `uint8` bool | UNKNOWN | **DELETE_CONSUMABLES_ON_UNEQUIP** | **STRONGLY SUPPORTED** |
| 3 | `+0x28` | `int32` | UNKNOWN | **NOTIFICATION_TYPE** | **STRONGLY SUPPORTED** |
| 4 | `+0x2C` | `uint8` bool | UNKNOWN | **HIDDEN** | **STRONGLY SUPPORTED** |
| 5 | `@+0x30` | SoeUtil string | encoding PROVEN; semantic UNKNOWN | **FACTORY_CATEGORY** | **STRONGLY SUPPORTED** |
| 6 | `+0x40` | `int32` | UNKNOWN | **NEEDS_FUEL_NOTIFICATION_TYPE** | **STRONGLY SUPPORTED** |
| 7 | `+0x44` | `uint8` bool | UNKNOWN | **IS_BLUEPRINT_STAMPER** | **STRONGLY SUPPORTED** |
| 8 | `+0x45` | `uint8` bool | UNKNOWN | **HIDE_IF_REQUIREMENT_FAILS** | **STRONGLY SUPPORTED** |

**Meanings resolved: 8/8.** Retail **wire values** for these slots are **not** live-captured (see §25.10).

### 25.2 Per-field traces

#### Local type vs wire type (important)

| Type | RTTI (VA) | Role |
|------|-----------|------|
| `FactoryToolDefinition` | `HashListMap<VFactoryToolDefinition,…>` @ `0x1B9CADC` | Client catalog loaded from `FactoryTools.txt` |
| `FactoryToolDefinitionNode` | nested in `FactoryPacketListToolsResponse` @ `0x1B491B4` / `0x1B49738` | Wire HashList element |

**Different C++ types.** Meanings transfer only because layouts match field-for-field (below).

#### Local zero-init ctor `0x1058F20` (**PROVEN** defaults)

Zeros `int32`s at `+0x00…+0x20` and `+0x28`, byte `+0x2C`, SoeUtil string `@+0x30`, `int32 +0x40`, bytes `+0x44`/`+0x45`. (Bool `+0x24` is written by column parse / wire deser; 3-byte pad `+0x25…+0x27` is not on the wire.)

#### Local column filler `0x1059020` (**PROVEN** column→offset order)

Called from FactoryTools.txt loader at `0x1037D6A` after ctor `0x1037D4F` → `0x1058F20`. Header order in `Resources\FactoryTools.txt` maps as:

| Col # | Header | Dest offset | Parse helper |
|------:|--------|-------------|--------------|
| 1 | HOUSING_INSTANCE_ID | `+0x04` | int `0x72D900` |
| 2 | ID | `+0x00` (dest = object base) | int `0x72D900` |
| 3 | NAME_STRING_ID | `+0x08` | int |
| 4 | ICON_ID | `+0x0C` | int |
| 5 | REQUIREMENT_ID | `+0x10` | int |
| 6 | COMPOSITE_EFFECT_ID | `+0x14` | int-like `0x1058EB0` |
| 7 | TOOL_ITEM_ID | `+0x18` | int |
| 8 | CONSUMABLE_ITEM_ID | `+0x1C` | int |
| 9 | TOOLTIP_STRING_ID | `+0x20` | int |
| 10 | DELETE_CONSUMABLES_ON_UNEQUIP | `+0x24` | bool `0x72D550` (`1`/`t`/`T` → 1) |
| 11 | NOTIFICATION_TYPE | `+0x28` | int |
| 12 | HIDDEN | `+0x2C` | bool |
| 13 | FACTORY_CATEGORY | `+0x30` | string `0x793E30` |
| 14 | NEEDS_FUEL_NOTIFICATION_TYPE | `+0x40` | int |
| 15 | IS_BLUEPRINT_STAMPER | `+0x44` | bool |
| 16 | HIDE_IF_REQUIREMENT_FAILS | `+0x45` | bool |

Loader entry pushes path string `FactoryTools.txt` @ `0x187E030` from `0x1037C61`.

#### Wire body deser `0x9E1010` (**PROVEN** widths/order)

Writes the **same** offsets in the **same** order (nine leading `int32`s through `+0x20`, `u8 +0x24`, `int32 +0x28`, `u8 +0x2C`, string `@+0x30` via `0x894B10`, `int32 +0x40`, `u8 +0x44`, `u8 +0x45`). Trailing useable `+0x48` is **outside** this function (HashList `0x9E71BA`).

#### Wire node copy `0x9DFEA0` (**PROVEN** member set)

Copies `+0x00…+0x20`, `+0x24`, `+0x28`, `+0x2C`, string `@+0x30`, `+0x40`, `+0x44`, `+0x45` — same body footprint.

#### Already-known body fields (unchanged)

| Offset | Meaning | Evidence |
|--------|---------|----------|
| `+0x00` | DS ToolId | GetData case 0 |
| `+0x04` | Housing Inst Id | debug format `0x9E8F58` / labels `0x1810A5C` |
| `+0x08` | Name string id | GetData case 1 |
| `+0x0C` | IconId | GetData case 2 |
| `+0x14` | Composite Effect | debug |
| `+0x18` | Tool Item | debug |
| `+0x1C` | charge item id | GetData cases 4–5 |
| `+0x20` | TooltipStringId | GetData case 6 |
| `+0x48` | IsCurrentlyUseable | GetData case 3; **not** a txt column |

### 25.3 ToolId 4 local `FactoryTools.txt` row (**PROVEN** file contents)

Path: `%LOCALAPPDATA%\OSFRLauncher\Servers\Sanctuary Local\Client\Resources\FactoryTools.txt`

Raw row:

```text
58^4^31036^34958^970^0^0^0^435253^0^197^0^FARMING^0^0^0^
```

| Column | Value |
|--------|------:|
| HOUSING_INSTANCE_ID | 58 |
| ID | 4 |
| NAME_STRING_ID | 31036 |
| ICON_ID | 34958 |
| REQUIREMENT_ID | 970 |
| COMPOSITE_EFFECT_ID | 0 |
| TOOL_ITEM_ID | 0 |
| CONSUMABLE_ITEM_ID | 0 |
| TOOLTIP_STRING_ID | 435253 |
| DELETE_CONSUMABLES_ON_UNEQUIP | 0 |
| NOTIFICATION_TYPE | 197 |
| HIDDEN | 0 |
| FACTORY_CATEGORY | FARMING |
| NEEDS_FUEL_NOTIFICATION_TYPE | 0 |
| IS_BLUEPRINT_STAMPER | 0 |
| HIDE_IF_REQUIREMENT_FAILS | 0 |

### 25.4 Name / Icon wire candidates for ToolId 4

| Field | Offset | Txt value | Confidence as **wire** value |
|-------|--------|----------|------------------------------|
| Name string id | `+0x08` | **31036** | STRONGLY SUPPORTED (schema); not live-captured |
| IconId | `+0x0C` | **34958** | STRONGLY SUPPORTED (schema); not live-captured |

### 25.5 Shovel charge item (`+0x1C`)

| Field | Value | Notes |
|-------|------:|-------|
| CONSUMABLE_ITEM_ID / charge item id | **0** | UsesCharges = false; ChargesRemaining path unused |

**PROVEN** as local txt value; wire duplication **STRONGLY SUPPORTED** only.

### 25.6 Tooltip (`+0x20`)

| Field | Value |
|-------|------:|
| TOOLTIP_STRING_ID | **435253** |

Same confidence as §25.4–25.5.

### 25.7 Map key vs body ToolId

| Piece | Offset | Relationship |
|-------|--------|--------------|
| HashMap key | wire `int32` → node `+0x54` | Insert `0x9E0730` / key init `0x9DAE70` |
| DS ToolId | body `int32` → node `+0x00` | GetData case 0 |

**PROVEN** they are separate storage. Retail equality `key == body ToolId == 4` remains **STRONGLY SUPPORTED** (HashMap keyed by tool id; local ID column is 4) — **not** live-captured.

### 25.8 Exact string encoding / record size (**PROVEN** structure)

Wire string helper `0x894B10`:

- Stream: `int32 length` + **exactly `length` bytes** (no on-wire NUL).
- Memory: copies `length` bytes, writes `buffer[length]=0`, stores length at string `+0x08`.
- `length == 0` → empty string object.

**Record size formula (PROVEN):**

`57 + strLen` bytes per HashList record  
(`4` key + `36` nine ints + `1` + `4` + `1` + `4` len + `strLen` + `4` + `1` + `1` + `1` useable).

| Variant | strLen | Record | Body (`count=1`) | Full packet (`BC0016`+body) |
|---------|-------:|-------:|-----------------:|----------------------------:|
| Empty category | 0 | **57** | **61** | **65** |
| `"FARMING"` ASCII | 7 | **64** | **68** | **72** |

`"FARMING"` bytes: `46 41 52 4D 49 4E 47`. Using this on the wire assumes category is sent — **STRONGLY SUPPORTED**, not live-proven.

### 25.9 Constructor / default-state

| Object | VA | Default notes |
|--------|-----|---------------|
| Local `FactoryToolDefinition` | `0x1058F20` | All definition ints/bools 0; empty string `@+0x30` |
| Wire node useable | `0x9D97C3` (related init) | `+0x48` cleared to 0 before HashList writes from stream |
| HashList useable write | `0x9E71A5` / `0x9E71BA` | Truncation → 0; else `setne` from wire `u8` |

**PROVEN:** missing/ truncated useable byte → `IsCurrentlyUseable = 0`.  
**UNKNOWN:** retail value for a listed shovel (depends on player/requirement state; **not** a txt column).

### 25.10 Byte ledger — NOT published as proven

Because this follow-up requires **every** field/value justified at implementable confidence, the ledger is **withheld** as a claimed exact packet.

Conditional reconstruction (txt-aligned definition + `key=4` + `useable=?`) would be:

| Wire slot | Conditional value | Justification gap |
|-----------|-------------------|-------------------|
| map key | 4 | equality not live-proven |
| `+0x00` | 4 | txt ID; wire dup unproven |
| `+0x04` | 58 | txt housing |
| `+0x08` | 31036 | txt name |
| `+0x0C` | 34958 | txt icon |
| `+0x10` | 970 | txt requirement |
| `+0x14` | 0 | txt composite |
| `+0x18` | 0 | txt tool item |
| `+0x1C` | 0 | txt consumable |
| `+0x20` | 435253 | txt tooltip |
| `+0x24` | 0 | txt delete-consumables |
| `+0x28` | 197 | txt notification |
| `+0x2C` | 0 | txt hidden |
| `@+0x30` | `"FARMING"` / len 7 | txt category |
| `+0x40` | 0 | txt needs-fuel notif |
| `+0x44` | 0 | txt blueprint stamper |
| `+0x45` | 0 | txt hide-if-req-fails |
| `+0x48` | **UNKNOWN** | **hard blocker** |

**No exact hex packet line is asserted.**

### 25.11 Static deserializer walkthrough of a **hypothetical** txt-aligned record

Assuming (not proven) `key=4`, body as §25.10, `strLen=7` `"FARMING"`, and **some** useable `u8`:

1. `0x9E8DE0` → header 188/22 OK.  
2. `0x9E7100` reads `count=1`.  
3. Reads map key `4` → `0x9E0730` / `0x9DAE70` → `+0x54=4`.  
4. `0x9E1010` consumes nine `int32`s → `+0x00…+0x20`.  
5. Reads `u8` → `+0x24`; `int32` → `+0x28`; `u8` → `+0x2C`.  
6. Reads `strLen=7`, `0x894B10` copies 7 bytes → string `@+0x30`.  
7. Reads `int32` → `+0x40`; `u8` → `+0x44`; `u8` → `+0x45`.  
8. Reads trailing `u8` → `+0x48` (`0x9E71BA`).  
9. GetData would expose ToolId 4, Name from 31036, Icon 34958, Tooltip 435253, UsesCharges false, ChargesRemaining via item 0.

**Walkthrough status:** structure **PASS**; value-complete retail packet **INCOMPLETE** (blocked on `+0x48` and live wire values).

### 25.12 Safe to implement?

| Question | Answer |
|----------|--------|
| Safe to implement exact one-Shovel 188/22 as retail-faithful? | **NO** |
| Remaining blocker | (1) `IsCurrentlyUseable` (`+0x48`) has **no** txt source and no proven retail value; (2) wire body values == txt row is **STRONGLY SUPPORTED** via layout isomorphism of two **distinct** types, not live-captured; (3) map key == 4 not live-captured |
| What **is** resolved | All eight previously unknown **meanings**; exact string encoding; exact empty/`FARMING` record sizes; full ToolId 4 local catalog row |

**Next evidence that would unlock YES:** live capture of non-empty 188/22 containing ToolId 4, **or** proven server-authoritative serializer / computation rule for `+0x48` plus confirmation wire body bytes match the txt-aligned ledger.

---

## Final Shovel Blockers — Hash Key and Useability

**Date:** 2026-09-21 (narrow final-blocker pass)  
**Scope:** Research only. No runtime code changes. No one-tool packet send/implement. No Sanctuary process stops.  
**Target blockers only:** (1) HashList map key vs body ToolId; (2) trailing `IsCurrentlyUseable` `uint8` / node `+0x48`.

### 1. HashList map key vs body ToolId (instruction-level)

| Fact | VA / evidence | Confidence |
|------|---------------|------------|
| Wire record starts with `int32` key | HashList deser `0x9E7100`: read dword → `[esp+10]`, then `push &key; call 0x9E0730` | **PROVEN** |
| Insert stores key at node `+0x54` | `0x9E0730` → key init `0x9DAE70`: `mov [esi+0x54], [arg]` | **PROVEN** |
| Bucket hash uses `+0x54` (not `+0x00`) | Insert link `0x9E07A8` / copy-insert `0x9E635D`: `mov ecx,[eax+0x54]; and ecx,0xFF` | **PROVEN** |
| Body ToolId is separate `int32` at `+0x00` | Body deser `0x9E1010` first dword → `[edi]`; GetData case 0 `0xA7236A`: `mov eax,[node]` | **PROVEN** |
| No post-deser copy `+0x00 ↔ +0x54` | Scan of factory packet range: key stores only from wire-key init (`0x9DAE70` / `0x9E4F50`); body ToolId only from `0x9E1010` | **PROVEN** |
| List → Factory tools store preserves key | `0x9E6C50`: `lea eax,[esi+0x54]; push eax; push node; call 0x9E62E0` | **PROVEN** |
| DS / UI ToolId is **body** `+0x00`, not map key | GetData case 0; Lua `ToolId` column | **PROVEN** |

**Equality `key == body ToolId`:** **not instruction-enforced.** Client never compares or syncs them.

### 2. SelectTool / EquipTool / FarmingBrowser xrefs (which id?)

| Path | Behavior | Confidence |
|------|----------|------------|
| Script binding `Factory.SelectTool` @ string `0x18280A4` → `0xBFECB0` | Lua number arg → `0x9E8CD0` | **PROVEN** |
| `0x9E8CD0` | Constructs `FactoryPacketEquipToolRequest` (`0x9D96E0`, sub **7**); stores arg at request `+0x0C` (`mov [esp+14], eax` on stack object); sends via `0x9E7E80` | **PROVEN** |
| EquipToolRequest serialize `0x9E7090` | After base header `0x9E6860`, writes **`int32 [obj+0x0C]`** only | **PROVEN** |
| FarmingBrowser `PopulateFarmTools` | Passes DS `ToolId` (GetData → `+0x00`) into Flash `AddFarmTool`; Flash/Lua later can call `Factory.SelectTool` with that ToolId | **STRONGLY SUPPORTED** |
| Housing `SelectTool` string in ScriptsBase (~1640980) | Separate decorate/housing tool path — **not** the ListTools HashList key path | **PROVEN** (distinct string cluster) |

**Conclusion:** Equip/Select uses **DS ToolId (`+0x00`)**. Map key (`+0x54`) is **HashList indexing only** on the client tools list path examined. No Factory-tools `find-by-key` (`cmp [node+0x54], toolId`) was found in `0x9E0000–0x9EA000` (GetData walks `+0x4C` links).

**Retail `key == ToolId == 4`:** **STRONGLY SUPPORTED** (unique per-tool `HashListMap<int,…>` convention; safest experiment) — **not PROVEN** (no equality check; no live capture).

### 3. Full `+0x48` xref (set / read)

**Writers (FactoryToolDefinitionNode / ListTools family, `0x9D9000–0x9EA000`):**

| VA | Op | Role |
|----|-----|------|
| `0x9D97C3` | `mov [esi+0x48], al` (al=0) | Node ctor / related init → **0** |
| `0x9E71A5` | `mov byte [edi+0x48], 0` | HashList deser truncate → **0** |
| `0x9E71BA` | `cmp [stream],0; setne cl; mov [edi+0x48], cl` | HashList deser → **0 or 1** |
| `0x9E5349` / `0x9E5362` | same truncate/`setne` pattern | Sibling HashList useable deser |
| `0x9E692D` / `0x9E6944` | same | Sibling HashList useable deser |
| `0x9E3B0E` | `mov [dst+0x48], [src+0x48]` | Node field copy |

**No writer** in ListTools handler / copy window (`0x9E6C00–0x9E9200`) other than deser sites above. Handler `0x9E8DE0` after HashList: debug print → `0x9E6C50` copy → notify — **does not** recompute `+0x48`.

**Readers:**

| VA | Role |
|----|------|
| `0x9E8F37` | `movzx ebx,[esi+0x48]` → debug “Useable” line |
| `0xA72405` | GetData case 3: `add eax,0x48` → bool helper `0x98DEB0` |
| `0x98DEB0` | `cmp byte [ptr],0` → push string **`"1"`** (`0x180C498`) if nonzero, **`"0"`** (`0x17F7D9C`) if zero |

**Recomputed from REQUIREMENT_ID / local tables?** **No** path found that evaluates requirement into `+0x48` after 188/22. Local `Requirements.txt` only contains IDs **1–43**; shovel txt `REQUIREMENT_ID=970` is **absent** — client cannot derive shovel useability from that file.

### 4. Wire domain of trailing bool

At `0x9E71BA`:

- Wire byte **`0`** → memory `+0x48 = 0` → DS/`tonumber` → **0** / false  
- Wire byte **any nonzero** → `setne` → memory `+0x48 = 1` → **1** / true  
- No other accepted enum; values `2…255` collapse to **1** in memory  

**Both 0 and 1 are protocol-valid.** Truncation / missing byte → **0** (**PROVEN**).

### 5. Authoritative source of useability

| Layer | Source | Confidence |
|-------|--------|------------|
| Live `FactoryTools_DS.IsCurrentlyUseable` | Copied from ListToolsResponse nodes; nodes’ `+0x48` set **only** from S2C wire (or init 0) | **PROVEN** wire-authoritative for DS |
| Client-derived after packet? | No requirement / unlock recompute found on ListTools path | **PROVEN** not client-recomputed post-deser |
| Server decision rule (how retail chooses 0/1) | Not present in client; not in Sanctuary git | **UNKNOWN** |

**Server-authoritative (bit comes from S2C, not client recompute): PROVEN** for the tools DS path. **Retail policy** for when shovel is 1: **UNKNOWN**.

### 6. Shovel-usable retail / tutorial evidence

| Probe | Result |
|-------|--------|
| `FactoryTools.txt` ToolId 4 | `58^4^31036^34958^970^…^HIDDEN=0^FARMING^…` — **no** useable column |
| `Requirements.txt` id **970** | **Missing** (file max id 43) |
| ScriptsBase / exe strings for shovel tutorial unlock → useable | **No** evidence tying tutorial completion to `+0x48` |
| Live/retail 188/22 capture for this character | **None** in this research |

**Shovel=`true` as a legitimate retail state:** **UNKNOWN** (plausible when listed/unlocked, but not evidenced).  
**Historical retail `+0x48` for this character:** **UNKNOWN**.

### 7. Historical Sanctuary / git (`IsCurrentlyUseable` / ListToolsResponse generation)

| Search | Result |
|--------|--------|
| `git log -S"IsCurrentlyUseable"` | **Empty** |
| `git grep IsCurrentlyUseable` (tracked history) | **Empty** |
| `git log -S"ListToolsResponse"` / `FactoryPacketListTools` | Name-map only: `76e683d`, `3a9b6c4` in `PacketReaderExtensions` |
| Working tree | Investigation artifacts + experimental **zero-tool** `FactoryPacketListToolsResponse` only (`count=0`); **no** one-tool / useable generator |

**No historical Sanctuary serializer supplies `+0x48` or map-key policy.**

### 8. Controlled TRUE test — protocol-safe?

| Question | Verdict |
|----------|---------|
| Is wire `useable=1` a valid encoding? | **YES** (**PROVEN** `setne` path) |
| Is `useable=0` valid? | **YES** |
| Does TRUE match historical retail for shovel on this character? | **UNKNOWN** |
| Protocol-safe **local structural** trial (deser + DS + UI observe)? | **YES** — does not violate deserializer; not a claim of retail fidelity |
| Safe as production retail-faithful serializer? | **NO** |

### 9. Exact one-Shovel packet — publishable?

**Gate (this pass):** publish only if map key, all field values, and trailing bool representation are proven.

| Gate | Status |
|------|--------|
| Trailing bool **representation** (0/1) | **PROVEN** |
| Map key **must be** 4 | **STRONGLY SUPPORTED only** |
| All body values == ToolId 4 txt row | **STRONGLY SUPPORTED only** (prior §25) |
| Retail `+0x48` value | **UNKNOWN** |

⇒ **Exact one-Shovel packet: NOT published.** Evidence-backed **retail** packet **cannot** yet be constructed. A labeled experiment (`key=4`, txt-aligned body, `useable=1`) remains structurally deser-valid but **not** proven retail.

### 10. Deserializer walkthrough (conditional experiment shape)

Assuming (not retail-proven) `count=1`, `key=4`, body per §25.10, `strLen=7` `"FARMING"`, trailing `u8∈{0,1}`:

1. `0x9E8DE0` header 188/22  
2. `0x9E7100` `count=1`  
3. Key `4` → `0x9E0730`/`0x9DAE70` → `+0x54=4`  
4. `0x9E1010` body → `+0x00…+0x45`  
5. Trailing u8 → `+0x48` via `setne`  
6. `0x9E6C50` copy; GetData ToolId/Name/Icon/Useable/Tooltip from offsets in §18  

**Walkthrough:** structure **PASS**; retail value-complete packet **INCOMPLETE**.

### 11. Items checklist (1–12)

1. Map key storage/`+0x54` bucketing — **PROVEN**  
2. Body ToolId `+0x00` / DS / EquipTool — **PROVEN** separate from key  
3. `key == ToolId` required — **STRONGLY SUPPORTED**, not PROVEN  
4. `+0x48` writers — init/deser/copy only; **no** post-ListTools recompute — **PROVEN**  
5. `+0x48` readers — debug + GetData → `"0"`/`"1"` — **PROVEN**  
6. Wire domain — 0→false; nonzero→true (stored 1) — **PROVEN**  
7. Both 0 and 1 protocol-valid — **YES**  
8. Server-wire authoritative for DS useable — **PROVEN** (policy UNKNOWN)  
9. Shovel retail useable / tutorial bit — **UNKNOWN**  
10. Git history for generation — **none**  
11. Controlled `useable=1` local protocol test — **YES**; retail-faithful — **NO**  
12. Exact one-Shovel packet publish — **NO** (key equality + retail `+0x48` + live wire=txt still open)

### 12. Final blocker verdict

| Topic | Verdict |
|-------|---------|
| HashList key == ToolId | **STRONGLY SUPPORTED** / not PROVEN |
| `+0x48` meaning | **PROVEN** = `IsCurrentlyUseable` |
| `+0x48` wire encoding | **PROVEN** uint8 bool (0 / nonzero→1) |
| Retail shovel `+0x48` value | **UNKNOWN** |
| Exact retail one-Shovel 188/22 | **Blocked** |
| Remaining blocker | (1) live or otherwise **proven** `key==4` (or proven key policy); (2) **proven** retail `IsCurrentlyUseable` for listed shovel; (3) live proof body ints/string match txt row (still only STRONGLY SUPPORTED from §25) |

**Unlock:** non-empty retail/local capture of 188/22 with ToolId 4 showing key bytes + trailing useable byte (and body), **or** server-side serializer evidence for those two fields.
