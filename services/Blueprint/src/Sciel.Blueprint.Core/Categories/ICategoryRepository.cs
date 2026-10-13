using Sciel.Blueprint.Core.Common;

namespace Sciel.Blueprint.Core.Categories;

/// <summary>
/// Persistence contract for <see cref="Category"/>. Implemented in Sciel.Blueprint.Data.
/// </summary>
public interface ICategoryRepository
{
    /// <summary>
    /// Looks up a category by id, scoped to the current tenant.
    /// </summary>
    /// <returns>The matching category, or <c>null</c> if none exists (or it's soft-deleted).</returns>
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Returns a page of categories for the current tenant, optionally filtered by a
    /// case-insensitive substring match on <see cref="Category.Name"/>.
    /// </summary>
    /// <param name="nameFilter">Substring to match against <see cref="Category.Name"/>, or <c>null</c>/empty for no filter.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Maximum number of items to return per page.</param>
    Task<PagedResult<Category>> GetAllAsync(
        string? nameFilter,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Adds a new category. The repository stamps <see cref="Category.OrganizationId"/> and
    /// <see cref="Category.SpaceId"/> — never trust caller-supplied values for these.
    /// <see cref="Category.CreatedAtUtc"/>/<see cref="Category.UpdatedAtUtc"/> are
    /// database-generated, not set here.
    /// </summary>
    Task AddAsync(Category category, CancellationToken ct = default);

    /// <summary>
    /// Persists changes to an existing category. <see cref="Category.UpdatedAtUtc"/> is kept
    /// current by a database trigger, not set here.
    /// </summary>
    Task UpdateAsync(Category category, CancellationToken ct = default);

    /// <summary>
    /// Soft-deletes a category by stamping <see cref="Category.DeletedAtUtc"/>. Does not
    /// remove the row — callers must have already loaded the entity to pass in.
    /// </summary>
    Task RemoveAsync(Category category, CancellationToken ct = default);

    /// <summary>
    /// Returns a page of soft-deleted categories for the current tenant, optionally filtered
    /// by a case-insensitive substring match on <see cref="Category.Name"/>. For browsing and
    /// selecting categories to restore.
    /// </summary>
    /// <param name="nameFilter">Substring to match against <see cref="Category.Name"/>, or <c>null</c>/empty for no filter.</param>
    /// <param name="page">1-based page number.</param>
    /// <param name="pageSize">Maximum number of items to return per page.</param>
    Task<PagedResult<Category>> GetDeletedAsync(
        string? nameFilter,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Restores a soft-deleted category by id, clearing <see cref="Category.DeletedAtUtc"/>.
    /// </summary>
    /// <returns>The restored category, or <c>null</c> if no soft-deleted category with this id exists for the current tenant.</returns>
    Task<Category?> RestoreAsync(Guid id, CancellationToken ct = default);
}
