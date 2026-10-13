namespace Sciel.Blueprint.Core.Categories;

/// <summary>
/// A tenant-scoped grouping of blueprints (e.g. "Networking", "Compute").
/// Maps directly to the categories table.
/// </summary>
public class Category
{
    /// <summary>
    /// Unique identifier for the category.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Owning organization. Stamped by the repository from <c>IRequestContext</c> —
    /// never trust a caller-supplied value for this.
    /// </summary>
    public Guid OrganizationId { get; set; }

    /// <summary>
    /// Owning space within the organization. Stamped the same way as <see cref="OrganizationId"/>.
    /// </summary>
    public Guid SpaceId { get; set; }

    /// <summary>
    /// Display name shown in the UI (e.g. "Networking"). Must be unique within the
    /// owning organization and space.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional free-text description shown in the UI.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// UTC timestamp when the category was created.
    /// </summary>
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp of the most recent update.
    /// </summary>
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>
    /// UTC timestamp of when category was soft deleted.
    /// </summary>
    public DateTime? DeletedAtUtc { get; set; }
}
