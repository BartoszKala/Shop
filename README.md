# Sklep internetowy

Aplikacja sklepu w architekturze mikroserwisów: .NET 10, Blazor Web App, PostgreSQL 18, EF Core, RabbitMQ, Aspire.

| Część | Stan | Opis |
|---|---|---|
| [IdentityService](IdentityService/README.md) | gotowy | konta, logowanie, 2FA, role, JWT, RODO, panel admina użytkowników |
| [ShopService](ShopService/README.md) (Shop API) | wstępna wersja | katalog, wyszukiwanie, zamówienia, panel admina katalogu i zamówień |
| Shop.Web (Blazor) | do zrobienia | sklep i panel administratora |

## Uruchomienie

Wymagany Docker Desktop oraz .NET SDK 10 (wersja w `global.json`).

```bash
dotnet run --project Aspire/Shop.AppHost
```

Aspire uruchamia PostgreSQL, RabbitMQ, Mailpit (podgląd maili) i wszystkie serwisy, a w przeglądarce otwiera panel
z logami, tracingiem i linkami do każdej usługi.

## Testy

```bash
dotnet test --solution Shop.slnx
```

Testy integracyjne uruchamiają PostgreSQL w kontenerze (Testcontainers), więc potrzebują Dockera. To samo robi CI
(`.github/workflows/ci.yml`) przy każdym pushu i pull requeście.

## Struktura repozytorium

```
Aspire/               Shop.AppHost (orkiestracja lokalna), Shop.ServiceDefaults (OpenTelemetry, health checks, resilience)
IdentityService/      mikroserwis tożsamości (Clean Architecture)
ShopService/          Shop API: produkty, kategorie, zamówienia (ten sam układ warstw)
Directory.Build.props wspólne ustawienia: net10.0, nullable, ostrzeżenia = błędy, audyt pakietów NuGet
Directory.Packages.props  wersje wszystkich pakietów w jednym miejscu (Central Package Management)
global.json           wersja SDK i Microsoft.Testing.Platform dla `dotnet test`
```

Każdy nowy mikroserwis dostaje te same ustawienia automatycznie; w `.csproj` podaje się tylko nazwy pakietów, bez wersji.
