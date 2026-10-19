using Balboa.BuildingBlocks.Persistence;
using Balboa.Modules.Sample.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Balboa.Modules.Sample.Infrastructure.Persistence;

internal sealed class SampleDbContext(DbContextOptions<SampleDbContext> options) : DbContext(options)
{
    public const string Schema = "sample";
    public const string MigrationsHistoryTable = "__EFMigrationsHistory";

    public DbSet<WidgetCatalogItem> Widgets => Set<WidgetCatalogItem>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) =>
        configurationBuilder.UseStronglyTypedIds(typeof(WidgetId).Assembly);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SampleDbContext).Assembly);
        modelBuilder.ApplyOutboxInbox();
    }
}

internal sealed class WidgetCatalogItemConfiguration : IEntityTypeConfiguration<WidgetCatalogItem>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<WidgetCatalogItem> builder)
    {
        builder.ToTable("widgets");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).HasStronglyTypedIdKey();
        builder.Property(w => w.Sku)
            .HasConversion(sku => sku.Value, value => Sku.Create(value).Value)
            .HasMaxLength(Sku.MaxLength)
            .IsRequired();
        builder.HasIndex(w => w.Sku).IsUnique();
        builder.Property(w => w.Name).HasMaxLength(WidgetCatalogItem.MaxNameLength).IsRequired();
        builder.Property(w => w.IsPublished);
        builder.Ignore(w => w.DomainEvents);
    }
}

/// <summary>Used only by `dotnet ef migrations ...`; never at runtime.</summary>
internal sealed class SampleDbContextFactory : IDesignTimeDbContextFactory<SampleDbContext>
{
    public SampleDbContext CreateDbContext(string[] args)
    {
        DbContextOptions<SampleDbContext> options = new DbContextOptionsBuilder<SampleDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=balboa_design_time",
                npgsql => npgsql.MigrationsHistoryTable(SampleDbContext.MigrationsHistoryTable, SampleDbContext.Schema))
            .Options;
        return new SampleDbContext(options);
    }
}
