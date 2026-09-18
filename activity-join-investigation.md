# Activity 29 join investigation

## Capture and identity

The local activity-list build's logs confirm:

- `2026-09-17 14:31:47.0194`: BrowserV2 / SelectGame / Param 10.
- `2026-09-17 14:31:50.0573`: MiniGameDetail / PlayButtonClicked / Param 29.
- At the same timestamp, outer opcode 6 was unhandled with `0600010C000000A70001021D000000909DFFFF`.

Files: `src/Sanctuary.Gateway/bin/activity-list-solution/Debug/net9.0/Logs/dotnet-Info-2026-09-17.log` (lines 96, 98) and `dotnet-Error-2026-09-17.log` (line 6).

Outer framing: Int16 opcode 6, Boolean reliable=true, Int32 payload size=12, then the payload below.

| Inner bytes | Reference type / meaning | Value |
|---|---|---|
| A7 00 | Int16 BaseActivityServicePacket opcode | 167 |
| 01 | Byte BaseActivityPacket branch | 1 |
| 02 | Byte ActivityPacketJoinActivityRequest message | 2 |
| 1D 00 00 00 | Int32 ActivityId | 29 |
| 90 9D FF FF | Int32 TimezoneOffset | -25200 |

These are the exact fields read by the pinned parser, not inferred fields. The existing GameTimeSync handler uses the player's similarly named offset with Unix seconds; -25200 is numerically -7 hours. The join handler does not change the player's timezone.

`ClientActivityDefinitions.json` identifies activity 29 as **Bandit Hideout**: AppSystemId=2, Category=10, ServerType=1, NameId=5172, DisplayNameId=425259, DisplayDescriptionId=6995, PlayerCanJoin=true, Difficulty=1, and `bandit_hideout_detail.dds` / `bandit_hideout_thumb.dds`. Param 10 matches its category, but this alone does not establish the complete BrowserV2 parameter contract.

## Existing implementations and evidence limits

### Pinned origin/minigame

Commit `6fcdf6bfc44471369980a5b1b843cad25fd975c4` has a fully specified join parser and two dispatch layers. Its join handler only implements experimental branches for activities 7 and 1113. Activity 29 matches neither and receives no response.

Those two branches send:

1. ClientActivityLaunchPacketInviteDetails (family 175, Int32 subopcode 1).
2. ClientActivityLaunchPacketActivityLaunched (family 175, Int32 subopcode 5).
3. MiniGameInfoPacket (family 39, byte subopcode 16).

Their data includes activity-specific TCG/Flash settings and experimental constants; this is not evidence that the same sequence is correct for Bandit Hideout.

[Pinned reference handler](https://github.com/Open-Source-Free-Realms/Sanctuary/blob/6fcdf6bfc44471369980a5b1b843cad25fd975c4/src/Sanctuary.Gateway/Handlers/BaseActivityServicePacket/BaseActivityPacket/ActivityPacketJoinActivityRequestHandler.cs)

### Sulphural and other relevant forks

- Sulphural/Sanctuary `minigame`, commit `bc0982bf7bb9893fbc72f1ab5791f904b82b38c7`: same experimental 7/1113 handler; no activity-29 response.
- edenfps/Sanctuary `fishing`, commit `70c664bffc2d09655d0149a4a21b7c41e73799c5`: adds fishing-specific launch handling alongside 7/1113; no Bandit Hideout branch was found in the handler.
- CarterW24/combat-main `combat-v2`, commit **`3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4`**: **does contain activity-29 dungeon support**. The public fork list and these relevant branches were inspected; this is not a claim to have exhaustively searched every fork's history.

Relevant CarterW24 files:

- [ActivityPacketJoinActivityRequestHandler.cs](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Gateway/Handlers/BaseActivityServicePacket/BaseActivityPacket/ActivityPacketJoinActivityRequestHandler.cs): looks up DungeonCatalog.ByActivity and calls StartingZone.SendDungeonOffer.
- [DungeonDefinition.cs](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Game/Dungeons/DungeonDefinition.cs): contains DungeonCatalog and activity 29, Bandit Hideout, world `sg_bandit_hideout`.
- [StartingZone.cs](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Game/Zones/StartingZone.cs): SendDungeonOffer response sequence.
- [EncounterDetailsResponsePacket.cs](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Packet/BaseEncounterPacket/EncounterDetailsResponsePacket.cs): documented serializer, offer/launch selector, nested reward and objective data.
- [EncounterStatePacket.cs](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Packet/BaseEncounterPacket/EncounterStatePacket.cs).
- [EncounterZoneIsReadyPacket.cs](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Packet/BaseEncounterPacket/EncounterZoneIsReadyPacket.cs).
- [BaseEncounterPacketHandler.cs](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Gateway/Handlers/BaseEncounterPacketHandler.cs) and [EncounterParticipantRequestEntranceHandler.cs](https://github.com/CarterW24/combat-main/blob/3a4f9336e6761a4d33611fe5bd8c09311cb9f4b4/src/Sanctuary.Gateway/Handlers/BaseEncounterPacket/EncounterParticipantRequestEntranceHandler.cs): route and process the GO button request.

The exact **fork implementation sequence**, not independently confirmed retail activity-29 behavior, is:

1. C2S: 167 / 1 / 2, ActivityId=29.
2. S2C: 41 / 106, encounter=29, instance=1, states 2, 3, 4.
3. S2C: 41 / 114, encounter offer (Launch=false).
4. After 600 ms: S2C 41 / 107, zone ready; then 41 / 106, state 5.
5. On GO: C2S 41 / 108, EncounterParticipantRequestEntrance; the fork then enters its dungeon/party launch implementation.

Reasons this sequence was not copied into the current server:

- Its offer uses a fixed instance ID rather than a prepared instance here.
- Preview rewards, coins, XP and profile type come from FrostfangArenaZone.
- Its activity-29 description/difficulty (382845 / 3) differ from the pinned activity resource (6995 / 1).
- The delayed zone-ready packet uses default header IDs, whereas the offer uses activity 29 / instance 1; the serializer's own comments discuss matching the encounter ID.
- No working local encounter instance, launch state or cancellation path exists for this feature. Sending readiness would claim state that this milestone has not prepared.
- Serializer comments cite IDA analysis and a 2014-04-01 capture for another encounter (activity 174). Those comments are useful evidence leads, not an independently validated Bandit Hideout capture.

The archived OpenSourceFreeRealms repository's main and old-server-backup indexes and relevant server sources were searched. Its packet archive includes racing, demolition derby, soccer and generically named captures; no specifically identified Bandit Hideout trace or activity-29 join implementation was found in this inspection. The generic PCAPs were not decoded, so their contents remain an evidence gap.

[Archived packet directory](https://github.com/Open-Source-Free-Realms/OpenSourceFreeRealms/tree/ace6a82e0ab07111be0fc8e899b5a0716724e137/ofrserver/Packets)

## Implemented scope

- Copied the pinned join parser unchanged, retaining strict complete-frame consumption.
- Added the two reference dispatcher layers, with only the join message enabled.
- Routed family 167 from both existing tunnel handlers, matching reference ServerType 1/2 routing.
- Logged every decoded request field, connection, raw inner payload and loaded activity metadata.
- Explicitly logged that no response was sent and launch is unsupported.
- Unknown family-167 branches/messages remain unhandled and get structured warnings. Existing GatewayConnection warnings continue to record raw subsequent unhandled packets from other families, including 41, 39 and 175.
- Added captured-packet and malformed-packet tests. Existing corrected base-header validation remains in use.
- No response serializer, acceptance/readiness signal, profile switch, zone transition, database write or gameplay behavior was added. Returning true from the handler means the diagnostic request was recognized, not that an acceptance packet was sent.

The exact incremental source diff is `activity-join-parser.diff`; it excludes earlier queue-list, activity-list, WebAPI and database changes.

## Next safe milestone

Validate the encounter offer/readiness contract using the fork's referenced client routines or a decoded encounter capture, including header IDs, offer versus launch flags, reward data and cancellation. Then port only a coherent encounter-offer stage. The candidate next client packet is **41 / 108**, not necessarily 175 or 39.

This parser-only milestone is expected to log the real join and report unsupported launch. It does not unblock the UI or promise a subsequent client request.

## Files changed this milestone

Added source/test files:

- `src/Sanctuary.Packet/BaseActivityServicePacket/BaseActivityPacket/ActivityPacketJoinActivityRequest.cs`
- `src/Sanctuary.Gateway/Handlers/BaseActivityServicePacketHandler.cs`
- `src/Sanctuary.Gateway/Handlers/BaseActivityServicePacket/BaseActivityPacketHandler.cs`
- `src/Sanctuary.Gateway/Handlers/BaseActivityServicePacket/BaseActivityPacket/ActivityPacketJoinActivityRequestHandler.cs`
- `src/Sanctuary.Game.Tests/ActivityJoinPacketTests.cs`

Modified source files (one routing line each):

- `src/Sanctuary.Gateway/Handlers/PacketTunneledClientPacketHandler.cs`
- `src/Sanctuary.Gateway/Handlers/PacketTunneledClientWorldPacketHandler.cs`

Review artifacts: this report and `activity-join-parser.diff`. Ignored build outputs and two non-source `.cs.baseline` snapshots are under `src/Sanctuary.Gateway/bin/`.

## Build and tests

Full solution build, from `src`:

```powershell
dotnet build Sanctuary.slnx --no-restore -c Debug -m:1 --nologo -v:minimal -p:BaseOutputPath=C:/Users/lauri/Sanctuary/src/Sanctuary.Gateway/bin/activity-join-solution/
```

Final result: **succeeded, 0 warnings, 0 errors**, all 16 projects. The first attempt picked up two review snapshots as duplicate source files; renaming them with `.baseline` extensions resolved that build-only issue. No project configuration change was needed.

```powershell
dotnet test Sanctuary.Game.Tests/Sanctuary.Game.Tests.csproj --no-build --no-restore -c Debug -p:BaseOutputPath=C:/Users/lauri/Sanctuary/src/Sanctuary.Gateway/bin/activity-join-solution/ --filter 'FullyQualifiedName~ActivityJoinPacketTests|FullyQualifiedName~ActivityPacketTests|FullyQualifiedName~MatchmakingPacketTests' --nologo
```

Result: **8 passed, 0 failed, 0 skipped**. Two new tests validate the exact outer/inner capture and signed offset, and reject truncated frames, incorrect header bytes and trailing data. The other six test cases cover activity-list loading/serialization and the working queue-list protocol. The copied output resource matches the source SHA-256. No live-client or end-to-end handler execution was performed.

## Manual client test

No service was started or stopped during this work. When ready to test:

1. Exit the client, stop only the active Gateway, and keep Login/WebAPI running.
2. In the Gateway console, retain the same environment/configuration as the working activity-list build, then run:

   ```powershell
   Set-Location C:\Users\lauri\Sanctuary\src\Sanctuary.Gateway\bin\activity-join-solution\Debug\net9.0
   dotnet .\Sanctuary.Gateway.dll
   ```

3. Log in fresh, enter the world, verify activity-list logs, open Games, select Bandit Hideout, and click Play once.
4. Inspect Info/Error logs under the new output's `Logs` directory. Expect a decoded join with ActivityId=29, TimezoneOffset=-25200 for the same client timezone, ServerType=1, and a warning ending `No response sent; launch flow is not implemented.`
5. Record any subsequent raw unhandled packet. The client may still wait as before because this build does not send an offer or acceptance. Repeated Play attempts are unnecessary once the decoded request is recorded.

After a separately verified encounter-offer implementation, the fork-derived next-request candidate is family 41 / subopcode 108 (`29 00 6C 00` inner header), triggered by GO. That is a future validation target, not an expected outcome of this parser-only build.
