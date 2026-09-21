using BrainArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrainArena.Infrastructure.Data.Configurations;

public class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Name).IsRequired().HasMaxLength(40);
        builder.Property(r => r.Topic).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.GameMode).IsRequired().HasMaxLength(30);
        builder.Property(r => r.ShareCode).HasMaxLength(6);

        builder.HasIndex(r => r.ShareCode).IsUnique().HasFilter("\"ShareCode\" IS NOT NULL");

        builder.HasOne(r => r.HostUser)
            .WithMany()
            .HasForeignKey(r => r.HostUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
