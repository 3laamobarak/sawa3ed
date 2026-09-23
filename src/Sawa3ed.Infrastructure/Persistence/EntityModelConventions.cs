using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sawa3ed.Domain.Chat;
using Sawa3ed.Domain.Common;

namespace Sawa3ed.Infrastructure.Persistence;

internal static class EntityModelConventions
{
    public static void ApplyEntityConventions(this ModelBuilder model)
    {
        // Applies automatically to every future domain Entity, not a hand-maintained list.
        foreach (var type in model.Model.GetEntityTypes().Where(t => typeof(Entity).IsAssignableFrom(t.ClrType)))
        {
            var e = model.Entity(type.ClrType);
            var parameter = Expression.Parameter(type.ClrType, "entity");
            e.HasQueryFilter(Expression.Lambda(Expression.Not(Expression.Property(parameter, nameof(Entity.IsDeleted))), parameter));
            e.Property(nameof(Entity.Version)).IsConcurrencyToken();
            e.Property(nameof(Entity.CreatedBy)).HasMaxLength(450);
            e.Property(nameof(Entity.UpdatedBy)).HasMaxLength(450);
            e.Property(nameof(Entity.DeletedBy)).HasMaxLength(450);
        }
        model.Entity<ChatMessage>().HasQueryFilter(x => !x.IsDeleted && !x.Conversation.IsDeleted);
    }
}
