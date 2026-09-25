# Factory 188/7 EquipToolRequest + 188/23 EquipToolResponse — Wire Layout Investigation

**Date:** 2026-09-21  
**Client:** `FreeRealms.exe` 1.910.1.530630  
**Path:** `%LOCALAPPDATA%\OSFRLauncher\Servers\Sanctuary Local\Client\FreeRealms.exe`  
**ImageBase:** `0x400000` (VA − ImageBase = file offset / RVA for `.text`)  
**Scope:** Research only. No Sanctuary EquipTool implementation. No runtime/process changes. No crop/obstacle/Bandit/housing/Login/WebAPI changes.

Confidence labels: **LIVE-PROVEN** / **PROVEN (static)** / **STRONGLY SUPPORTED** / **UNKNOWN**.

Related prior report: [`factory-list-tools-response-investigation.md`](factory-list-tools-response-investigation.md) (188/22 ListToolsResponse; EquipTool briefly noted in §23 as bodies UNKNOWN — superseded here).

---

## 1. Executive summary

| Item | Verdict |
|------|---------|
| C2S **188/7** EquipToolRequest | Header + `int32 ToolId` only; ends immediately after ToolId |
| Live Shovel click | `BC00070004000000` (**LIVE-PROVEN**) |
| S2C **188/23** EquipToolResponse | Header + `uint8 Success` + `int32 ToolId` — **9 bytes total** |
| Exact Shovel success response | `BC0017000104000000` — **PROVEN (static)** from client deser + handler |
| Visible 3D Shovel from 188/23 alone | **NOT PROVEN** — packet sets CurrentToolId + invokes tool-mode UI method; shovel `TOOL_ITEM_ID=0` / `COMPOSITE_EFFECT_ID=0` locally |
| Sanctuary handler for 188/7 | Absent (unhandled) |
| Safe controlled live response experiment | **YES** |

---

## 2. Live 188/7 capture

**LIVE-PROVEN** (user session after one-Shovel 188/22 populated authentic Tool Shed):

1. Server 188/26 OpenToolshed  
2. Client 188/6 ListToolsRequest  
3. Server 188/22 ListToolsResponse count=1 Shovel ToolId=4 → authentic Shovel tile  
4. User clicks Shovel  
5. Client sends tunneled: `05000108000000BC00070004000000`  
6. Inner Factory packet after tunnel unwrap: **`BC 00 07 00 04 00 00 00`**

Sanctuary has no handler for subopcode 7 → unhandled.

---

## 3. Exact 188/7 request wire structure

| Offset | Hex (Shovel) | Type | Value | Meaning | Confidence |
|--------|--------------|------|-------|---------|------------|
| 0 | `BC 00` | `int16` LE | 188 | Factory family | LIVE-PROVEN + PROVEN |
| 2 | `07 00` | `int16` LE | 7 | EquipToolRequest | LIVE-PROVEN + PROVEN |
| 4 | `04 00 00 00` | `int32` LE | 4 | ToolId (Shovel) | LIVE-PROVEN + PROVEN |
| 8 | — | — | — | **Packet ends** | LIVE-PROVEN + PROVEN |

**Full live Shovel request:** `BC00070004000000`  
**Size:** 8 bytes.

No strings, lists, bools, character/factory/plot IDs, or trailing bytes.

---

## 4. 188/7 client / static confirmation

| Symbol / role | VA | Notes | Confidence |
|---------------|-----|-------|------------|
| RTTI `.?AUFactoryPacketEquipToolRequest@@` | `0x1B49044` | | PROVEN |
| Ctor | `0x9D96E0` | `push 7` → `BaseFactoryPacket` `0x9D90D0`; vptr `0x18104BC`; zeros `int32 [this+0x0C]` | PROVEN |
| Serialize | `0x9E7090` | Base ser `0x9E6860` then writes **only** `int32 [obj+0x0C]` | PROVEN |
| Deserialize | `0x9E0510` | Base deser `0x9E0340` then reads **only** `int32` → `[obj+0x0C]` | PROVEN |
| Lua `Factory.SelectTool` string | `0x18280A4` | | PROVEN |
| Script binding | `0xBFECB0` | Lua number → Factory object → `0x9E8CD0` | PROVEN |
| Send path | `0x9E8CD0` | Stack-ctor request (`call 0x9D96E0`); stores arg at request `+0x0C`; send helper `0x9E7E80` | PROVEN |

**Object layout (request):**

| Offset | Field |
|--------|-------|
| `+0x00` | vtable |
| `+0x04` | family (188) |
| `+0x08` | subopcode (7) |
| `+0x0C` | `int32` ToolId |

**Client-side state when sending:** ToolId copied from Lua `SelectTool` argument (DS/Flash ToolId from ListTools body `+0x00`, not HashList map key) into request `+0x0C`. No other request fields written before serialize.

---

## 5. Exact 188/23 response wire structure

Header on wire: `BC 00 17 00` (family 188, sub 23).

**What follows `BC 00 17 00` (PROVEN by deserializer `0x9E04A0`):**

| Wire order | Type | Width | Object dest | Semantic | Confidence |
|------------|------|-------|-------------|----------|------------|
| 1 | `uint8` bool (normalized) | 1 | `[obj+0x0C]` | Success / accept equip | PROVEN |
| 2 | `int32` LE | 4 | `[obj+0x10]` | ToolId (stored as CurrentToolId on success) | PROVEN |

**No** additional fields: no strings, lists, nested structs, character/factory/plot IDs, animation IDs, or padding on the wire between bool and ToolId.

**Exact packet length:** **9 bytes** (4 header + 1 + 4).

### 5.1 Bool normalization (PROVEN)

At `0x9E04CD`–`0x9E04D3`:

- Wire `0` → memory `+0x0C = 0`  
- Wire **any nonzero** → `setne` → memory `+0x0C = 1`  

Truncation / missing bool → `+0x0C = 0` and stream error flag.  
Truncation / missing ToolId → `+0x10 = 0`.

### 5.2 It is **not** “echo ToolId only”

A response that is only `BC00170004000000` (header + ToolId, **no** bool) would be mis-parsed: first body byte `04` becomes Success=true, then next 4 bytes consumed as ToolId (would read past/wrong). **Bool is required and comes first.**

---

## 6. Field table (188/23)

| # | Wire off | Type | Dest | Meaning | Required values for Shovel | Confidence |
|---|----------|------|------|---------|----------------------------|------------|
| 0 | 0 | `int16` | `+0x04` | Family 188 | `BC 00` | PROVEN |
| 1 | 2 | `int16` | `+0x08` | Sub 23 | `17 00` | PROVEN |
| 2 | 4 | `uint8` | `+0x0C` | Success | `01` (nonzero) | PROVEN |
| 3 | 5 | `int32` | `+0x10` | ToolId | `04 00 00 00` | PROVEN |

Ctor `0x9D97F0`: `push 0x17`; zeros byte `+0x0C` and dword `+0x10`; vptr `0x18104C4`.

---

## 7. Response handler control flow

| Step | VA | Behavior | Confidence |
|------|-----|----------|------------|
| Dispatch | `0x9E9E46` → handler | Factory subopcode jump table routes to EquipToolResponse handler | PROVEN |
| Handler entry | `0x9E5800` | `this` = Factory client obj in `edi` | PROVEN |
| Construct | `0x9E5847` → `0x9D97F0` | Stack `FactoryPacketEquipToolResponse` | PROVEN |
| Deserialize | `0x9E5887` → `0x9E04A0` | Header + bool + ToolId | PROVEN |
| Fail if deser error flag | `cmp [esp+20], 0` / jne | Abort | PROVEN |
| **Success gate** | `cmp byte [packet+0x0C], 0` / **je skip** | **`+0x0C == 0` → no equip side effects** | PROVEN |
| Set CurrentToolId | `mov [eax+0xC14], ToolId` with `eax=[edi+0x2270]` | **CurrentToolId = response ToolId** | PROVEN |
| Lookup tool | `call 0x10343E0(ToolId)` using list at `[factory+0x21C]` | Hash/list find by id | PROVEN |
| If lookup fails | `je` skip UI block | Still left CurrentToolId set | PROVEN |
| If lookup ok | build string / push tool field `[found+0x0C]` | | PROVEN |
| UI / Lua invoke | `push 0x18108F8`; `call 0x9797A0` | String **`Housing:SetEditButtonToExitToolMode`** | PROVEN |

**Getter** for CurrentToolId: `0xA72200` = `mov eax, [ecx+0xC14]; ret`.  
**Setter** helper: `0x9D9A20` = `mov [ecx+0xC14], arg`.  
DS/HUD consumer example: `0xA72512` loads `[…+0xC14]` then resolves tool definition (charges / tooltip path).

**Not observed in this handler (UNKNOWN / not proven here):**

- Explicit Tool Shed close call  
- Cursor/mouse mode native set  
- Character animation state machine call  
- Attach shovel model / wield slot  
- Sending another Factory C2S packet  
- Waiting for a follow-up S2C packet  

---

## 8. Success / failure semantics

| Wire Success | Memory `+0x0C` | Handler behavior | Confidence |
|--------------|----------------|------------------|------------|
| `0` | 0 | **Skip** CurrentToolId update and UI invoke | PROVEN |
| `1`…`255` | 1 | Enter success path | PROVEN |

There is **no** separate result enum beyond this bool.  
“Equip succeeded” from the client’s perspective on this packet = **Success nonzero**, then CurrentToolId ← ToolId, then optional tool-def lookup + `Housing:SetEditButtonToExitToolMode`.

Failure (Success=0) is silent in this handler (no error string push found on the skip path).

---

## 9. Shovel-specific values (ToolId=4)

| Field | Required value | Proof |
|-------|----------------|-------|
| Success | nonzero; canonical **`01`** | Handler requires `+0x0C != 0`; `setne` normalizes to 1 |
| ToolId | **`4`** | Live request ToolId=4; handler stores response `+0x10` as CurrentToolId; ListTools/DS Shovel id=4 |

Handler does **not** compare response ToolId to the pending request; it trusts the S2C value. For a legitimate Shovel equip matching the live click, ToolId **must** be 4 (otherwise CurrentToolId would be wrong).

Local `FactoryTools.txt` Shovel row (catalog, not wire):

```text
58^4^31036^34958^970^0^0^0^435253^0^197^0^FARMING^0^0^0^
```

`COMPOSITE_EFFECT_ID=0`, `TOOL_ITEM_ID=0` for ToolId 4.

---

## 10. What creates visible equipped-tool state?

| Mechanism | Role of 188/23 | Confidence |
|-----------|----------------|------------|
| Set `CurrentToolId` (`+0xC14`) | **Yes — directly** | PROVEN |
| Invoke `Housing:SetEditButtonToExitToolMode` | **Yes — on success + successful tool lookup** | PROVEN |
| Attach 3D shovel / Tool Item model | **Not shown in handler**; local shovel Tool Item / Composite Effect are **0** | STRONGLY SUPPORTED that 188/23 alone is acknowledgment + tool-mode UI/state; **UNKNOWN** whether any separate packet/system creates a held shovel mesh for ToolId 4 |
| FactoryTools_DS update | **No** — DS filled by 188/22 | PROVEN (prior report) |

**First controlled response milestone to expect:**

1. Client accepts packet (no deser abort)  
2. CurrentToolId becomes 4  
3. Tool-mode exit-button UI path may run (if tool lookup hits)  
4. **Do not** require a visible held Shovel mesh as proof of correct 188/23 alone  

---

## 11. Expected UI behavior after successful response

**PROVEN / STRONGLY SUPPORTED:**

- Equip side effects run only if Success ≠ 0  
- CurrentToolId = 4  
- Attempt to switch housing/farming UI into “exit tool mode” button state via named method  

**UNKNOWN (need live observe):**

- Whether Tool Shed Flash UI closes automatically  
- Cursor icon change  
- Character shovel prop visibility  
- Whether farming HUD highlights ToolId 4  

---

## 12. Relationship to obstacle / misfortune / tool-use

- Rdata contains **`Misfortune`** strings (`0x187E05F`, `0x187E077`) and **`ExitToolMode` / `SetEditButtonToExitToolMode`**.  
- **No** static proof in this pass that CurrentToolId=4 changes the next click packet on a Factory misfortune/obstacle (no `UseTool` / `ClearObstacle` / `RemoveObstacle` rdata hits).  
- **Verdict:** Whether obstacle click after equip sends another Factory opcode, a generic interaction, or something else is **UNKNOWN without a live equipped-tool test**. Do not implement obstacle clearing from this report.

---

## 13. Relevant executable addresses / RVAs

| Item | VA | RVA / file (`.text`) |
|------|-----|----------------------|
| EquipToolRequest ctor | `0x9D96E0` | `0x5D96E0` |
| EquipToolRequest ser | `0x9E7090` | `0x5E7090` |
| EquipToolRequest deser | `0x9E0510` | `0x5E0510` |
| EquipToolResponse ctor | `0x9D97F0` | `0x5D97F0` |
| EquipToolResponse deser | `0x9E04A0` | `0x5E04A0` |
| EquipToolResponse handler | `0x9E5800` | `0x5E5800` |
| BaseFactory deser | `0x9E0340` | `0x5E0340` |
| BaseFactory ser | `0x9E6860` | `0x5E6860` |
| SelectTool send | `0x9E8CD0` | `0x5E8CD0` |
| SelectTool Lua bind | `0xBFECB0` | `0x7FECB0` |
| CurrentToolId getter | `0xA72200` | `0x672200` |
| CurrentToolId setter | `0x9D9A20` | `0x5D9A20` |
| RTTI Request / Response | `0x1B49044` / `0x1B49070` | |
| `Housing:SetEditButtonToExitToolMode` | string `0x18108F8` / label region `0x1810900` | |

---

## 14. Sanctuary + git-history findings

| Search | Result |
|--------|--------|
| `PacketReaderExtensions` name map | `7 → FactoryPacketEquipToolRequest`, `23 → FactoryPacketEquipToolResponse` since initial commits |
| `git log -S EquipTool` / Request / Response | **Name-map only** — no packet class, serializer, or handler implementation ever committed |
| Working tree | Comments explicitly exclude EquipTool from `!farmtest` / OpenToolshed experiments; **no** `FactoryPacketEquipTool*` types under `Sanctuary.Packet` |
| `FactoryInstance` | Still TODO stub in `PlayerHousingInstanceData` (`Write(false)`) — unrelated unfinished housing field |
| `CurrentTool` / `SelectedTool` in `.cs` | **No** Factory CurrentTool implementation |

---

## 15. Exact 188/23 Shovel response (PROVEN)

### 15.1 Byte ledger — inner packet for live request `BC00070004000000`

| Off | Hex | Type | Decoded | Meaning | Evidence |
|-----|-----|------|---------|---------|----------|
| 0 | `BC` | `int16` lo | 188 | Family | ctor / base ser-deser |
| 1 | `00` | `int16` hi | | | |
| 2 | `17` | `int16` lo | 23 | EquipToolResponse | ctor `push 0x17` |
| 3 | `00` | `int16` hi | | | |
| 4 | `01` | `uint8` | 1 | Success=true | deser `setne` → handler gate |
| 5 | `04` | `int32` b0 | 4 | ToolId | deser → `+0x10` → CurrentToolId |
| 6 | `00` | b1 | | | |
| 7 | `00` | b2 | | | |
| 8 | `00` | b3 | | | |

### 15.2 Concatenated inner hex

```text
BC0017000104000000
```

### 15.3 Manual deserializer walkthrough — **PASS**

Target: `0x9E04A0` with stream = above 9 bytes.

1. `0x9E0340` base: read `int16` 188 → `+0x04`; read `int16` 23 → `+0x08`; cursor=4  
2. Need 1 byte: present `01` → `setne` → `+0x0C=1`; cursor=5  
3. Need 4 bytes: `04 00 00 00` → `+0x10=4`; cursor=9  
4. Cursor == packet end → **PASS** (no unread bytes, no truncation)

Tunneled framing for a live send would wrap this inner payload the same way other Factory S2C packets are wrapped; **this report does not send**.

---

## 16. Remaining unknowns

1. Whether Tool Shed UI auto-closes on success (not proven in native handler body).  
2. Whether a visible held Shovel mesh appears from 188/23 alone (local Tool Item / Composite Effect are 0).  
3. Exact object type returned by `0x10343E0` and meaning of `[found+0x0C]` passed into the UI string path.  
4. Obstacle/misfortune click packet after CurrentToolId=4.  
5. Retail Success policy edge cases (always 1 on allow? ever 0 with ToolId still set?).  
6. Whether response ToolId must equal request ToolId for retail servers (client does not check).

---

## 17. Safest next controlled live experiment

**Do not implement production EquipTool yet** unless explicitly requested. Suggested experiment only:

1. Keep existing one-Shovel ListToolsResponse path.  
2. On unhandled / logged `BC00070004000000`, send tunneled inner **`BC0017000104000000`** once.  
3. Observe: CurrentTool / tool-mode button UI; whether shed closes; presence/absence of shovel mesh; next packet if an obstacle is clicked.  
4. Optional negative control later: Success=`00` with ToolId=4 → expect **no** CurrentTool update.

**Not safe to guess:** omitting the Success byte, swapping field order, or adding unproven trailing fields.

---

## 18. Confidence summary

| Claim | Label |
|-------|-------|
| Live 188/7 Shovel `BC00070004000000` | LIVE-PROVEN |
| 188/7 = header + int32 ToolId only | LIVE-PROVEN + PROVEN (static) |
| 188/23 = header + uint8 Success + int32 ToolId | PROVEN (static) |
| Success gate + CurrentToolId store + ExitToolMode invoke | PROVEN (static) |
| Exact Shovel response `BC0017000104000000` | PROVEN (static) — all required fields proven |
| 188/23 alone guarantees visible shovel prop | UNKNOWN / not supported by local Tool Item=0 |
| Obstacle interaction after equip | UNKNOWN without live test |
