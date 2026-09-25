using Sanctuary.Core.IO;

namespace Sanctuary.Packet;

/// <summary>
/// One HashList record for FactoryPacketListToolsResponse (188/22).
/// Wire layout PROVEN (investigation §§17, 25.8); field meanings from FactoryTools.txt
/// column → local FactoryToolDefinition offset isomorphism (§25) — STRONGLY SUPPORTED,
/// not live-captured retail wire.
/// </summary>
public sealed class FactoryToolDefinitionNode
{
    /// <summary>
    /// HashList map key (node+0x54). EXPERIMENTAL assumption: equals ToolId.
    /// STRONGLY SUPPORTED, not live-proven.
    /// </summary>
    public int HashListKey;

    /// <summary>Body ToolId (node+0x00) — DS column 0.</summary>
    public int ToolId;

    /// <summary>HOUSING_INSTANCE_ID (node+0x04).</summary>
    public int HousingInstanceId;

    /// <summary>NAME_STRING_ID (node+0x08).</summary>
    public int NameStringId;

    /// <summary>ICON_ID (node+0x0C).</summary>
    public int IconId;

    /// <summary>REQUIREMENT_ID (node+0x10).</summary>
    public int RequirementId;

    /// <summary>COMPOSITE_EFFECT_ID (node+0x14).</summary>
    public int CompositeEffectId;

    /// <summary>TOOL_ITEM_ID (node+0x18).</summary>
    public int ToolItemId;

    /// <summary>CONSUMABLE_ITEM_ID / charge item id (node+0x1C).</summary>
    public int ChargeItemId;

    /// <summary>TOOLTIP_STRING_ID (node+0x20).</summary>
    public int TooltipStringId;

    /// <summary>DELETE_CONSUMABLES_ON_UNEQUIP (node+0x24).</summary>
    public bool DeleteConsumablesOnUnequip;

    /// <summary>NOTIFICATION_TYPE (node+0x28).</summary>
    public int NotificationType;

    /// <summary>HIDDEN (node+0x2C).</summary>
    public bool Hidden;

    /// <summary>FACTORY_CATEGORY string (node+0x30); int32 length + bytes, no NUL.</summary>
    public string? FactoryCategory;

    /// <summary>NEEDS_FUEL_NOTIFICATION_TYPE (node+0x40).</summary>
    public int NeedsFuelNotificationType;

    /// <summary>IS_BLUEPRINT_STAMPER (node+0x44).</summary>
    public bool IsBlueprintStamper;

    /// <summary>HIDE_IF_REQUIREMENT_FAILS (node+0x45).</summary>
    public bool HideIfRequirementFails;

    /// <summary>
    /// IsCurrentlyUseable (node+0x48). Server-authoritative wire bool.
    /// EXPERIMENTAL controlled-test value may be TRUE; NOT historical retail.
    /// </summary>
    public bool IsCurrentlyUseable;

    /// <summary>
    /// ToolId 4 Shovel from local FactoryTools.txt row
    /// <c>58^4^31036^34958^970^0^0^0^435253^0^197^0^FARMING^0^0^0^</c>.
    /// HashList key=4 and IsCurrentlyUseable=true are experimental assumptions.
    /// </summary>
    public static FactoryToolDefinitionNode CreateExperimentalShovel()
    {
        return new FactoryToolDefinitionNode
        {
            // EXPERIMENTAL: HashList key = ToolId (STRONGLY SUPPORTED, not live-proven).
            HashListKey = 4,
            ToolId = 4,
            HousingInstanceId = 58,
            NameStringId = 31036,
            IconId = 34958,
            RequirementId = 970,
            CompositeEffectId = 0,
            ToolItemId = 0,
            ChargeItemId = 0,
            TooltipStringId = 435253,
            DeleteConsumablesOnUnequip = false,
            NotificationType = 197,
            Hidden = false,
            FactoryCategory = "FARMING",
            NeedsFuelNotificationType = 0,
            IsBlueprintStamper = false,
            HideIfRequirementFails = false,
            // EXPERIMENTAL: protocol-valid server-authoritative TRUE for controlled test; NOT historical retail.
            IsCurrentlyUseable = true,
        };
    }

    public void Serialize(PacketWriter writer)
    {
        writer.Write(HashListKey);
        writer.Write(ToolId);
        writer.Write(HousingInstanceId);
        writer.Write(NameStringId);
        writer.Write(IconId);
        writer.Write(RequirementId);
        writer.Write(CompositeEffectId);
        writer.Write(ToolItemId);
        writer.Write(ChargeItemId);
        writer.Write(TooltipStringId);
        writer.Write(DeleteConsumablesOnUnequip);
        writer.Write(NotificationType);
        writer.Write(Hidden);
        writer.Write(FactoryCategory);
        writer.Write(NeedsFuelNotificationType);
        writer.Write(IsBlueprintStamper);
        writer.Write(HideIfRequirementFails);
        writer.Write(IsCurrentlyUseable);
    }
}
