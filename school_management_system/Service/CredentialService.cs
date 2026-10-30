using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using school_management_system.Data;
using school_management_system.Models;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace school_management_system.Service;
    public class CredentialService(AppDbContext db, UserManager<ApplicationUser> users)
    {

        public async Task<int> NextNumberAsync(string key)
        {
            var updated = await db.IdCounters.Where(c => c.Key == key)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.Value, c => c.Value + 1));
            if (updated == 0)
            {
                db.IdCounters.Add(new IdCounter { Key = key, Value = 1 });
                await db.SaveChangesAsync();
                return 1;
            }
            return await db.IdCounters.Where(c => c.Key == key).Select(c => c.Value).SingleAsync();
        }


    public async Task<string> GenerateEmailAsync(string fullName, string domain)
    {
        var parts = fullName.Trim().ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => Regex.Replace(p, "[^a-z]", "")).Where(p => p.Length > 0).ToArray();
        if (parts.Length == 0) throw new DomainException("A full name is required.");

        var surname = parts[^1];
        var initials = string.Concat(parts[..^1].Select(p => p[0]));
        var baseName = parts.Length == 1 ? surname : $"{initials}.{surname}";

        var candidate = $"{baseName}@{domain}";
        var n = 1;
        while (await users.FindByEmailAsync(candidate) != null)
            candidate = $"{baseName}{++n}@{domain}";
        return candidate;
    }



    public static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz",
                     digits = "23456789", symbols = "!@#$%&*?";
        var all = upper + lower + digits + symbols;
        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)],
            lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)],
            symbols[RandomNumberGenerator.GetInt32(symbols.Length)]
        };
        while (chars.Count < 10) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray());
    }


    public async Task<(ApplicationUser User, string Password)> CreateAccountAsync(
      string fullName, string role, string emailDomain)
    {
        var email = await GenerateEmailAsync(fullName, emailDomain);
        var password = GeneratePassword();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = fullName.Trim(),
            MustChangePassword = true
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded) throw new DomainException(string.Join("; ", created.Errors.Select(e => e.Description)));
        var inRole = await users.AddToRoleAsync(user, role);
        if (!inRole.Succeeded) throw new DomainException(string.Join("; ", inRole.Errors.Select(e => e.Description)));
        return (user, password);
    }


}

