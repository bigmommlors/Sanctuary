# Encounter capture reassembly investigation

## Outcome

Reassembly recovered **13 additional fragmented 41/114 occurrences**, including the previously missing combat offer. There are now 21 complete 114 occurrences, representing 12 distinct offer/launch payloads across four encounter runs. Overlapping capture files are not independent sessions.

**No verified Activity 29 / Bandit Hideout encounter was found. A safe Bandit Hideout offer still cannot be generated from this evidence.** No server source/configuration was changed and no services were started or stopped. Only research scripts, downloaded reference material and this report were written. No server build was needed.

## A. Capture coverage and reproducibility

All 14 capture files in the [pinned OSFR archive](https://github.com/Open-Source-Free-Realms/OpenSourceFreeRealms/tree/ace6a82e0ab07111be0fc8e899b5a0716724e137/ofrserver/Packets) were processed:

- p1.pcap, p12.pcap, p2.pcap, p3.pcap
- packets_1.pcapng, packets_1_just_gameserver.pcapng, packets_2.pcapng
- packets_3.pcapng, packets_4.pcapng
- packets_demoderby_minigame.pcapng, packets_racing_minigame.pcapng, packets_soccer_minigame.pcapng
- possible_character_data.pcap, possible_character_data.pcapng

The earlier inventory inspected all five OSFR archive branches. Bik182/Free-Realms also exposes the same 14 capture filenames/sizes; this was not treated as independent encounter evidence. No additional relevant captures were found in the other inspected repository trees.

Method: IPv4 fragment support, TCP sequence-range assembly without bridging gaps, SOE decompression and grouping, reliable-channel sequencing with wrap/duplicate handling, and application fragmentation using the big-endian declared message length. Tunnel lengths must match exactly before interpreting little-endian gameplay headers. No actual IPv4 fragmentation was encountered; SOE application fragmentation supplied the new encounter data. TCP searches found no encounter signatures; the three lowercase `bandit` hits were player surnames in HTTP leaderboard XML, not Bandit Hideout.

Limitations: no CRC validation or arbitrary encryption support; some captures omit transport negotiation (two-byte CRC assumption), have sequence gaps, or contain unrelated/undecodable UDP. Partial fragments are not silently stitched across gaps. Therefore the conclusion is scoped to successfully reconstructed data, not a proof that missing bytes could not have contained an encounter.

Research artifacts are under `C:/Users/lauri/Sanctuary/src/Sanctuary.Gateway/bin/encounter-reassembly/`:

- `reassemble.py`, `check_reassembly.py`: reproducible assembly and four passing synthetic checks (wrap, out-of-order arrival, duplicates, missing fragment).
- `reassembly-stats.json`: per-capture transport counts and TCP hit context.
- `encounters.json`: all 305 family-41 occurrences, full hex, direction, timestamps and capture frame numbers.
- `decoded-offers.json`: all 21 complete 114 decodes, including every reward/objective field and raw participant record.
- `decode_offers.py`: decoder with exact final-length assertions for all 21 occurrences.
- `ui-events.json`, `zones.json`: nearby UI/zoning evidence.

Frame numbers below are one-based capture-record numbers. Times in artifacts are Unix microseconds. Raw integer slots remain raw where semantic types are uncertain (for example 1065353216 is also the IEEE-754 representation of float 1.0).

## B. What reassembly added

Thirteen fragmented 114 occurrences: four of length 807, eight of length 787, and one of length 514. The original complete-frame scan saw eight other 114 occurrences. The four copies of the combat offer and eight copies of its two launch updates overlap across p12, p2, p3 and packets_2.

Other fixed-layout packet counts remain 30 state packets (106), six readiness packets (107), and six entrance requests (108). No additional fragmented 106/107/108 was needed. Total family-41 count is 305; the earlier loose scan's 293 included one extra false-positive 41/2, so the net increase is 12 rather than 13.

## C. Activity 29 correlation

No reconstructed 167/1/2 join request was found, including none for 29. The 48 family-167 occurrences are list/refresh-related branches (`167/1/1`, `167/1/4`, `167/2/1`). Activity 29 appears in generic activity-definition data, which is not evidence that it was played. No Bandit Hideout UI event or `sg_bandit_hideout` zoning was found.

The recovered combat offer is **Activity 174 / Frostfang Growler**, corroborated by its offer tail and resource mapping. Its zone is `sg_random_encounter_clearing`, not Bandit Hideout. The other three runs are soccer, racing and demolition derby.

## D. Exact decoded layout and values

All gameplay headers/integers below are little-endian. H1/H2 retain neutral names.

| Packet | Exact fixed layout | Length |
|---|---|---:|
| 41/106 | UInt16 41, UInt16 106, Int32 H1, Int32 H2, Int32 state | 16 |
| 41/107 | UInt16 41, UInt16 107, Int32 H1, Int32 H2 | 12 |
| 41/108 | UInt16 41, UInt16 108, Int32 H1, Int32 H2, UInt64 character GUID | 20 |
| 41/114 | Same 12-byte header followed by common data, MiniGameInfo and offer/launch tail | variable |

The 108 suffix `91CA73956ACC524B` decodes to `0x4B52CC6A9573CA91`. It matches the GUID decoded using Sanctuary's PacketLogin layout in p1 frame 3 / packets_1 frame 867, and appears in the captured participant record and player-enter/message packets. Only one character is represented in these encounter records; this is not cross-player validation.

Example Frostfang fixed packets:

```text
106 state 2: 29006A00AE000200F003000002000000
107:         29006B00AE000200F0030000
108:         29006C00AE000200F003000091CA73956ACC524B
```

### Reassembled 114 structure

In every recovered 114: H1/H2 at offsets 4/8; common Int32 pair at 12/16; collection count 1 at 20; one 71-byte record at 24..94; team count 0 at 95; context at 99; teleport effect at 103; three Boolean bytes at 107..109; unknown Int32 at 110; respawn time at 114; MiniGameInfo begins at 118.

**The 71-byte record's generalized schema is not established.** The decoder preserves it verbatim rather than pretending to parse arbitrary participants. It contains the corroborated character GUID at offset 32 and a length-prefixed seven-byte UTF-8 name at offset 67. There are eight distinct record payloads across the 21 occurrences; the GUID/name remain the same, while other bytes vary. The initial Frostfang offer's raw record is:

```text
01000000FB0621DE91CA73956ACC524B0001000000010000000000A61B0243C520303F6F12F0420000803F070000004D61C3BF68656D0000000000F00300000000000000000000
```

MiniGameInfo: six Int32 slots (name, icon, description, difficulty, profile type, type), membership Boolean, three reward bundles, count-prefixed objectives, five Booleans, a length-prefixed string, Int32, Boolean, preselected-game Int32, four Booleans, final Int32. Followed by two common Booleans, Launch Boolean, packet-tail Int32, and count-prefixed store-bundle IDs.

Reward bundle observed layout: Boolean; nine 32-bit slots; two 64-bit slots; icon/name Int32s; count-prefixed entries; final Int32. Each observed entry has type 1, Boolean hidden, six Int32s, string, Int32, Boolean and (when bundle flag is true) an additional Int32. Objectives contain id/name/description, Boolean, reward bundle, four Int32s, Boolean and Int32. Full values and offsets for each occurrence are in `decoded-offers.json`; names for unresolved slots are deliberately neutral. Exact consumption verifies boundaries for these fixtures, not a universal schema.

| Run | H1 | H2 | Context | MiniGameInfo name/icon/description | difficulty/profile/type | preselected |
|---|---|---:|---:|---|---|---:|
| Frostfang 174 | 0x000200AE | 1008 | 1 | 93276 / 28605 / 104171 | 1 / 2 / 4 | 0 |
| Soccer 72 | 0x00070048 | 1008 | 4 | 37944 / 20992 / 6703 | 1 / 8 / 12 | 61 |
| Racing 62 | 0x000E003E | 1008 | 2 | 17958 / 9870 / 37935 | 1 / 5 / 10 | 28 |
| Derby 65 | 0x00140041 | 1008 | 3 | 37938 / 9869 / 37941 | 1 / 7 / 11 | 39 |

Frostfang has teleport effect 0, common flags true/true/false, unknown value 2000 and respawn value 10000. These are captured Frostfang values, not recommended Bandit defaults. Its objectives are 12288 (name 2286, description 100712) and 12642 (name 104176); totals change from 0/0 in the offer to 5/1 in launch updates. Its preview item-definition IDs are 76209, 75408, 75091, 75385, 10482. Soccer has objective 2350/name 20136; racing/derby have no objectives in these messages. Reward bundles and their changes are retained individually in the decoded JSON.

### Important correction to earlier 114 assumptions

All four runs show this three-message progression:

| Stage | common pair | MiniGameInfo final Int32 | Launch | packet-tail Int32 | store bundles |
|---|---|---|---|---|---|
| Offer | H1, 2 | H1 | false | activity ID | 2194,2919,2920,2921,3752 |
| First launch update | H1, 4 | activity ID | true | 0 | empty |
| Second launch update | H1, 4 | 0 | true | 0 | empty |

Thus the field previously labelled `ActivityId` at the end of MiniGameInfo is **not always an activity ID**, and the packet-tail Int32 is not always zero. The current restricted 320-byte serializer does not represent these nonempty retail offers. It remains unused; no correction to server code was made in this investigation.

### Exact correlated combat sequence

In p12.pcap:

| Frame(s) | Event |
|---|---|
| 29375 | 41/106 state 2 |
| 29379 | 41/106 state 3, then state 4 |
| 29382–29383 | reassembled 807-byte 41/114 offer |
| 29412 | 41/107 readiness, then 41/106 state 5 |
| 29452 | MiniGameStartScreen / show |
| 29593 | client 41/108 entrance request |
| 29599 | zone transition to sg_random_encounter_clearing |
| 30460 | 41/106 state 6 |
| 30680–30681 | reassembled 787-byte launch update |
| 30685–30686 | second reassembled 787-byte launch update |

Other initial offers: soccer frames 216/218 (514 bytes), racing frame 135 (411 bytes), derby frame 122 (411 bytes). Each has its own nearby start-screen and zone transition, recorded in the artifacts. This verifies a retail sequence for other encounters, not the missing Activity29-specific data.

## E/F. Variation and header allocation

The four initial offers occur on 2014-03-31 at approximately 23:20:16, 23:41:48, 23:53:36 and 23:56:10 UTC. H1 obeys the observed equation:

```text
H1 = (observed high word << 16) | activity ID
high words in chronological order: 2, 7, 14, 20
H2 = 1008 in every run
```

The equation fits these four observations. **It is not a verified allocation rule.** There are no repeat runs of the same encounter with different players/server processes to establish high-word increment, reuse, lifetime, scope or reset behavior. Intermediate numbers are unobserved. No timer/timestamp interpretation is established. H2 may belong to a larger identity scheme, but there is no evidence here to label it as an instance, player, realm or server ID. The matching player GUID does not explain 1008. The decoded zone-transition ID also does not establish that mapping.

What varies within one run: state, common second Int32, MiniGameInfo final Int32, Launch, packet tail, store bundles, participant-record bytes, and some objectives/rewards. What remains stable within each observed run: H1/H2 and character identity. What differs between encounters: H1, presentation/context/type/preselected data. What is not measurable: per-player or same-activity cross-session header variation.

## Code/resource/history search

The previous branch inventory covered 38 forks, 156 branch heads and 130 distinct commits, with three known paths checked per distinct head. This was not an exhaustive search of all renamed/deleted files. This pass additionally inspected Bik182's archived client/server tree, FreeRealms-Legacy and Clover trees, client activity/minigame tables, and the full pinned Sulphural source snapshot.

Client MiniGameData's ID namespace is distinct: entries 28/39/61 corroborate racing/derby/soccer preselected IDs, while MiniGameData row 29 is not Bandit Hideout. ActivityDefinitions supplies the Bandit metadata but no runtime header allocator. The POI's location/teleport/name IDs likewise do not establish H1/H2.

[CarterW24's pinned implementation](https://github.com/CarterW24/combat-main/tree/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4) and [Sulphural's pinned implementation](https://github.com/Sulphural/Sanctuary/tree/8075c83f0eb1fd4ed6a829884e0351b24a800675) still use hardcoded `instanceId = 1` and activity/constant encounter IDs. No captured-header allocator was found. Sulphural history commit `63a44f99bfd170d7302483ca41a1ec95f3311e37` replaces fabricated `900000 + POI` routing IDs with ActivityId; that change does not generate the captured high word or H2. Generic player-GUID generation is a separate scheme. These forks do not resolve the evidence gap.

## G/H. Can a safe Bandit offer be generated?

**No, under the requirement not to invent values.** No live sequence is proposed for implementation yet. Remaining unknowns:

1. H1 high-word allocation/scope/lifetime and H2 meaning/allocation.
2. Bandit-specific retail common/participant data and generalized participant-record schema.
3. Verified Bandit encounter MiniGameInfo/type/profile, objectives/rewards and whether empty forms are permitted.
4. Correct state/offer tail semantics and actual instance readiness/lifetime requirements for this encounter.

Activity29 definitions establish its static identity/presentation, not those runtime rules. Copying Frostfang H1/H2, forcing H1=29, using H2=1/0, or borrowing its rewards would remain unsupported.

## I. Smallest next experiment

First perform **offline client-code tracing** of the 41/106/107/108/114 consumers using these recovered fixtures: follow the two header fields into encounter lookup/equality/storage and the 114 final Int32 into offer/launch handling. This can establish whether the high word and H2 are opaque identities echoed by the client or carry client-side constraints, without changing/running the server. Trace the 108 GUID write at the same time.

To establish original-server allocation rather than merely client tolerance, obtain either the allocator implementation or an additional authentic capture containing repeated starts of the same encounter, ideally Activity29, across two characters and a reconnect. Capture from before world initialization through offer, entrance, cancellation/completion and repeat start. Compare all identity fields and their reuse. The current archive cannot distinguish competing allocation rules; replaying a fork's fabricated values would not establish the retail rule.

No client/server restart is required for this report. No guessed success response was sent.
