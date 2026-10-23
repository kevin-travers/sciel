using Microsoft.EntityFrameworkCore;
using Sciel.Blueprint.Core.Categories;
using Sciel.Blueprint.Data.Categories;
using Sciel.Blueprint.Data.Common;

namespace Sciel.Blueprint.Data;

public class BlueprintDbContext: DbContext
{
    public BlueprintDbContext(DbContextOptions<BlueprintDbContext> options): base(options)
    {
    }   
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfiguration(new CategoryConfiguration());

        modelBuilder.Entity<Category>().HasQueryFilter(QueryFilterNames.SoftDelete, 
            category => category.DeletedAtUtc == null);
    }

}