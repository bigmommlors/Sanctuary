# Bandit Hideout encounter-offer milestone

## Outcome

The verified layouts support a protocol-only increment, but **no coherent live offer/readiness sequence can yet be sent using verified Activity 29 values**. The implementation follows the requested fallback: serializers, a capture-backed entrance parser, both tunnel routes, diagnostics, and tests. No offer, readiness, acceptance, combat, teleport, reward, or instance creation is performed.

The new evidence is materially stronger than the earlier fork-only review: complete family-41 frames were extracted from the public archived PCAPs. Their identity headers differ from the fork's `ActivityId, 1` shortcut, and 41/108 contains eight bytes that the fork's handler never reads.

## A. Wire layouts

All integers below are little-endian. `i16`/`i32` mean signed 16-/32-bit values; `b` means a one-byte Boolean. `H1` and `H2` are the two opaque header Int32 values. Their allocation/meaning is not established for the local server; they are deliberately not renamed to ActivityId or ZoneId.

| Message | Layout | Length and evidence |
|---|---|---|
| 41/106 EncounterStatePacket | `i16 41, i16 106, i32 H1, i32 H2, i32 State` | 16 bytes; four distinct archived header pairs, each with states 2 through 6 |
| 41/107 EncounterZoneIsReadyPacket | `i16 41, i16 107, i32 H1, i32 H2` | 12 bytes; four distinct archived frames |
| 41/108 EncounterParticipantRequestEntrancePacket | `i16 41, i16 108, i32 H1, i32 H2, opaque[8]` | 20 bytes in all six extracted occurrences / four distinct frames; suffix meaning unverified |
| 41/114 EncounterDetailsResponsePacket | 12-byte header, EncounterDetailsCommon including MiniGameInfo, packet tail | Variable; the fork's restricted empty-collection offer is 320 bytes; extracted complete retail messages were 391, 411, or 494 bytes |

One archived set, from `p12.pcap`:

```text
106: 29006A00AE000200F003000002000000
107: 29006B00AE000200F0030000
108: 29006C00AE000200F003000091CA73956ACC524B
```

Here H1=`0x000200AE` (131246), H2=1008. Other captured H1 values are `0x000E003E`, `0x00070048`, and `0x00140041`, all with H2=1008. These are **capture fixtures for other encounters, not identifiers to send for Bandit Hideout**. A low-word resemblance to an activity does not establish an allocation algorithm. The eight-byte 108 suffix is retained verbatim; no participant ID or GUID meaning is invented.

The captured order includes state 2, then states 3/4, then readiness and state 5, then client 108, then state 6. In this trace state 5 precedes the client's GO request, despite an ambiguous source comment saying it follows GO. The complete 114 frames extracted by the scan do not establish a full Bandit Hideout offer sequence.

### 114 details and limits

The candidate serializer's layout is:

1. Header: `i16 41, i16 114, i32 H1, i32 H2`.
2. EncounterDetailsCommon:
   - Two Int32 unknowns.
   - Unknown collection and team collection, each count-prefixed. The port supports only their empty form; the complete element layouts have not been validated here.
   - Int32 ZoneContext and TeleportEffectId.
   - Three Booleans: Unknown5, Unknown6, Tutorial.
   - Int32 Unknown8 and RespawnTime.
3. MiniGameInfo:
   - Six Int32s: NameId, IconId, DescriptionId, Difficulty, ProfileType, Type.
   - Boolean MembersOnly.
   - Three reward bundles (base, member, preview).
   - Count-prefixed objective list.
   - Five Booleans U8..U12, Int32-length-prefixed UTF-8 string U13, Int32 U14, Boolean U15, Int32 PreselectedGameId, four Booleans U16..U19, Int32 ActivityId.
4. Two common trailing Booleans UNK0 and UNK1.
5. Boolean Launch, Int32 packet unknown, count-prefixed StoreBundleId set.

For the restricted port, collections/objectives/prizes are empty, Launch=false, and no reward entries are supported. The 69-byte reference empty reward form is `b + 17*i32`. This is copied from the fork's empty-bundle writer; it is **not a claim that all-zero reward defaults match retail**. Archived live empty bundles include a float-1.0 bit pattern and -1 values where the fork writes zeroes. Existing Sanctuary RewardBundleBase currently emits a different 65-byte empty shape and was not reused or changed.

Offsets in the 320-byte restricted serializer:

| Offset | Field |
|---:|---|
| 0, 2 | Family 41, message 114 |
| 4, 8 | H1, H2 |
| 12, 16, 20, 24 | Common unknowns, two empty collection counts |
| 28, 32 | ZoneContext, TeleportEffectId |
| 36..38 | Three Booleans |
| 39, 43 | Unknown8, RespawnTime |
| 47..70 | Six MiniGameInfo Int32s |
| 71 | MembersOnly |
| 72..278 | Three 69-byte reference empty bundles |
| 279 | Objective count=0 |
| 283..308 | MiniGameInfo tail, ActivityId at 305 |
| 309, 310 | Common UNK0, UNK1 |
| 311 | Launch=false |
| 312, 316 | Packet unknown, empty prize-set count |

The serializer is intentionally not a general parser for arbitrary retail 114 messages. Nonempty participants, teams, rewards and objectives are outside this milestone. No live send path calls it.

## B. Verified Bandit Hideout data

From the pinned activity resource:

- ActivityId 29, comment Bandit Hideout, ServerType 1, AppSystemId 2, Category 10.
- NameId 5172, DisplayNameId 425259, DescriptionId and DisplayDescriptionId 6995.
- ImageSetId 1345, ImagePositionId 101, Difficulty 1, PreferredRequirementId 5, PlayerCanJoin=true.
- Detail/thumbnail filenames identify Bandit Hideout.

The current PointOfInterests resource independently contains Bandit Hideout at POI 54, NameId 5172, LocationId 1130, SubNameId 382845 and TeleportLocationId 210068. These identifiers have their own roles; they are not evidence for the family-41 header pair. In particular, the fork's 382845 description also exists as a POI subname, but that does not establish it as the correct encounter description.

The current ZoneManager creates the starting zone with a local unique zone ID. There is no verified mapping from that ID, the player GUID, the POI ID, or ActivityId to family-41 H1/H2. Reusing one would be guessing.

## C. Hardcoded or borrowed candidate values

- `instanceId = 1`: hardcoded in both CarterW24/combat-v2 and Sulphural/main.
- First encounter header = ActivityId: a fork shortcut; captured headers are not simply their low-word activity values.
- Ready packet = default zero headers: inconsistent with the same fork's offer header `(29, 1)` and the matching headers in captures.
- `Task.Delay(600)`: an arbitrary timer, not a protocol field or a prepared-instance condition.
- Rewards/profile/XP reference FrostfangArenaZone; these are not verified Bandit Hideout rewards.
- Difficulty 3 differs from the pinned activity's 1.
- Description 382845 is a POI subname; the activity description is 6995.
- Type 4 and profile type from Frostfang have no activity-29-specific validation in this work.
- Empty common data, zero-filled reward defaults, and reference Boolean defaults are serializer choices, not proof of a coherent offer for the local player.
- Sulphural's newer version additionally constructs objective IDs from ActivityId and `900000 + ActivityId`, and sets MembersOnly=true for a reward-screen behavior. These are not a cleaner source of verified values.

## D. Search coverage and alternative implementations

Public GitHub fork inventory: **38 forks, 156 branch heads, 130 distinct commits**. Three known join/encounter source paths were checked at every distinct head (390 requests: 11 existing files, 379 absent paths). This is a branch-head/path search, not an exhaustive scan of every historical or renamed file. Additional trees were inspected for the protocol/minigame branches in brennengreen, CoderA05, Sulphural, and the pinned CarterW24 implementation. Local main/origin/minigame and existing packet-name mappings were inspected as well.

- CarterW24/combat-v2 has Activity 29 support but the issues above remain.
- Sulphural/main `8075c83f0eb1fd4ed6a829884e0351b24a800675` has a later dungeon path, still using fixed IDs, zero-header readiness and Frostfang preview rewards. It is not safer to port as-is.
- CarterW24/combat and PR serializer branches provide related packet structures, not an independently verified local Activity 29 offer setup.
- The reviewed origin/minigame, soccer and fishing variants do not supply a cleaner Bandit Hideout handshake.
- No safer existing Activity 29 implementation was found within that search scope.

All five archived OSFR branch capture indexes were inspected. The 14 public captures from pinned archive commit `ace6a82e0ab07111be0fc8e899b5a0716724e137` were downloaded and scanned: **276,570 UDP records**, **293 encounter-frame matches**, including 30 message-106 occurrences, six 107s, six 108s, and eight complete 114s. Several files overlap, so these are not independent sessions. No complete 167/1/2 join frame was found by this scan.

Method: the existing brennengreen PCAP/UDP reader, zlib decompression at transport prefixes, and bounded complete tunnel extraction. It does not reassemble SOE fragmentation or encrypted/missing segments. Consequently, absence of a Bandit Hideout frame is not proof that none exists in fragmented data. The important fixed-layout fixtures have repeated consistent headers, directions and state sequences.

Research sources and scan outputs are retained under `src/Sanctuary.Gateway/bin/encounter-research/` as ignored artifacts, including branch/source-scan metadata and `capture-encounters.json.txt`.

Primary source links:

- [CarterW24 pinned packet directory](https://github.com/CarterW24/combat-main/tree/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Packet/BaseEncounterPacket)
- [CarterW24 SendDungeonOffer](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Game/Zones/StartingZone.cs)
- [CarterW24 entrance handler](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Gateway/Handlers/BaseEncounterPacket/EncounterParticipantRequestEntranceHandler.cs)
- [Sulphural SendDungeonOffer](https://github.com/Sulphural/Sanctuary/blob/8075c83f0eb1fd4ed6a829884e0351b24a800675/src/Sanctuary.Game/Zones/StartingZone.cs)
- [Public archived captures](https://github.com/Open-Source-Free-Realms/OpenSourceFreeRealms/tree/ace6a82e0ab07111be0fc8e899b5a0716724e137/ofrserver/Packets)
- [Capture reader/tooling](https://github.com/brennengreen/Sanctuary/tree/33ecdd5a8b5d2ef25bf3df48f393a8ea21183bf9/research/packet-analysis)

## E. Smallest safe live sequence

**None yet under the no-invented-values constraint.** The candidate sequence remains a useful target, but the current implementation sends none of its responses. Missing prerequisites are:

1. Verified creation/allocation and matching rules for H1/H2 for a new Activity 29 encounter.
2. Verified offer common/participant/team data and its relation to this player.
3. Correct offer presentation/type/profile values and either verified reward bundles or proof that omission is valid for this encounter.
4. A real readiness condition and cancellation/lifetime behavior; an arbitrary timer is not that condition.

The new join log identifies these blockers. The receive path can now record 41/108 completely without assuming that its first Int32 is ActivityId or discarding its final eight bytes. It makes no state changes and sends no acceptance.

## F/G. Files and diff

New:

- `src/Sanctuary.Packet/BaseEncounterInstancePacket.cs`: separate 12-byte header writer/reader; leaves existing shared encounter behavior intact.
- `src/Sanctuary.Packet/BaseEncounterPacket/EncounterStatePacket.cs`: 16-byte serializer.
- `src/Sanctuary.Packet/BaseEncounterPacket/EncounterZoneIsReadyPacket.cs`: 12-byte serializer with explicit headers.
- `src/Sanctuary.Packet/BaseEncounterPacket/EncounterParticipantRequestEntrancePacket.cs`: strict captured 20-byte parser, opaque suffix preserved.
- `src/Sanctuary.Packet/BaseEncounterPacket/EncounterDetailsResponsePacket.cs`: restricted 320-byte reference offer serializer, no launch/reward gameplay.
- `src/Sanctuary.Gateway/Handlers/BaseEncounterPacketHandler.cs`: message dispatch and receive diagnostics; no sends.
- `src/Sanctuary.Game.Tests/EncounterPacketTests.cs`: capture/layout/resource/regression tests.

Modified:

- `src/Sanctuary.Gateway/Handlers/PacketTunneledClientPacketHandler.cs`: one family-41 routing line.
- `src/Sanctuary.Gateway/Handlers/PacketTunneledClientWorldPacketHandler.cs`: one family-41 routing line.
- `src/Sanctuary.Gateway/Handlers/BaseActivityServicePacket/BaseActivityPacket/ActivityPacketJoinActivityRequestHandler.cs`: specific Activity 29 blocker log.

`encounter-offer.diff` is the exact incremental source/test diff against this milestone's starting state, excluding earlier changes. This report is the other review artifact. Prior queue/activity/join/WebAPI/database work is preserved. No existing reward serializer, zone manager or gameplay code was changed.

## H. Build and tests

Full solution build from `src`:

```powershell
dotnet build Sanctuary.slnx --no-restore -c Debug -m:1 --nologo -v:minimal -p:BaseOutputPath=C:/Users/lauri/Sanctuary/src/Sanctuary.Gateway/bin/encounter-offer-solution/
```

Final result: **all 16 projects built; 0 warnings, 0 errors**. An initial test-fixture collection-expression syntax error was corrected before the successful build.

```powershell
dotnet test Sanctuary.Game.Tests/Sanctuary.Game.Tests.csproj --no-build --no-restore -c Debug -p:BaseOutputPath=C:/Users/lauri/Sanctuary/src/Sanctuary.Gateway/bin/encounter-offer-solution/ --filter 'FullyQualifiedName~EncounterPacketTests|FullyQualifiedName~ActivityJoinPacketTests|FullyQualifiedName~ActivityPacketTests|FullyQualifiedName~MatchmakingPacketTests' --nologo
```

Result: **16 passed, 0 failed, 0 skipped**. Eight new test cases cover four archived header sets, 108 truncation/wrong-header/trailing-data rejection, all significant offsets in the reference empty-offer serializer, pinned Activity 29 metadata, and unchanged 41/132 bytes. Eight previous cases cover queue-list, activity-list and captured join regression. Synthetic 114 test values are explicitly test-only, not local encounter configuration.

Tests validate serializers and parsers; they do not prove an in-client offer, server readiness, dispatcher execution against a live client, or combat support. No service was started or stopped.

## I/J. Manual validation and next request

No services were started or stopped during implementation. This build is diagnostic; it is not expected to advance past Play. There is no need to repeat the client test to prove a new response, since no response has been added.

If validating the new logs manually:

1. Exit the client and stop only the currently running Gateway when ready. Keep Login/WebAPI running.
2. Retain the working environment/configuration. Set the Gateway working directory to `C:\Users\lauri\Sanctuary\src\Sanctuary.Gateway\bin\encounter-offer-solution\Debug\net9.0` and run `dotnet .\Sanctuary.Gateway.dll`.
3. Log in fresh, open Games, select Bandit Hideout, click Play once.
4. Check Info/Error logs in that output directory. Expect the existing decoded ActivityId=29 join and the new `Bandit Hideout offer blocked` message. The UI may still wait.
5. If a 41/108 arrives, expect both header values, the complete opaque suffix, tunnel type, connection and raw payload to be logged, followed by `Encounter entrance not accepted`. Unexpected lengths/opcodes remain unhandled and preserve the raw payload in logs.

After a future validated offer/readiness implementation, the target next client packet is **41/108**, inner header `29 00 6C 00`, observed as 20 bytes. No such live-client success is claimed for this fallback milestone.
