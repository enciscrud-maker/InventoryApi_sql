using Inventory.Domain;
using Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Inventory.Infrastructure.Persistence;

namespace Inventory.Infrastructure.Persistence.Configurations;

public class TrGoodStatusHistoryConfiguration : IEntityTypeConfiguration<TrGoodStatusHistory>
{
    public void Configure(EntityTypeBuilder<TrGoodStatusHistory> b)
    {
        b.ToTable("TR_GoodStatusHistory");
        b.HasKey(x => x.GoodStatusHistoryId);
        b.Property(x => x.GoodStatusHistoryId).HasColumnName("GoodStatusHistoryId").ValueGeneratedOnAdd();
        b.Property(x => x.GoodId).HasColumnName("GoodId");
        b.Property(x => x.PreviousStatus).HasColumnName("PreviousStatus").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.NewStatus).HasColumnName("NewStatus").HasConversion<string>().HasMaxLength(30);
        b.Property(x => x.ActorUserId).HasColumnName("ActorUserId");
        b.Property(x => x.ActorNameSnapshot).HasColumnName("ActorNameSnapshot").HasMaxLength(200).IsRequired();
        b.Property(x => x.ActorNikSnapshot).HasColumnName("ActorNikSnapshot").HasMaxLength(50).IsRequired();
        b.Property(x => x.ChangedAt).HasColumnName("ChangedAt");
        b.Property(x => x.Notes).HasColumnName("Notes").HasMaxLength(500);
        b.Fk(x => x.Actor, x => x.ActorUserId);
    }
}
