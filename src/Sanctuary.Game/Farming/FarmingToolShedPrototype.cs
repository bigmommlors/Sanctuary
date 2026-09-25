namespace Sanctuary.Game.Farming;

/// <summary>
/// Physical Tool Shed on the private Wilds farm (runtime NPC, not FarmObstacles).
/// Uses <see cref="FarmingPrototypeConfig.DebugToolShedModelId"/> (Models.txt 3415 /
/// farming_tool_shed_lv1_01.adr). Click opens existing Factory OpenToolshed 188/26.
/// </summary>
public static class FarmingToolShedPrototype
{
    /// <summary>
    /// Models.txt tool-shed ModelId. Same numeric value as Npcs.json courier Id 3415 —
    /// never pass this to <c>TrySpawnNpc(npcId, …)</c>.
    /// </summary>
    public const int ModelId = FarmingPrototypeConfig.DebugToolShedModelId;

    public const string ModelFileName = "farming_tool_shed_lv1_01.adr";

    public const string NpcName = FarmingPrototypeConfig.DebugToolShedNpcName;

    /// <summary>True when <paramref name="modelId"/> is the Models.txt tool shed (not an NPC definition id).</summary>
    public static bool IsToolShedModel(int modelId) => modelId == ModelId;

    /// <summary>Per-zone duplicate prevention for enter / re-spawn.</summary>
    public static bool ShouldSkipSpawnBecauseAlreadyPresent(bool alreadyPresentForZone) => alreadyPresentForZone;

    /// <summary>
    /// Click may call <c>SendExperimentalOpenToolshed</c> only inside the private Wilds farm.
    /// </summary>
    public static bool ShouldOpenToolshedOnInteract(bool isInWildsTestInstance) => isInWildsTestInstance;
}
