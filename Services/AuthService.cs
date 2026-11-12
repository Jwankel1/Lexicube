using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Lexicube.Services;

public sealed partial class AuthService(DataStore store)
{
    private const int Iterations = 210_000;
    private const int MaxFailures = 5;
    private static readonly TimeSpan Lockout = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<string, (int Count, DateTime Until)> _failures = new(StringComparer.OrdinalIgnoreCase);

    [GeneratedRegex("^[A-Za-z0-9_]{3,16}$")]
    private static partial Regex NameRule();

    public (User? User, string? Error) Register(string? username, string? password)
    {
        username = username?.Trim() ?? "";
        password ??= "";
        if (!NameRule().IsMatch(username)) return (null, "Usernames are 3–16 letters, numbers or underscores.");
        if (password.Length < 8) return (null, "Passwords need at least 8 characters.");
        if (password.Length > 128) return (null, "That password is too long.");

        var salt = RandomNumberGenerator.GetBytes(16);
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Salt = Convert.ToBase64String(salt),
            PasswordHash = Convert.ToBase64String(Hash(password, salt)),
            CreatedUtc = DateTime.UtcNow,
        };

        var added = store.Write(s =>
        {
            if (s.FindUser(username) is not null) return false;
            s.AddUser(user);
            return true;
        });
        store.Flush();
        return added ? (user, null) : (null, "That username is taken.");
    }

    public (User? User, string? Error) Login(string? username, string? password, string? remoteIp)
    {
        username = username?.Trim() ?? "";
        password ??= "";
        var key = $"{username}|{remoteIp}";

        if (_failures.TryGetValue(key, out var f) && f.Count >= MaxFailures)
        {
            if (f.Until > DateTime.UtcNow) return (null, "Too many attempts. Try again in a couple of minutes.");
            _failures.TryRemove(key, out _);
        }

        var user = store.Read(s => s.FindUser(username));
        // Hash even when the user is unknown so response time doesn't reveal which names exist.
        var salt = user is null ? new byte[16] : Convert.FromBase64String(user.Salt);
        var hash = Hash(password, salt);
        if (user is not null && CryptographicOperations.FixedTimeEquals(hash, Convert.FromBase64String(user.PasswordHash)))
        {
            _failures.TryRemove(key, out _);
            return (user, null);
        }

        _failures.AddOrUpdate(key, (1, DateTime.UtcNow + Lockout), (_, old) => (old.Count + 1, DateTime.UtcNow + Lockout));
        return (null, "Wrong username or password.");
    }

    public static ClaimsPrincipal Principal(User user) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username)],
            CookieAuthenticationDefaults.AuthenticationScheme));

    private static byte[] Hash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
}
