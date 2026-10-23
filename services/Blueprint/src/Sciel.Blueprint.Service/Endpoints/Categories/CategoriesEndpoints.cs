
using Npgsql.Replication;
using Sciel.Blueprint.Core.Categories;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace Sciel.Blueprint.Service.Categoires;

public static class CategoriesEnpoints
{
    public static void MapCategoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/categories");
        group.MapGet("{id}", GetCategory);
        group.MapPost("", CreateCategory);
        group.MapDelete("{id}", DeleteCategory);
    }

    public static async Task<Results<Ok<CategoryResponse>, NotFound<string>>> GetCategory(
        Guid id,
        ICategoryRepository categoryRepository,
        CancellationToken ct)
    {
        try
        {
            var category = await categoryRepository.GetByIdAsync(id,ct);
            if(category is null)
                throw new Exception($"Category Id:{id} not found");
            return TypedResults.Ok(category.ToCategoryResponse());
        }
        catch(Exception ex){
            return TypedResults.NotFound(ex.Message);
        }
    }
    public static async Task<Results<Created<CategoryResponse>,BadRequest<string>>> CreateCategory(
        CreateCategoryRequest request,
        ICategoryRepository categoryRepository,
        CancellationToken ct)
    {
        try
        {
            var category = await categoryRepository.AddAsync(request.ToCategory(), ct);
            return TypedResults.Created($"api/categories/{category.Id}", category.ToCategoryResponse());
        }
        catch(Exception ex){
            return TypedResults.BadRequest(ex.Message);
        } 
    }
    public static async Task<Results<NoContent,NotFound<string>>> DeleteCategory(
        Guid id,
        ICategoryRepository categoryRepository,
        CancellationToken ct)
    {
        var category = await categoryRepository.GetByIdAsync(id, ct);
        if (category is null)
            return TypedResults.NotFound($"Category {id} not found.");

        await categoryRepository.RemoveAsync(category, ct);
        return TypedResults.NoContent();
    }
}