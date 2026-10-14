namespace Sciel.Blueprint.Data.Categories;

using Sciel.Blueprint.Core.Categories;
using Blueprint.Core.Common;

public class CategoryRepository: ICategoryRepository
{
  /// <inheritdoc />
  public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc />
  public Task<PagedResult<Category>> GetAllAsync(string? nameFilter, int paged, int pageSize, CancellationToken ct)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc />
  public Task AddAsync(Category cateogry, CancellationToken ct)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc />
  public Task UpdateAsync(Category cateogry, CancellationToken ct)
  {
    throw new NotImplementedException();
  }

  /// <inheritdoc />
  public Task RemoveAsync(Category cateogry, CancellationToken ct)
  {
    throw new NotImplementedException();
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
