namespace Sciel.Blueprint.Service.Categoires;

using Npgsql.Replication;
using Sciel.Blueprint.Core.Categories;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;

public static class CategoriesEnpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/categories");
        group.MapGet("{id}", GetCategory);
        group.MapPost("", CreateCategory);
        group.MapDelete("{id}", DeleteCategory);
    }

    public static Results<Ok<CategoryResponse>, NotFound<string>> GetCategory(
        Guid id)
    {
        try
        {
            var category = new CategoryResponse()
            {
                Id = id,
                Name = "MyFakeName",
                Description = "This Category is fake",
            };
            return TypedResults.Ok(category);
        }
        catch(Exception ex){
            return TypedResults.NotFound(ex.Message);
        }
    }
    public static Results<Created<CategoryResponse>,BadRequest<string>> CreateCategory(CreateCategoryRequest request)
    {
        var category = new CategoryResponse()
        {
            Id = new Guid(),
            Name = request.Name,
            Description = request.Description,
        };
        return TypedResults.Created($"api/categories/{category.Id}", category);
    }
    public static Results<NoContent,NotFound<string>> DeleteCategory(Guid id)
    {
        return TypedResults.NoContent();
    }
}