using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopService.Domain.Common;
using ShopService.Domain.Entities;

namespace ShopService.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.HasIndex(c => c.Slug).IsUnique();

        builder.Property(c => c.Name).HasMaxLength(Category.NameMaxLength);
        builder.Property(c => c.Slug).HasMaxLength(Slug.MaxLength);
        builder.Property(c => c.Description).HasMaxLength(Category.DescriptionMaxLength);

        builder.HasData(SeedData.Categories);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.HasIndex(p => p.CategoryId);
        builder.HasIndex(p => p.Name);

        builder.Property(p => p.Name).HasMaxLength(Product.NameMaxLength);
        builder.Property(p => p.Description).HasMaxLength(Product.DescriptionMaxLength);
        builder.Property(p => p.Price).HasPrecision(10, 2);
        builder.Property(p => p.ImageUrl).HasMaxLength(Product.ImageUrlMaxLength);
        builder.Property(p => p.Version).IsRowVersion();

        builder.HasOne(p => p.Category)
            .WithMany()
            .HasForeignKey(p => p.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(SeedData.Products);
    }
}
