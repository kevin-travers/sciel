namespace Sciel.Blueprint.Core.Common;

/// <inheritdoc/>
public class RequestContext : IRequestContext
{
    /// <inheritdoc/>
    public required Guid UserId { get; init; }
    /// <inheritdoc/>
    public required Guid OrganizationId { get; init; }
    /// <inheritdoc/>
    public required Guid SpaceId { get; init; }
    /// <inheritdoc/>
    public required string AccessToken { get; init; }
}

