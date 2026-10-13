namespace Sciel.Blueprint.Data.Common;

/// <summary>
/// Shared names for EF Core named query filters, so the name used when defining a filter
/// (<c>HasQueryFilter</c>) and the name used to bypass it (<c>IgnoreQueryFilters</c>) can't
/// drift apart into a silent typo.
/// </summary>
internal static class QueryFilterNames
{
    /// <summary>
    /// Scopes a query to the current <c>IRequestContext</c>'s organization and space.
    /// </summary>
    public const string Tenant = "Tenant";

    /// <summary>
    /// Excludes soft-deleted rows (<c>DeletedAtUtc != null</c>).
    /// </summary>
    public const string SoftDelete = "SoftDelete";
}
