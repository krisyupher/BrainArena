using BrainArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrainArena.Infrastructure.Data.Configurations;

public class RoomPlayerConfiguration : IEntityTypeConfiguration<RoomPlayer>
{
    public void Configure(EntityTypeBuilder<RoomPlayer> builder)
    {
        builder.HasKey(rp => new { rp.RoomId, rp.UserId });

        builder.HasOne(rp => rp.Room)
            .WithMany(r => r.Players)
            .HasForeignKey(rp => rp.RoomId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(rp => rp.User)
            .WithMany()
            .HasForeignKey(rp => rp.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
