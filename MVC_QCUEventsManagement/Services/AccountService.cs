using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MVC_QCUEventsManagement.Data;
using MVC_QCUEventsManagement.Models;

namespace MVC_QCUEventsManagement.Services;

public sealed class AccountService(ApplicationDbContext db, IPasswordHasher<User> passwordHasher)
{
    private static readonly User UnknownAccount = CreateUnknownAccount();

    public async Task<User?> AuthenticateAsync(string identifier, string password, CancellationToken cancellationToken)
    {
        identifier = identifier.Trim();
        var account = await db.Users.AsNoTracking()
            .Where(user => user.Email == identifier || (user.Role == "Student" && user.Student!.StudentId == identifier))
            .OrderByDescending(user => user.Email == identifier)
            .FirstOrDefaultAsync(cancellationToken);

        var hashAccount = account ?? UnknownAccount;
        PasswordVerificationResult result;
        try
        {
            result = passwordHasher.VerifyHashedPassword(hashAccount, hashAccount.PasswordHash, password);
        }
        catch (FormatException)
        {
            return null;
        }

        return account is { IsActive: true, Role: "Admin" or "Student" } && result != PasswordVerificationResult.Failed
            ? account : null;
    }

    public async Task<bool> CreateFirstAdminAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        var user = new User { Email = email, Role = "Admin", PasswordHash = "", PasswordSalt = "" };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (await db.Users.AnyAsync(account => account.Role == "Admin" || account.Email == email, cancellationToken))
        {
            return false;
        }

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private static User CreateUnknownAccount()
    {
        // Verify a throwaway hash for unknown identifiers so those requests also perform hashing work.
        var user = new User { Email = "", Role = "", PasswordSalt = "", PasswordHash = "" };
        user.PasswordHash = new PasswordHasher<User>().HashPassword(user, Guid.NewGuid().ToString());
        return user;
    }
}
