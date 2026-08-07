using Domus.Domain.Common;
using Domus.Domain.Enums;

namespace Domus.Domain.Entities;

public class User : SoftDeletableEntity
{
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public UserStatus Status { get; private set; } = UserStatus.Active;

    public ICollection<TenantMembership> TenantMemberships { get; private set; } = new List<TenantMembership>();
    public ICollection<ResidenceMembership> ResidenceMemberships { get; private set; } = new List<ResidenceMembership>();
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    private User()
    {
    }

    public static User Create(string email, string passwordHash, string name)
    {
        return new User
        {
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            Name = name.Trim(),
            Status = UserStatus.Active
        };
    }

    public void UpdatePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Nome é obrigatório.", nameof(name));
        }

        Name = name.Trim();
    }

    public void Lock() => Status = UserStatus.Locked;

    public void Activate() => Status = UserStatus.Active;
}
