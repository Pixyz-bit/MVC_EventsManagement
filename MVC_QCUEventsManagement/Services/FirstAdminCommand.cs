using System.ComponentModel.DataAnnotations;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace MVC_QCUEventsManagement.Services;

internal static class FirstAdminCommand
{
    public static async Task<int> RunAsync(string[] args, AccountService accounts)
    {
        var email = args.Length == 2 ? args[1].Trim() : "";
        if (email.Length is 0 or > 150 || !new EmailAddressAttribute().IsValid(email))
        {
            Console.Error.WriteLine("Usage: --create-admin <email address>");
            return 1;
        }
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("Run this command in an interactive terminal to enter the password securely.");
            return 1;
        }

        var password = ReadPassword("Password: ");
        if (password is null) return 1;
        if (password.Length < 15 || string.IsNullOrWhiteSpace(password))
        {
            Console.Error.WriteLine("Choose a password containing 15 to 128 characters.");
            return 1;
        }
        if (password != ReadPassword("Confirm password: "))
        {
            Console.Error.WriteLine("The passwords did not match.");
            return 1;
        }

        try
        {
            if (await accounts.CreateFirstAdminAsync(email, password))
            {
                Console.WriteLine("Administrator created. You can now sign in with that email and password.");
                return 0;
            }
            Console.Error.WriteLine("An administrator already exists or that email is in use.");
        }
        catch (Exception exception) when (exception is DbUpdateException or SqlException)
        {
            Console.Error.WriteLine("Could not create the administrator. Check the database connection and migration, then retry.");
        }
        return 1;
    }

    private static string? ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var password = new StringBuilder();
        var excess = 0;
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key is ConsoleKey.Enter or ConsoleKey.Escape)
            {
                Console.WriteLine();
                if (key.Key == ConsoleKey.Escape) return null;
                if (excess == 0) return password.ToString();
                Console.Error.WriteLine("Passwords must be at most 128 characters.");
                return null;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (excess > 0) excess--;
                else if (password.Length > 0) password.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
            {
                if (password.Length < 128) password.Append(key.KeyChar);
                else excess++;
            }
        }
    }
}
