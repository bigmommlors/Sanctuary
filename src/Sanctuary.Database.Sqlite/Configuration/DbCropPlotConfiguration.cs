using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.Sqlite.Configuration;

public sealed class DbCropPlotConfiguration : IEntityTypeConfiguration<DbCropPlot>
{
    public void Configure(EntityTypeBuilder<DbCropPlot> builder)
    {
        builder.HasKey(plot => plot.Id);
        builder.Property(plot => plot.Id).ValueGeneratedOnAdd();

        builder.Property(plot => plot.PlotKey).IsRequired().HasMaxLength(64);
        builder.HasIndex(plot => new { plot.CharacterId, plot.PlotKey }).IsUnique();

        builder.Property(plot => plot.SeedDefinitionId);
        builder.Property(plot => plot.PlantedAtUtc);

        builder.Property(plot => plot.Created).IsRequired().HasDefaultValueSql("DATE()");

        builder.HasOne(plot => plot.Character)
            .WithMany(character => character.CropPlots)
            .HasForeignKey(plot => plot.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
