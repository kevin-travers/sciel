using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using Sciel.Blueprint.Core.Categories;

namespace Sciel.Blueprint.Data.Categories;

/// <summary>
/// EF Core mapping for <see cref="Category"/> — table, keys, indexes. The tenant/soft-delete
/// query filters live on <c>BlueprintDbContext.OnModelCreating</c> instead of here; see that
/// class for why.
/// </summary>
public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Table this entity maps to.
        builder.ToTable("Categories");

        // Primary key.
        builder.HasKey(category => category.Id);

        // Display name shown in the UI; required, capped at 128 chars.
        builder.Property(category => category.Name)
            .IsRequired()
            .HasMaxLength(128);

        // Optional free-text description; capped at 512 chars.
        builder.Property(category => category.Description)
            .HasMaxLength(512);

        // Database-generated on insert; Postgres' now()
        builder.Property(category => category.CreatedAtUtc)
            .HasDefaultValueSql("now()");

        // Database-generated on insert AND kept current by the categories_set_updated_at_utc
        // trigger (see migration) on every UPDATE. ValueGeneratedOnAddOrUpdate tells EF not to
        // send its own value and to re-read whatever the database computed after each save.
        builder.Property(category => category.UpdatedAtUtc)
            .HasDefaultValueSql("now()")
            .ValueGeneratedOnAddOrUpdate();

        // Enforces the per-tenant name uniqueness that CategoryNameConflictException exists for.
        // (OrganizationId, SpaceId) as the leading columns also covers plain tenant-scoped
        // lookups via leftmost-prefix matching, so no separate composite index is needed.
        builder.HasIndex(category => new { category.OrganizationId, category.SpaceId, category.Name })
            .IsUnique();
    }
}
