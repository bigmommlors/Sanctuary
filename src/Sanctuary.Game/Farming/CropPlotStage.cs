namespace Sanctuary.Game.Farming;

public enum CropPlotStage
{
    Empty = 0,
    Growing = 1,
    Harvestable = 2
}

/// <summary>
/// Client-visible growth models (Models.txt blueprint ranks). Distinct from <see cref="CropPlotStage"/>
/// so Planted/early can show without a separate DB state.
/// </summary>
public enum CropVisualStage
{
    Empty = 0,
    Planted = 1,
    Growing = 2,
    Ready = 3
}
