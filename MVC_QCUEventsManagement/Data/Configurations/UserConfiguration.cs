using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MVC_QCUEventsManagement.Models;

namespace MVC_QCUEventsManagement.Data.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("UserTable", "dbo", table => table.HasCheckConstraint("CK_UserTable_Role",
            "([Role] COLLATE Latin1_General_100_BIN2 = 'Admin' AND DATALENGTH([Role]) = 5) OR ([Role] COLLATE Latin1_General_100_BIN2 = 'Student' AND DATALENGTH([Role]) = 7)"));
        builder.HasKey(user => user.UserId).HasName("PK_UserTable");
        builder.Property(user => user.UserId).UseIdentityColumn();
        builder.Property(user => user.Email).HasMaxLength(150).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(256).IsUnicode(false).IsRequired();
        builder.Property(user => user.PasswordSalt).HasMaxLength(128).IsUnicode(false).IsRequired();
        builder.Property(user => user.Role).HasMaxLength(50).IsUnicode(false).IsRequired();
        builder.Property(user => user.IsActive).HasDefaultValue(true);
        builder.HasIndex(user => user.Email).IsUnique().HasDatabaseName("UX_UserTable_Email");
    }
}
