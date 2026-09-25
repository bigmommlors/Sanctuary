using System.Numerics;

namespace Sanctuary.Game.Farming;

/// <summary>
/// Farming prototype constants. Farnum public harness + private Wilds test instance.
/// </summary>
public static class FarmingPrototypeConfig
{
    // ---------------------------------------------------------------------------
    // Farnum public harness (debug only). !farmtest tp stays here.
    // ---------------------------------------------------------------------------

    /// <summary>
    /// FabledRealms (Resources/Zones/FabledRealms.json Id = 1).
    /// </summary>
    public const int ZoneDefinitionId = 1;

    public const string PlotKey = "fabled-prototype-1";

    // Proven source: PointOfInterests.json Id 23 "Farnum's Farm"
    //   Position = [-2161.46, -31.96, 371.82], Atlas sacredgrove / public FabledRealms.
    public const float PositionX = -2155.0f;
    public const float PositionY = -31.96f;
    public const float PositionZ = 375.0f;
    public const float Heading = 0f;

    public static Vector4 PlotPosition => new(PositionX, PositionY, PositionZ, 1f);

    // ---------------------------------------------------------------------------
    // Private Wilds Farm test instance (developer enter/leave — not retail ownership).
    // Scene: farming_wilds_farmstead_02 (client asset; covers House 34 spawn).
    // House 34 / ZoneId 34 / deed 11435 = standard Wilds Farm.
    // ---------------------------------------------------------------------------

    public const int WildsZoneDefinitionId = 34;
    public const string WildsZoneName = "farming_wilds_farmstead_02";
    public const string WildsPlotKey = "wilds-private-test-1";

    /// <summary>
    /// Persistence farm identity for private Wilds overgrown-farm obstacles (not the crop PlotKey).
    /// </summary>
    public const string WildsFarmKey = "wilds-private-farm-1";

    /// <summary>Houses.json Id 34 SpawnPosition.</summary>
    public const float WildsSpawnX = 1409f;
    public const float WildsSpawnY = 0f;
    public const float WildsSpawnZ = 750.8f;

    /// <summary>
    /// Temporary dirt plot inside House 34 BuildAreas center (not on spawn pad).
    /// BuildAreas Min=[1356,-100,778.2] Max=[1406,60,723.4].
    /// </summary>
    public const float WildsPlotX = 1381f;
    public const float WildsPlotY = 0f;
    public const float WildsPlotZ = 750.8f;
    public const float WildsPlotHeading = 0f;

    public static Vector4 WildsSpawnPosition => new(WildsSpawnX, WildsSpawnY, WildsSpawnZ, 1f);
    public static Vector4 WildsPlotPosition => new(WildsPlotX, WildsPlotY, WildsPlotZ, 1f);

    /// <summary>Houses.json Id 34 SpawnRotation stored as Vector4; zoning uses Quaternion.</summary>
    public static Quaternion WildsSpawnRotation => new(-1f, 0f, 0f, 0f);

    // ---------------------------------------------------------------------------
    // Persistent click-to-clear weed (NOT retail Factory 188).
    // Models.txt: 3424 farming_weeds_01.adr "farming - obstacle - weeds 01".
    // Auto-spawns on !farmtest enter when uncleared; debug !farmtest weed also ok.
    // Interaction reuses dirt-plot NPC pattern (IsInteractable + InteractAction).
    // Position is intentionally easy to tweak — keep clear of WildsPlot* crop loop.
    // ---------------------------------------------------------------------------

    /// <summary>PROVEN visual: farming_weeds_01.adr (Models.txt comment).</summary>
    public const int DebugWeedModelId = 3424;

    public const string WildsWeedObstacleKey = "wilds-weed-test-1";

    /// <summary>
    /// Nameplate label (AddNpc.Name). No proven InteractionList ButtonText NameId for
    /// "Pull Weed"; client interact affordance is IsInteractable + CursorId like crop plot.
    /// </summary>
    public const string DebugWeedNpcName = "Pull Weed";

    public const float DebugWeedX = 1395f;
    public const float DebugWeedY = 0f;
    public const float DebugWeedZ = 735f;
    public const float DebugWeedHeading = 0f;

    public static Vector4 DebugWeedPosition => new(DebugWeedX, DebugWeedY, DebugWeedZ, 1f);

    // ---------------------------------------------------------------------------
    // Persistent click-to-clear rock (NOT retail Factory 188).
    // Models.txt: 3441 farming_obstacles_rockpatch_big_01.adr "farming - obstacle - big rock patch"
    // (3442 = small rock patch — unused; prefer rockpatch 3441).
    // Auto-spawns on !farmtest enter when uncleared; debug !farmtest rock also ok.
    // ---------------------------------------------------------------------------

    /// <summary>PROVEN visual: farming_obstacles_rockpatch_big_01.adr (Models.txt comment).</summary>
    public const int DebugRockModelId = 3441;

    public const string WildsRockObstacleKey = "wilds-rock-test-1";

    public const string DebugRockNpcName = "Clear Rock";

    // Safe unused coord near private farm (not weed/crop/spawn).
    public const float DebugRockX = 1370f;
    public const float DebugRockY = 0f;
    public const float DebugRockZ = 735f;
    public const float DebugRockHeading = 0f;

    public static Vector4 DebugRockPosition => new(DebugRockX, DebugRockY, DebugRockZ, 1f);

    // ---------------------------------------------------------------------------
    // Persistent click-to-clear small tree (NOT retail Factory 188).
    // Models.txt: 3575 farming_obstacles_tree_wilds_small_01.adr (Wilds small tree;
    // 3573/3574 = big tree/stump, 3576 = small stump — unused).
    // Auto-spawns on !farmtest enter when uncleared; debug !farmtest tree also ok.
    // ---------------------------------------------------------------------------

    /// <summary>PROVEN visual: farming_obstacles_tree_wilds_small_01.adr.</summary>
    public const int DebugTreeModelId = 3575;

    public const string WildsTreeObstacleKey = "wilds-tree-test-1";

    public const string DebugTreeNpcName = "Clear Tree";

    /// <summary>All prototype overgrown-farm obstacle keys for this private Wilds farm.</summary>
    public static readonly string[] WildsObstacleKeys =
    [
        WildsWeedObstacleKey,
        WildsRockObstacleKey,
        WildsTreeObstacleKey
    ];

    // Safe unused coord near private farm (not weed/crop/spawn/rock).
    public const float DebugTreeX = 1400f;
    public const float DebugTreeY = 0f;
    public const float DebugTreeZ = 765f;
    public const float DebugTreeHeading = 0f;

    public static Vector4 DebugTreePosition => new(DebugTreeX, DebugTreeY, DebugTreeZ, 1f);

    /// <summary>Bumbleberry Seed (ClientItemDefinitions / CoinStoreItems).</summary>
    public const int SeedDefinitionId = 38715;

    /// <summary>Bumbleberry harvest item.</summary>
    public const int HarvestDefinitionId = 38753;

    public const int GrowthSeconds = 60;

    /// <summary>
    /// Seconds of Planted/early visual before switching to Growing stalk.
    /// Rank-1 mound → rank-3 stalk while DB stage remains Growing.
    /// </summary>
    public const int PlantedVisualSeconds = 20;

    // Models.txt growth ranks (PROVEN comments):
    //   rank 0 withered (generic bush 3452), rank 1 mound 3417, rank 2 sprout 3562,
    //   rank 3 stalk 3451 (generic bush), rank 4 mature 3446, rank 5 harvestable 3445.
    // Bumbleberry has crop-specific rank 4/5 only; early ranks share generic bush/sprout.
    // Foundations (3416/3453) are separate from blueprints — retail keeps foundation under crop.

    /// <summary>
    /// PROVEN foundation: farming_plot_dirt_sacred_grove_01.adr
    /// ("farming - foundation - basic wilds dirt plot"). Always kept under the crop.
    /// </summary>
    public const int EmptyModelId = 3416;

    /// <summary>
    /// PROVEN planted/early: farming_dirt_mound_01.adr
    /// ("farming - blueprint - rank 1 (generic)").
    /// </summary>
    public const int PlantedModelId = 3417;

    /// <summary>
    /// STRONGLY SUPPORTED growing: farming_bush_stalk_01.adr
    /// ("farming - blueprint - rank 3 (generic bush)"). No bumbleberry-specific stalk exists.
    /// </summary>
    public const int GrowingModelId = 3451;

    /// <summary>
    /// PROVEN Ready/harvestable: farming_bumbleberry_bush_harvestable_01.adr
    /// ("farming - blueprint - rank 5 (bumbleberry)"). Prefer over rank-4 mature for Ready.
    /// </summary>
    public const int HarvestableModelId = 3445;

    /// <summary>
    /// PROVEN mature (rank 4): farming_bumbleberry_bush_mature_01.adr — retail pre-harvest stage.
    /// Not used while prototype Ready maps to harvestable rank 5.
    /// </summary>
    public const int MatureModelId = 3446;

    /// <summary>
    /// PROVEN sprout (rank 2): farming_sprout_01.adr — unused while Planted uses rank-1 mound.
    /// </summary>
    public const int SproutModelId = 3562;

    // 3453 farming_dirt_mound_follow_01.adr = fallow foundation — unused; Empty stays 3416.

    public const string FoundationNpcName = "Crop Plot";
    public const string CropNpcName = "Crop Visual";

    public const int InteractRange = 12;
    public const byte CursorId = 18;

    /// <summary>Logical DB/interact stage → not used for foundation ModelId (foundation stays EmptyModelId).</summary>
    public static int ModelIdFor(CropPlotStage stage) => stage switch
    {
        CropPlotStage.Growing => GrowingModelId,
        CropPlotStage.Harvestable => HarvestableModelId,
        _ => EmptyModelId
    };

    public static int ModelIdForVisual(CropVisualStage visual) => visual switch
    {
        CropVisualStage.Planted => PlantedModelId,
        CropVisualStage.Growing => GrowingModelId,
        CropVisualStage.Ready => HarvestableModelId,
        _ => EmptyModelId
    };

    public static string AssetNameForVisual(CropVisualStage visual) => visual switch
    {
        CropVisualStage.Planted => "farming_dirt_mound_01.adr",
        CropVisualStage.Growing => "farming_bush_stalk_01.adr",
        CropVisualStage.Ready => "farming_bumbleberry_bush_harvestable_01.adr",
        _ => "farming_plot_dirt_sacred_grove_01.adr"
    };
}
