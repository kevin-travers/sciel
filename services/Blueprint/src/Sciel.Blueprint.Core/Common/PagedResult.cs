namespace Sciel.Blueprint.Core.Common;

/// <summary>
/// A single page of results from a larger, filtered collection.
/// </summary>
/// <param name="Items">The items on this page.</param>
/// <param name="TotalCount">Total number of items across all pages that matched the filter.</param>
/// <param name="Page">1-based page number this result represents.</param>
/// <param name="PageSize">Maximum number of items per page.</param>
public record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize)
{
    /// <summary>
    /// Total number of pages available for <see cref="PageSize"/>.
    /// </summary>
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
