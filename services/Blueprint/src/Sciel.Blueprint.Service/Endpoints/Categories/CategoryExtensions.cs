using Sciel.Blueprint.Core.Categories;

namespace Sciel.Blueprint.Service.Categoires;

public static class CategoryExtensions
{
    extension(Category category)
    {
        public CategoryResponse ToCategoryResponse() => new()
        {
            Id = category.Id,
            Name = category.Name,
            Description = category.Description
        };
    }

    extension(CreateCategoryRequest createCategoryRequest)
    {
        public Category ToCategory() => new()
        {
            Name = createCategoryRequest.Name,
            Description = createCategoryRequest.Description
        };
    }
}