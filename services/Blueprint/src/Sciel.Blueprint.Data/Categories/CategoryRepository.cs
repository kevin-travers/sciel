using Sciel.Blueprint.Core.Categories;
using Sciel.Blueprint.Core.Common;
using Microsoft.EntityFrameworkCore;

namespace Sciel.Blueprint.Data.Categories;

public class CategoryRepository: ICategoryRepository
{
  private readonly BlueprintDbContext _dbContext;
  private readonly IRequestContext _requestContext;
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
  public Task<PagedResult<Category>> GetAllAsync(string? nameFilter, int page, int pageSize, CancellationToken ct)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc />
  public async Task<Category> AddAsync(Category category, CancellationToken ct)
  {
    ArgumentNullException.ThrowIfNull(category);

    // change to have DB make the ID
    category.Id = Guid.NewGuid();
    category.OrganizationId = _requestContext.OrganizationId;
    category.SpaceId = _requestContext.SpaceId;
    await _dbContext.Categories.AddAsync(category,ct);
    await _dbContext.SaveChangesAsync(ct);
    return category;
  }

  /// <inheritdoc />
  public Task UpdateAsync(Category category, CancellationToken ct)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc />
  public async Task RemoveAsync(Category category, CancellationToken ct)
  {
    ArgumentNullException.ThrowIfNull(category);

    // Have the database set the time not application code
    category.DeletedAtUtc = DateTime.UtcNow;
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
