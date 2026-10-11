namespace Sciel.Blueprint.Core.Common;

/// <summary>
/// Read only access to the identity of the current rquest
/// </summary>
public interface IRequestContext
{
    /// <summary>
    /// The unique identifier of the authenticated user making the request.
    /// </summary>
    Guid UserId { get; }

    /// <summary>
    /// The unique identifier of the organization the request is scoped to.
    /// </summary>
    Guid OrganizationId { get; }

    /// <summary>
    /// The unique identifier of the space within the organization the request is scoped to.
    /// </summary>
    Guid SpaceId { get; }

    /// <summary>
    /// The raw bearer token presented with the request, without the "Bearer " prefix.
    /// </summary>
    string AccessToken { get; }
}
