using BrainArena.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BrainArena.Infrastructure.Data.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Topic).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.Text).IsRequired();
        builder.Property(q => q.Options).IsRequired();
        builder.Property(q => q.Explanation).IsRequired();
        builder.Property(q => q.Language).IsRequired().HasMaxLength(5);

        builder.HasIndex(q => new { q.Topic, q.Language });
    }
}
