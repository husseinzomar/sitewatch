using SiteWatch.Core.Security;

namespace SiteWatch.Infra.Security;

public class BCryptPasswordHasher : IPasswordHasher
{
    // BCrypt cost factor: hashing runs 2^WorkFactor rounds internally.
    // Higher = slower to brute-force but slower on every hash/verify call.
    private const int WorkFactor = 12;

    public string Hash(string password) =>
        BCrypt.Net.BCrypt.HashPassword(password, workFactor: WorkFactor);

    public bool Verify(string password, string hash) =>
        BCrypt.Net.BCrypt.Verify(password, hash);
}
