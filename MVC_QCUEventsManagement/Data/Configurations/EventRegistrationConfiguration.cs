using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MVC_QCUEventsManagement.Models;

namespace MVC_QCUEventsManagement.Data.Configurations;

public sealed class EventRegistrationConfiguration : IEntityTypeConfiguration<EventRegistration>
{
    public void Configure(EntityTypeBuilder<EventRegistration> builder)
    {
        builder.ToTable("EventRegistrationTable", "dbo", table =>
        {
            table.HasCheckConstraint("CK_EventRegistrationTable_Status",
                "([Status] COLLATE Latin1_General_100_BIN2 = 'NoShow' AND DATALENGTH([Status]) = 6) OR ([Status] COLLATE Latin1_General_100_BIN2 = 'Present' AND DATALENGTH([Status]) = 7) OR ([Status] COLLATE Latin1_General_100_BIN2 = 'Cancelled' AND DATALENGTH([Status]) = 9)");
            table.HasCheckConstraint("CK_EventRegistrationTable_Year", "[CurrentYearLvl] BETWEEN 1 AND 5");
            table.HasCheckConstraint("CK_EventRegistrationTable_Section", "LEN(LTRIM(RTRIM([CurrentSection]))) > 0");
            table.HasCheckConstraint("CK_EventRegistrationTable_CheckIn", "([Status] = 'NoShow' AND [CheckInTimestamp] IS NULL) OR ([Status] = 'Present' AND [CheckInTimestamp] IS NOT NULL) OR [Status] = 'Cancelled'");
        });
        builder.HasKey(registration => registration.EventRegistrationId).HasName("PK_EventRegistrationTable");
        builder.Property(registration => registration.EventRegistrationId).UseIdentityColumn();
        builder.Property(registration => registration.StudentId).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(registration => registration.CurrentSection).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(registration => registration.Status).HasMaxLength(50).IsUnicode(false).HasDefaultValue("NoShow").IsRequired();
        builder.Property(registration => registration.CheckInTimestamp).HasColumnType("datetime");
        builder.Property(registration => registration.RowVersion).IsRowVersion();
        builder.HasIndex(registration => new { registration.EventId, registration.StudentId })
            .IsUnique().HasFilter("[Status] <> 'Cancelled'")
            .HasDatabaseName("UX_EventRegistrationTable_ActiveStudentEvent");
        builder.HasIndex(registration => registration.StudentId).HasDatabaseName("IX_EventRegistrationTable_StudentId");
        builder.HasOne(registration => registration.Event).WithMany(ev => ev.Registrations)
            .HasForeignKey(registration => registration.EventId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_EventRegistrationTable_EventsTable");
        builder.HasOne(registration => registration.Student).WithMany(student => student.Registrations)
            .HasForeignKey(registration => registration.StudentId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_EventRegistrationTable_StudentTable");
    }
}
