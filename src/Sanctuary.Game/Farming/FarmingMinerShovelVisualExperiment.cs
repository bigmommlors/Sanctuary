using System;

using Sanctuary.Packet.Common;

namespace Sanctuary.Game.Farming;

/// <summary>
/// EXPERIMENTAL / debug-only: temporary visual attach of inventory mining shovel ADR
/// (complete local DME/DDS), play farm_dig, then restore profile weapon visuals.
/// Separate from farming shovelvisual (missing DME/DDS). Not retail Factory protocol.
/// Does not mutate inventory, profile Items, or equipment DB.
/// </summary>
public static class FarmingMinerShovelVisualExperiment
{
    /// <summary>
    /// PROVEN ModelName from ClientItemDefinitions (e.g. catalog Id 1910) and local packs.
    /// Catalog Id is definition-only — never used as inventory instance Guid/Id.
    /// </summary>
    public const string ModelName = "tool_ar_ag_weapon_shovel.adr";

    /// <summary>PROVEN TextureAlias from ClientItemDefinitions shovel entries + miner ADR.</summary>
    public const string TextureAlias = "miner-steel-L2";

    /// <summary>PROVEN TintAlias from ClientItemDefinitions shovel entries + miner ADR.</summary>
    public const string TintAlias = "dyetint";

    /// <summary>
    /// CompositeEffectId absent on shovel ClientItemDefinitions entries → default 0
    /// (same as inventory equip when definition omits the field).
    /// </summary>
    public const int CompositeEffectId = 0;

    /// <summary>
    /// PROVEN inventory Weapon slot (EquipmentSlotDefinitions id 7 / ClientItemDefinitions Slot).
    /// </summary>
    public const int ExperimentalSlot = 7;

    /// <summary>
    /// PROVEN ItemClasses.txt Class 37 WIELD_TYPE = 0.
    /// </summary>
    public const int ExperimentalWieldType = 0;

    /// <summary>
    /// Visual-only attach uses inventory-instance Id/Guid 0 (zero sentinel).
    /// Not catalog definition Id 1910.
    /// </summary>
    public const int ExperimentalItemInstanceId = 0;

    /// <summary>
    /// Observation window for dig + held mesh before visual restore (~2.5s).
    /// Polled via Player second-tick; may complete slightly after this ms.
    /// </summary>
    public const int ExperimentalVisualDurationMs = 2500;

    public const string NotInWildsFarmMessage =
        "Debug minervisual only works in private Wilds Farm (!farmtest enter).";

    public const string AlreadyRunningMessage =
        "A shovel visual experiment is already running.";

    public const string UnsafeRestoreMessage =
        "Cannot safely run minervisual: equipped Slot 7 weapon cannot be snapshotted for restore.";

    public const string SuccessMessage =
        "EXPERIMENTAL: temporary mining shovel attach + farm_dig; will restore weapon visuals shortly.";

    public static DateTimeOffset ComputeDueAtUtc(DateTimeOffset startedAtUtc) =>
        startedAtUtc.AddMilliseconds(ExperimentalVisualDurationMs);

    public static bool IsDue(DateTimeOffset nowUtc, DateTimeOffset dueAtUtc) =>
        nowUtc >= dueAtUtc;

    /// <summary>
    /// Attachment payload for temporary mining shovel. Bone R_weapon lives in ADR (not a packet field).
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

    public static bool TryCreateSnapshot(
        int profileId,
        bool slotHasProfileItem,
        CharacterAttachmentData? attachment,
        int wieldType,
        int? profileItemId,
        out FarmingShovelVisualExperiment.EquipmentSnapshot snapshot,
        out string? rejectMessage)
    {
        // Reuse shovelvisual snapshot/clone helpers (same Slot 7 restore policy).
        if (!FarmingShovelVisualExperiment.TryCreateSnapshot(
                profileId,
                slotHasProfileItem,
                attachment,
                wieldType,
                profileItemId,
                out snapshot,
                out _))
        {
            rejectMessage = UnsafeRestoreMessage;
            return false;
        }

        rejectMessage = null;
        return true;
    }
}
