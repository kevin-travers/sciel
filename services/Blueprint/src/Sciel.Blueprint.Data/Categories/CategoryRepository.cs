namespace Sciel.Blueprint.Data.Categories;

using Sciel.Blueprint.Core.Categories;
using Sciel.Blueprint.Core.Common;
using Microsoft.EntityFrameworkCore;

public class CategoryRepository: ICategoryRepository
{
  protected readonly BlueprintDbContext _dbContext;
  protected readonly IRequestContext _requestContext;
  public CategoryRepository(BlueprintDbContext dbContext, IRequestContext requestContext)
  {
    _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    _requestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
  }


  /// <inheritdoc />
  public async Task<Category?> GetByIdAsync(Guid id, CancellationToken ct) => 
    await _dbContext.Categories.FirstOrDefaultAsync(category => 
      category.Id == id && 
      category.SpaceId == _requestContext.SpaceId && 
      category.OrganizationId == _requestContext.OrganizationId,
      ct);

  /// <inheritdoc />
  public Task<PagedResult<Category>> GetAllAsync(string? nameFilter, int paged, int pageSize, CancellationToken ct)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc />
  public async Task<Category> AddAsync(Category cateogry, CancellationToken ct)
  {
    // TODO: change to have DB make the ID
    cateogry.Id =Guid.NewGuid();
    cateogry.OrganizationId = _requestContext.OrganizationId;
    cateogry.SpaceId = _requestContext.SpaceId;
    await _dbContext.Categories.AddAsync(cateogry,ct);
    await _dbContext.SaveChangesAsync(ct);
    return cateogry;
  }

  /// <inheritdoc />
  public Task UpdateAsync(Category cateogry, CancellationToken ct)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc />
  public async Task RemoveAsync(Category cateogry, CancellationToken ct)
  {
    // TODO: Have the database set the time not application code
    cateogry.DeletedAtUtc = DateTime.UtcNow;
    await _dbContext.SaveChangesAsync(ct);
  }

  /// <inheritdoc />
  public Task<PagedResult<Category>> GetDeletedAsync( string? nameFilter, int page, int pageSize, CancellationToken ct)
  {
    throw new NotImplementedException();
  }
  
  /// <inheritdoc />
  public Task<Category?> RestoreAsync(Guid id, CancellationToken ct)
  {
    throw new NotImplementedException();
  }
}
