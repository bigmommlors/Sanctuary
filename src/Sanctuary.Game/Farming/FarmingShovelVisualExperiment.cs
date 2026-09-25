using System;

using Sanctuary.Packet;
using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Farming;

/// <summary>
/// EXPERIMENTAL / debug-only: temporary visual attach of original farming shovel ADR,
/// play farm_dig, then restore profile weapon visuals. Not retail Factory protocol.
/// Does not mutate inventory, profile Items, or equipment DB.
/// </summary>
public static class FarmingShovelVisualExperiment
{
    /// <summary>PROVEN asset name from client ADR / Assets_manifest.</summary>
    public const string ModelName = "tool_ar_ag_weapon_farmingshovel.adr";

    /// <summary>PROVEN TextureAlias from extracted farming shovel ADR.</summary>
    public const string TextureAlias = "freestyle-farming-M";

    /// <summary>PROVEN TintAlias from extracted farming shovel ADR.</summary>
    public const string TintAlias = "dyetint";

    /// <summary>FactoryTools COMPOSITE_EFFECT_ID for Shovel ToolId 4.</summary>
    public const int CompositeEffectId = 0;

    /// <summary>
    /// EXPERIMENTAL CANDIDATE: inventory Weapon slot (EquipmentSlotDefinitions id 7).
    /// STRONGLY SUPPORTED by ADR bone R_weapon; NOT proven retail Factory slot.
    /// </summary>
    public const int ExperimentalSlot = 7;

    /// <summary>
    /// EXPERIMENTAL CANDIDATE: ItemClass 37 (miner shovel) WieldType.
    /// NOT proven retail Factory WieldType.
    /// </summary>
    public const int ExperimentalWieldType = 0;

    /// <summary>
    /// Visual-only attach uses inventory-instance Id/Guid 0 (zero sentinel).
    /// Not an invented catalog item definition id.
    /// </summary>
    public const int ExperimentalItemInstanceId = 0;

    /// <summary>
    /// EXPERIMENTAL observation window for dig + held mesh before visual restore.
    /// Polled via Player second-tick; may complete slightly after this ms.
    /// </summary>
    public const int ExperimentalVisualDurationMs = 2500;

    public const string NotInWildsFarmMessage =
        "Debug shovelvisual only works in private Wilds Farm (!farmtest enter).";

    public const string AlreadyRunningMessage =
        "Shovel visual experiment already running.";

    public const string UnsafeRestoreMessage =
        "Cannot safely run shovelvisual: equipped Slot 7 weapon cannot be snapshotted for restore.";

    public const string SuccessMessage =
        "EXPERIMENTAL: temporary farming shovel attach + farm_dig; will restore weapon visuals shortly.";

    public static DateTimeOffset ComputeDueAtUtc(DateTimeOffset startedAtUtc) =>
        startedAtUtc.AddMilliseconds(ExperimentalVisualDurationMs);

    public static bool IsDue(DateTimeOffset nowUtc, DateTimeOffset dueAtUtc) =>
        nowUtc >= dueAtUtc;

    /// <summary>
    /// Attachment payload for temporary farming shovel. Bone R_weapon lives in ADR (not a packet field).
    /// </summary>
    public static CharacterAttachmentData CreateExperimentalAttachment() =>
        new()
        {
            ModelName = ModelName,
            TextureAlias = TextureAlias,
            TintAlias = TintAlias,
            TintId = 0,
            CompositeEffectId = CompositeEffectId,
            Slot = ExperimentalSlot
        };

    public static ClientUpdatePacketEquipItem CreateSelfEquipPacket(
        CharacterAttachmentData attachment,
        int profileId,
        int itemInstanceGuid,
        bool equip) =>
        new()
        {
            Guid = itemInstanceGuid,
            Attachment = CloneAttachment(attachment),
            ProfileId = profileId,
            Equip = equip
        };

    public static PlayerUpdatePacketEquipItemChange CreateVisibleEquipPacket(
        ulong playerGuid,
        int itemInstanceId,
        CharacterAttachmentData attachment,
        int profileId,
        int wieldType) =>
        new()
        {
            Guid = playerGuid,
            Id = itemInstanceId,
            Attachment = CloneAttachment(attachment),
            ProfileId = profileId,
            WieldType = wieldType
        };

    /// <summary>
    /// Unequip-style visible clear for a slot (ModelName left empty), matching inventory unequip.
    /// </summary>
    public static PlayerUpdatePacketEquipItemChange CreateVisibleClearSlotPacket(
        ulong playerGuid,
        int itemInstanceId,
        int slot,
        int profileId,
        int wieldType) =>
        new()
        {
            Guid = playerGuid,
            Id = itemInstanceId,
            Attachment = new CharacterAttachmentData { Slot = slot },
            ProfileId = profileId,
            WieldType = wieldType
        };

    public static ClientUpdatePacketUnequipSlot CreateSelfUnequipPacket(int slot, int profileId) =>
        new()
        {
            Slot = slot,
            ProfileId = profileId
        };

    public static CharacterAttachmentData CloneAttachment(CharacterAttachmentData source) =>
        new()
        {
            ModelName = source.ModelName ?? string.Empty,
            TextureAlias = source.TextureAlias ?? string.Empty,
            TintAlias = source.TintAlias ?? string.Empty,
            TintId = source.TintId,
            CompositeEffectId = source.CompositeEffectId,
            Slot = source.Slot
        };

    /// <summary>
    /// Snapshot for safety checks / logging. Profile Items are never mutated by this experiment.
    /// </summary>
    public readonly record struct EquipmentSnapshot(
        int ProfileId,
        bool HadEquippedItem,
        int? ProfileItemId,
        CharacterAttachmentData? Attachment,
        int WieldType);

    public static bool TryCreateSnapshot(
        int profileId,
        bool slotHasProfileItem,
        CharacterAttachmentData? attachment,
        int wieldType,
        int? profileItemId,
        out EquipmentSnapshot snapshot,
        out string? rejectMessage)
    {
        if (slotHasProfileItem && attachment is null)
        {
            snapshot = default;
            rejectMessage = UnsafeRestoreMessage;
            return false;
        }

        snapshot = new EquipmentSnapshot(
            profileId,
            slotHasProfileItem,
            profileItemId,
            attachment is null ? null : CloneAttachment(attachment),
            wieldType);
        rejectMessage = null;
        return true;
    }
}
