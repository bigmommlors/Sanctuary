# Offline client encounter-header investigation

## Conclusion

**For the inspected client's offer -> readiness -> entrance-request path, H1 is an opaque 32-bit lookup/correlation key and H2 is a separately remembered, echoed 32-bit value.** This path does not split H1 into activity/high words or require H2=1008. The retail allocation algorithm remains unknown, but it is no longer necessary to reproduce that algorithm merely to choose identifiers for an isolated local offer-to-108 experiment.

This conclusion concerns identifier handling only. It does **not** establish that the current restricted 114 serializer or unverified Bandit Hideout offer contents are sufficient. No packet was sent, no client/server was executed for this research, and no server source/configuration or service state was changed.

## Evidence and scope

Analyzed the actual installed `FreeRealms.exe` at:

`C:/Users/lauri/AppData/Local/OSFRLauncher/Servers/Sanctuary Local/Client/FreeRealms.exe`

- Size: 25,067,520 bytes; 32-bit PE; preferred image base `0x00400000`.
- SHA-256: `26cc1c522a45e71fb57882da515f65bac45c8c8b8c7e740c0cea81c52a733351`.
- Installed OSFR Public Server and Raising Kaines Windows client executables have the identical hash.
- Addresses below are preferred virtual addresses in this exact binary, not file offsets or addresses sampled from a running process.
- Native instructions were read using Windows' existing DbgEng disassembler against a synthetic dump assembled from PE file bytes. The game executable was never launched or attached to. No software was downloaded/installed and no network research was performed this turn.
- RTTI names and the actual switch table corroborate the packet identities. Fork comments were used as leads, then checked against the binary.
- Parsed the installed Lua 5.1 `UI/ScriptsBase.bin`: **5,057 function prototypes**, exact full-file consumption of 2,897,335 bytes. Retained 91 relevant function listings, including original source filenames and line numbers embedded in bytecode. No standalone encounter Lua/SWF/debug-symbol/decompiler database was found in the inspected installed clients. Packed artwork/assets were indexed by manifest, not universally unpacked/decompiled.
- Inspected installed activity/minigame tables, existing archive-tree metadata, all locally available git refs/history, cached Carter sources, and the previously downloaded Sulphural source. This offline pass does not claim to search new remote commits or every historical fork file.

All research artifacts are in:

`C:/Users/lauri/Sanctuary/src/Sanctuary.Gateway/bin/client-header-analysis/`

Key files: `static_client.py`, `find_refs.py`, `lua_scan.py`, `dispatcher.asm.txt`, `base_client_dispatch.asm.txt`, `header.asm.txt`, `start_pending.asm.txt`, `header_writer.asm.txt`, `ready_lookup.asm.txt`, `ready_insert.asm.txt`, `lua-selected.txt`, and `lua-selected.json`. These are research artifacts, not compiled server source.

## A. Exact handlers and dispatch locations

The family-41 native dispatcher is `0x00AA36C0`. It reads the common header, subtracts 2 from the message ID, and uses:

- byte selector table `0x00AA452C`;
- target-address table `0x00AA44E8`.

Reading those tables gives:

| Message | Client location | Verified behavior |
|---|---|---|
| 106 | case `0x00AA43BF`; validator `0x00A9FAE0`; reader `0x00A9ED20` | Reads header plus Int32 state. This handler acts on state **9**, passing full H1 to `0x00A28C70`. It does not advance a state-2/3/4/5 startup machine here. |
| 107 | case `0x00AA3FB1`; validator `0x00A9F990` | Reads exactly the common header; calls ready function `0x009B0CC0`, stores H2, inserts H1 into the readiness set. |
| 108 | outbound constructor `0x008B6E70`; request builder `0x00938F00`; send serializer `0x00929F80` | Builds and sends 41/108 from pending H1, stored H2, and character GUID. In the inbound switch, 108 selects default `0x00AA449C`; it is not an inbound startup handler. |
| 114 | case `0x00AA3C35`; validator `0x00AA3430`; reader `0x00AA32D0` | Deserializes header and offer body, looks up readiness by full H1, selects offer/launch processing and stores pending H1. |

Common header reader: `0x008D6690`. Common writer: `0x00913DB0`.

## B. H1 storage and use

### Packet object

The header reader stores the first Int32 at packet-object offset `+0x0C`:

```text
008D66FA  mov edx,[edx]
008D66FC  mov [ecx+0Ch],edx
```

The preceding wire UInt16 family/message values occupy object offsets +4/+8 as expanded integers. These object offsets differ from wire offsets.

### Readiness lookup

107 inserts full H1 into the command processor's container at `+0x84` (`0x00AA3FF1..0x00AA3FFF`, insertion function `0x009B4350`). The node stores its full key at +0x0C. Lookup `0x009AB0A0` hashes the unsigned key modulo 100, then compares **all 32 bits**:

```text
009AB0C0  cmp [eax+0Ch],esi
```

114 consults that set using its H1 at `0x00AA3D6B..0x00AA3D85`. This supports readiness arriving before an offer. A collision in the hash bucket is not identity equality: the full key is compared while traversing nodes.

### Pending encounter and GO

In the offer branch, `0x00AA3E1F` loads packet H1 and `0x00AA3E40` stores it in command-processor offset `+0x80`. Another special branch also writes the same slot at `0x00AA3D45..0x00AA3D4C`.

`StartPendingEncounter` -> `0x00938F00` reads `[BaseClient+0xF4]+0x80` at `0x00938F46..0x00938F58` and places it directly in outgoing packet H1. No activity-table lookup, 16-bit extraction, high-word counter calculation, or H1/H2 equality check occurs in that request builder.

114 also passes the full H1 to mini-game state construction. `0x009B8190` stores the key at state-node +0x918; `0x009B16E0` uses modulo-10 bucket selection. The state presentation constructor copies the supplied H1 to state +0x0C (`0x00C42CAB`). These are identity/storage operations, not resource-ID validation.

106's state-9 lookup at `0x00A28C70` uses `H1 & 0x3FF` as a bucket index, then compares the **full H1** at node +0x2D8 before removal. This mask is hashing, not extracting the activity's low 16 bits.

## C/F. H2 storage, echo and 1008

The common header reader stores H2 at packet-object +0x10 (`0x008D6720..0x008D6724`).

The outer encounter receive path `0x0090D830` reads that header and stores H2 into **BaseClient +0xC10** at `0x0090D8AD..0x0090D8B5`, before forwarding the message to the command processor. The 107 case also explicitly updates the same slot at `0x00AA3FE4..0x00AA3FEB`.

The entrance builder copies BaseClient +0xC10 to outgoing H2 at `0x00938F5C..0x00938F6E`. Cancellation and other encounter request builders also copy the same slot. The serializer writes it as four bytes without interpreting it.

**There is no comparison against decimal 1008 / hex 0x3F0 in these traced paths.** H2 is not consulted as a resource index or split into subfields here. Its retail business meaning remains unknown; the observable role is a last-received encounter-header value carried back to the server. It cannot yet be named definitively as server ID, zone ID, participant ID, version, or namespace.

Important constraint: H2 is **shared client state**, not a value stored independently for every pending H1. A subsequent family-41 receive through this outer path can overwrite it before GO. The archive also contains unrelated family-41 packets with zero headers; therefore merely assigning a nonzero H2 to one offer does not guarantee the client will echo it later. Any future controlled test must account for intervening family-41 messages.

## D/E. Resource validation and splitting

No `H1 & 0xFFFF`, `H1 >> 16`, `(x << 16) | ActivityId`, high-word comparison, or H2==1008 requirement was found in the traced header read/write, readiness, offer-to-pending, and entrance-building paths.

The client does use resources for MiniGameInfo presentation/type/preselected data. That is separate from validating H1/H2. Do not conflate the resource-dependent offer body with its opaque header identity.

The installed tables and pinned activity definitions do not support the proposed high words as shared category/type IDs:

| Encounter | Observed high word | Activity category | Captured MiniGameInfo type | Preselected game |
|---|---:|---:|---:|---:|
| Frostfang 174 | 2 | 99 | 4 | 0 |
| Soccer 72 | 7 | 22 | 12 | 61 |
| Racing 62 | 14 | 16 | 10 | 28 |
| Derby 65 | 20 | 16 | 11 | 39 |

Bandit 29 has activity category 10. Raw numbers 2/7/14/20 occur in many tables, but no correlated mapping from these four activities to those high words was established. The executable contains strings for EncounterDefinitions.txt, EncounterTeams.txt and EncounterDatasetMappings.txt; those named resources were not present in the inspected loose resource files or asset-manifest names. Their existence as strings is not proof that the startup path validates against them.

## Lua -> native -> 108 chain

The installed Lua bytecode retains `@minigamehandler.lua`, function lines 109–114:

```text
Options:Hide()
MiniGameHandler:CloseStartScreen()
Ui.StartPendingEncounter()
```

The script passes no encounter ID or H2. The native registration entry at `0x0182DE00` maps `StartPendingEncounter` to `0x00C17280`, which calls `0x00938F00`.

That function checks BaseClient +0x700 == 1 for the encounter branch, constructs packet 108, copies pending H1, stored H2, and the character GUID from BaseClient +0x3A0/+0x3A4, and calls `0x00929F80` if the connection exists. Mode 2 takes a separate mini-game path. Thus **proper offer state/mode still matters**, even though identifier values are opaque.

`@minigamestart.lua` lines 54–68 routes ready/not-ready UI state; it does not calculate either header. The reader recovered exact bytecode boundaries for these functions, not guessed text from a binary string search.

## G. Allocation rule and fork/history comparison

No retail allocation rule was recovered. The four captured H1 values remain consistent with `(highWord << 16) | ActivityId`, but the native paths above neither generate nor require that packing.

Cached CarterW24/combat-v2 (`3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4`) and the full downloaded Sulphural snapshot (`8075c83f0eb1fd4ed6a829884e0351b24a800675`) do not implement a matching allocator: they use activity/constant encounter IDs and fixed instance values. The previously inspected dungeon routing-ID history is not an allocator for the captured high word. Searches of all available local refs (`main`, current branch, `origin/main`, `origin/minigame`) and their relevant history did not reveal a removed encounter allocator. Cached fork material was searched offline; no new remote history was fetched.

## H/I. Is a fresh local key supportable? Proposed rule only

**Yes, for identifier handling in a single-player, isolated offer-to-108 experiment in this exact client build.** This is a static-analysis-backed compatibility conclusion, not a successful live test or proof about all later encounter paths.

Proposed local policy, explicitly **not** a claim about retail allocation:

1. Allocate a new positive, non-reused encounter serial N from a local registry. For a conservative packed form, restrict N to 1..32767 and use `H1 = (N << 16) | 29`. Keeping the observed low-word convention costs nothing, but the traced client does not require it. N is a local uniqueness mechanism; it is not a discovered retail counter or a fixed instance ID.
2. Never reuse H1 while that client may retain readiness/state entries. H2 cannot disambiguate a reused H1 because readiness lookup uses H1 alone. Do not reset N on a world reload/reconnect unless client-state clearance is established; on exhaustion, stop allocation rather than wrap.
3. Allocate a positive nonzero 31-bit local session token for H2, checked against live tokens in the local registry. Keep it stable through the pending offer and entrance request. Its value need not be 1008 according to the traced code. This gives it a local meaning without claiming that meaning matches retail.
4. Preserve H1 across all relevant state/offer/readiness messages and the matching offer-body identity fields. Do not replace every lifecycle-dependent 114 field with H1: the captured H1 -> ActivityId -> 0 tail progression is still distinct.
5. Ensure intervening family-41 traffic cannot overwrite the H2 slot with another value before GO. For an initial experiment, isolate the pending handshake or explicitly account for every relevant sender; do not blindly rewrite unrelated packet headers.
6. Bind the pending server record to the authenticated connection and character GUID. On 108, require the expected H1, expected echoed H2 and that character's GUID; reject stale/cancelled keys. These are server-side correctness constraints, not validations supplied by the client.

Why this meets the observed client constraints: the complete H1 survives storage, hashing, equality and serialization; the shared H2 survives copying if traffic preserves it; the client does not reconstruct either field from a static table. Positive/nonzero bounds are conservative local policy, not discovered client restrictions.

## J. Remaining blocker and next smallest milestone

The unknown **retail header allocator is no longer the main blocker for a controlled local identity experiment**. The remaining blocker to claiming a coherent Bandit offer is the 114 body and its state setup: the current restricted serializer omits the nonempty participant/common data from authentic offers, and Bandit-specific type/profile/objectives/rewards remain unverified.

Smallest next offline milestone: trace `EncounterDetailsCommon` (`0x00A29120`) and its participant reader (`0x00A27610`), then trace the exact offer-mode/precondition fields consumed by `0x00AA3C35`. Establish whether a minimal empty-participant/objective/reward offer actually creates mode 1 and a valid pending state. This can identify which missing data is required to reach 108 without inventing Bandit combat/reward content.

No server implementation is proposed as already safe in its entirety, and none was performed. The raw native/Lua evidence is saved for independent review; original allocation semantics and full combat startup remain unresolved.
