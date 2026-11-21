using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MVC_QCUEventsManagement.Models;

namespace MVC_QCUEventsManagement.Data.Configurations;

public sealed class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("StudentTable", "dbo");
        builder.HasKey(student => student.StudentId).HasName("PK_StudentTable");
        builder.Property(student => student.StudentId).HasMaxLength(50).IsUnicode(false).ValueGeneratedNever();
        builder.Property(student => student.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(student => student.MiddleName).HasMaxLength(100);
        builder.Property(student => student.LastName).HasMaxLength(100).IsRequired();
        builder.Property(student => student.Gender).HasMaxLength(20).IsUnicode(false).IsRequired();
        builder.Property(student => student.CampusBranch).HasMaxLength(100).IsRequired();
        builder.Property(student => student.Department).HasMaxLength(100).IsRequired();
        builder.Property(student => student.Program).HasMaxLength(100).IsRequired();
        builder.Property(student => student.BirthDate).HasColumnType("date");
        builder.HasOne(student => student.User).WithOne(user => user.Student)
            .HasForeignKey<Student>(student => student.UserId).OnDelete(DeleteBehavior.NoAction)
            .HasConstraintName("FK_StudentTable_UserTable");
        builder.HasIndex(student => student.UserId).IsUnique().HasDatabaseName("UX_StudentTable_UserId");
    }
}
