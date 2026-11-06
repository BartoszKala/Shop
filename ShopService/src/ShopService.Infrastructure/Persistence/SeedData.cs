using ShopService.Domain;

namespace ShopService.Infrastructure.Persistence;

/// <summary>
/// Dane przykładowe wpisane w migracje (HasData). Id i daty muszą być stałe – zmiana tutaj wymaga nowej migracji.
/// </summary>
internal static class SeedData
{
    public static readonly CategoryId Electronics = CategoryId.From(Guid.Parse("019a5a10-0000-7000-8000-000000000001"));
    public static readonly CategoryId Books = CategoryId.From(Guid.Parse("019a5a10-0000-7000-8000-000000000002"));
    public static readonly CategoryId HomeAndGarden = CategoryId.From(Guid.Parse("019a5a10-0000-7000-8000-000000000003"));
    public static readonly CategoryId Sport = CategoryId.From(Guid.Parse("019a5a10-0000-7000-8000-000000000004"));

    private static readonly DateTimeOffset SeededAt = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    public static object[] Categories =>
    [
        new { Id = Electronics, Name = "Elektronika", Slug = "elektronika", Description = (string?)"Telefony, słuchawki i akcesoria." },
        new { Id = Books, Name = "Książki", Slug = "ksiazki", Description = (string?)"Literatura piękna i poradniki." },
        new { Id = HomeAndGarden, Name = "Dom i ogród", Slug = "dom-i-ogrod", Description = (string?)"Wszystko do domu i na działkę." },
        new { Id = Sport, Name = "Sport", Slug = "sport", Description = (string?)"Sprzęt i odzież sportowa." }
    ];

    public static object[] Products =>
    [
        Product(1, Electronics, "Słuchawki bezprzewodowe", "Słuchawki nauszne z aktywną redukcją szumów, do 30 h pracy.", 349.99m, 25),
        Product(2, Electronics, "Smartfon 128 GB", "Ekran 6,5\", aparat 50 Mpx, bateria 5000 mAh.", 1299.00m, 10),
        Product(3, Electronics, "Ładowarka USB-C 65 W", "Szybkie ładowanie laptopów i telefonów.", 129.90m, 60),
        Product(4, Books, "Wiedźmin. Ostatnie życzenie", "Zbiór opowiadań Andrzeja Sapkowskiego.", 39.90m, 40),
        Product(5, Books, "Czysty kod", "Podręcznik dobrych praktyk programowania.", 89.00m, 15),
        Product(6, Books, "Pan Tadeusz", "Epopeja narodowa Adama Mickiewicza.", 24.99m, 30),
        Product(7, HomeAndGarden, "Ekspres przelewowy", "Pojemność 1,25 l, funkcja podtrzymania temperatury.", 219.00m, 12),
        Product(8, HomeAndGarden, "Zestaw narzędzi ogrodowych", "Łopatka, grabki i sekator w poręcznej torbie.", 79.50m, 20),
        Product(9, HomeAndGarden, "Lampka biurkowa LED", "Regulowana jasność i barwa światła.", 99.99m, 0),
        Product(10, Sport, "Mata do jogi", "Antypoślizgowa, grubość 6 mm.", 59.90m, 50),
        Product(11, Sport, "Hantle 2 × 5 kg", "Hantle winylowe, para.", 89.90m, 18),
        Product(12, Sport, "Bidon 750 ml", "Bez BPA, z ustnikiem sportowym.", 19.99m, 100)
    ];

    private static object Product(int number, CategoryId categoryId, string name, string description, decimal price, int stock) => new
    {
        Id = ProductId.From(Guid.Parse($"019a5a10-0000-7000-9000-{number:D12}")),
        CategoryId = categoryId,
        Name = name,
        Description = (string?)description,
        Price = Money.From(price),
        Stock = stock,
        ImageUrl = (string?)null,
        IsActive = true,
        CreatedAt = SeededAt,
        UpdatedAt = SeededAt
    };
}
