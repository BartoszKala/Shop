using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ShopService.Domain.Entities;

namespace ShopService.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasIndex(o => new { o.UserId, o.CreatedAt });
        builder.HasIndex(o => new { o.Status, o.CreatedAt });
        builder.HasIndex(o => new { o.UserId, o.IdempotencyKey })
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL");

        builder.Ignore(o => o.DomainEvents);

        builder.Property(o => o.CustomerEmail).HasMaxLength(Order.EmailMaxLength);
        builder.Property(o => o.IdempotencyKey).HasMaxLength(Order.IdempotencyKeyMaxLength);
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.Total).HasPrecision(12, 2);

        builder.ComplexProperty(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.Recipient).HasColumnName("ShippingRecipient").HasMaxLength(ShippingAddress.RecipientMaxLength);
            address.Property(a => a.Street).HasColumnName("ShippingStreet").HasMaxLength(ShippingAddress.StreetMaxLength);
            address.Property(a => a.PostalCode).HasColumnName("ShippingPostalCode").HasMaxLength(ShippingAddress.PostalCodeMaxLength);
            address.Property(a => a.City).HasColumnName("ShippingCity").HasMaxLength(ShippingAddress.CityMaxLength);
            address.Property(a => a.Phone).HasColumnName("ShippingPhone").HasMaxLength(ShippingAddress.PhoneMaxLength);
        });

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.Property(i => i.ProductName).HasMaxLength(Product.NameMaxLength);
        builder.Property(i => i.UnitPrice).HasPrecision(10, 2);

        // Pozycja trzyma kopię nazwy i ceny, więc historia zamówień nie zależy od późniejszych zmian w katalogu
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(i => i.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
