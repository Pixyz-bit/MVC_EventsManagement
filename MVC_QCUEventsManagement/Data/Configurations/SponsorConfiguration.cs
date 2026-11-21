using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MVC_QCUEventsManagement.Models;

namespace MVC_QCUEventsManagement.Data.Configurations;

public sealed class SponsorConfiguration : IEntityTypeConfiguration<Sponsor>
{
    public void Configure(EntityTypeBuilder<Sponsor> builder)
    {
        builder.ToTable("SponsorListTable", "dbo");
        builder.HasKey(sponsor => sponsor.SponsorEntryId).HasName("PK_SponsorListTable");
        builder.Property(sponsor => sponsor.SponsorEntryId).UseIdentityColumn();
        builder.Property(sponsor => sponsor.SponsorName).HasMaxLength(150).IsRequired();
        builder.Property(sponsor => sponsor.CreatedAt).HasColumnType("datetime");
        builder.HasOne(sponsor => sponsor.Event).WithMany(ev => ev.Sponsors)
            .HasForeignKey(sponsor => sponsor.EventId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_SponsorListTable_EventsTable");
    }
}
