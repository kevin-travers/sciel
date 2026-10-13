namespace Sciel.Blueprint.Core.Categories;

/// <summary>
/// Thrown when a create/update would violate the uniqueness of <see cref="Category.Name"/>
/// within the owning organization and space. The repository translates the underlying
/// database unique-constraint violation into this type so callers (e.g. API middleware)
/// can map it to a meaningful response (409 Conflict) without depending on EF Core/DB types.
/// </summary>
public class CategoryNameConflictException : Exception
{
    /// <summary>
    /// The category name that caused the conflict.
    /// </summary>
    public string Name { get; }

    public CategoryNameConflictException(string name)
        : base($"A category named '{name}' already exists.")
    {
        Name = name;
    }
}
