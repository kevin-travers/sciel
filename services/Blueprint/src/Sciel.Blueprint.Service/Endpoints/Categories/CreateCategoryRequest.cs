namespace Sciel.Blueprint.Service.Categoires;

/// <summary>
/// A tenant-scoped grouping of blueprints (e.g. "Networking", "Compute").
/// Maps directly to the categories table.
/// </summary>
public class CreateCategoryRequest
{
    /// <summary>
    /// Display name shown in the UI (e.g. "Networking")a Must be unique within the
    /// owning organization and space.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Optional free-text description shown in the UI.
    /// </summary>
    public string? Description { get; set; }
}
