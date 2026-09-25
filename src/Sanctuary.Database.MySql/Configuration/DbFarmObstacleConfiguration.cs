using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sanctuary.Database.Entities;

namespace Sanctuary.Database.MySql.Configuration;

public sealed class DbFarmObstacleConfiguration : IEntityTypeConfiguration<DbFarmObstacle>
{
    public void Configure(EntityTypeBuilder<DbFarmObstacle> builder)
    {
        builder.HasKey(obstacle => obstacle.Id);
        builder.Property(obstacle => obstacle.Id).ValueGeneratedOnAdd();

        builder.Property(obstacle => obstacle.FarmKey).IsRequired().HasMaxLength(64);
        builder.Property(obstacle => obstacle.ObstacleKey).IsRequired().HasMaxLength(64);
        builder.HasIndex(obstacle => new { obstacle.CharacterId, obstacle.FarmKey, obstacle.ObstacleKey }).IsUnique();

        builder.Property(obstacle => obstacle.ClearedAtUtc).IsRequired();
        builder.Property(obstacle => obstacle.Created).IsRequired().HasDefaultValueSql("NOW()");

        builder.HasOne(obstacle => obstacle.Character)
            .WithMany(character => character.FarmObstacles)
            .HasForeignKey(obstacle => obstacle.CharacterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
