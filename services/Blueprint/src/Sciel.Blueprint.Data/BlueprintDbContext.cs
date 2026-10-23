namespace Sciel.Blueprint.Data;

using Microsoft.EntityFrameworkCore;
using Sciel.Blueprint.Core.Categories;
using Sciel.Blueprint.Data.Categories;
using Sciel.Blueprint.Data.Common;

public class BlueprintDbContext: DbContext
{
    public BlueprintDbContext(DbContextOptions<BlueprintDbContext> options): base(options)
    {
    }   
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CategoryConfiguration());

        modelBuilder.Entity<Category>().HasQueryFilter(QueryFilterNames.SoftDelete, 
            category => category.DeletedAtUtc == null);
    }

}