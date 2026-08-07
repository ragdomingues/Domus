namespace Domus.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
}

public abstract class SoftDeletableEntity : Entity
{
    public DateTimeOffset? DeletedAt { get; protected set; }
    public Guid? DeletedByUserId { get; protected set; }

    public bool IsDeleted => DeletedAt.HasValue;

    public virtual void SoftDelete(Guid deletedByUserId, DateTimeOffset? at = null)
    {
        if (IsDeleted)
        {
            return;
        }

        DeletedAt = at ?? DateTimeOffset.UtcNow;
        DeletedByUserId = deletedByUserId;
    }
}
