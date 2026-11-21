using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MVC_QCUEventsManagement.Models;

namespace MVC_QCUEventsManagement.Data.Configurations;

public sealed class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.ToTable("EventsTable", "dbo", table =>
        {
            table.HasCheckConstraint("CK_EventsTable_Status",
                "([Status] COLLATE Latin1_General_100_BIN2 = 'Upcoming' AND DATALENGTH([Status]) = 8) OR ([Status] COLLATE Latin1_General_100_BIN2 = 'Cancelled' AND DATALENGTH([Status]) = 9) OR ([Status] COLLATE Latin1_General_100_BIN2 = 'Completed' AND DATALENGTH([Status]) = 9)");
            table.HasCheckConstraint("CK_EventsTable_Capacity", "[MaxCapacity] > 0 AND [CurrentRegistrations] >= 0 AND [CurrentRegistrations] <= [MaxCapacity]");
            table.HasCheckConstraint("CK_EventsTable_Schedule", "[EventStart] < [EventEnd] AND [RegStart] < [RegEnd] AND CONVERT(date, [RegStart]) < CONVERT(date, [EventStart]) AND CONVERT(date, [RegEnd]) < CONVERT(date, [EventStart])");
            table.HasCheckConstraint("CK_EventsTable_TargetYearLevel", "[TargetYearLevel] IS NULL OR [TargetYearLevel] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_EventsTable_CancellationReason", "[Status] <> 'Cancelled' OR LEN(LTRIM(RTRIM([CancellationReason]))) > 0 AND [CancellationReason] IS NOT NULL");
        });
        builder.HasKey(ev => ev.EventId).HasName("PK_EventsTable");
        builder.Property(ev => ev.EventId).UseIdentityColumn();
        builder.Property(ev => ev.Title).HasMaxLength(200).IsRequired();
        builder.Property(ev => ev.Description).HasColumnType("nvarchar(max)");
        builder.Property(ev => ev.VenueLocation).HasMaxLength(200).IsRequired();
        builder.Property(ev => ev.CurrentRegistrations).HasDefaultValue(0);
        builder.Property(ev => ev.EventStart).HasColumnType("datetime");
        builder.Property(ev => ev.EventEnd).HasColumnType("datetime");
        builder.Property(ev => ev.RegStart).HasColumnType("datetime");
        builder.Property(ev => ev.RegEnd).HasColumnType("datetime");
        builder.Property(ev => ev.Status).HasMaxLength(50).IsUnicode(false).HasDefaultValue("Upcoming").IsRequired();
        builder.Property(ev => ev.CancellationReason).HasMaxLength(500);
        builder.Property(ev => ev.TargetBranch).HasMaxLength(100);
        builder.Property(ev => ev.TargetDepartment).HasMaxLength(100);
        builder.Property(ev => ev.TargetProgram).HasMaxLength(100);
        builder.Property(ev => ev.EventPhotoPath).HasMaxLength(500);
        builder.Property(ev => ev.RowVersion).IsRowVersion();
        builder.HasOne(ev => ev.CreatedByUser).WithMany(user => user.CreatedEvents)
            .HasForeignKey(ev => ev.CreatedByUserId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_EventsTable_UserTable");
    }
}
