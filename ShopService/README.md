# ShopService (Shop API)

Mikroserwis sklepu: kategorie, produkty z wyszukiwaniem, składanie zamówień, historia zamówień klienta oraz panel
administratora (CRUD katalogu, podgląd zamówień i zmiana statusu). Nie ma własnych kont – ufa tokenom JWT
z [IdentityService](../IdentityService/README.md).

## Uruchomienie

Najprościej przez Aspire z katalogu głównego repozytorium (`dotnet run --project Aspire/Shop.AppHost`).
Bez Aspire (PostgreSQL z `IdentityService/docker-compose.yml`, IdentityService uruchomiony na `https://localhost:7001`):

```bash
dotnet run --project src/ShopService.Api --launch-profile https
```

- API `https://localhost:7002`, dokumentacja (Scalar) `https://localhost:7002/scalar`, gotowe żądania w `src/ShopService.Api/ShopService.Api.http`
- W trybie Development migracje stosują się przy starcie (baza `shop` powstaje automatycznie) razem z danymi przykładowymi:
  4 kategorie i 12 produktów (`Persistence/SeedData.cs`)
- Token: zaloguj się w IdentityService (`admin@shop.local` / `Admin123!` albo `user@shop.local` / `User1234!`)

Nowa migracja:

```bash
dotnet ef migrations add <Nazwa> --project src/ShopService.Infrastructure --startup-project src/ShopService.Api -o Persistence/Migrations
```

## Architektura

```
src/
  ShopService.Domain/          agregaty Category, Product, Order (+ OrderItem), value objecty (Money, silnie typowane Id),
                               zdarzenia domenowe (OrderPlaced, OrderStatusChanged, OrderCancelled), Result/Error
  ShopService.Application/     serwisy Category/Product/Order, handlery zdarzeń domenowych, interfejsy repozytoriów, DTO
  ShopService.Infrastructure/  EF Core + PostgreSQL, migracje z danymi przykładowymi, repozytoria, Unit of Work, outbox (RabbitMQ)
  ShopService.Api/             kontrolery, weryfikacja JWT (JWKS z IdentityService), OpenAPI
  ShopService.Contracts/       zdarzenia integracyjne dla innych serwisów (bez zależności)
tests/
  ShopService.UnitTests/       domena + testy architektury
```

Zależności: `Api → Infrastructure → Application → Domain` (pilnują tego testy architektury).

Najważniejsze decyzje:

- **Uwierzytelnianie** – `Jwt:Authority` = `Jwt:Issuer` IdentityService. Klucze publiczne pobierane są z
  `/.well-known/jwks.json`, więc rotacja klucza w IdentityService nie wymaga zmian tutaj. Audience `shop-api`,
  role z claimu `role`, Id użytkownika z `sub`.
- **Koszyk po stronie front-endu** – model danych nie ma encji koszyka. Shop.Web trzyma koszyk u siebie i wysyła
  go w `POST /api/orders`; serwer sam pobiera ceny i sprawdza dostępność, klient nie może podać ceny.
- **Język biznesu w kodzie** – zamówienie zmienia się metodami `MarkAsPaid()`, `Ship()`, `MarkAsDelivered()`,
  `Cancel()` i tak samo nazywają się endpointy. Niedozwolone przejście (np. wysyłka przed płatnością) to 409
  `order.invalid_status_transition`.
- **Zdarzenia domenowe** – agregat zgłasza fakt (`OrderPlaced`, `OrderStatusChanged`, `OrderCancelled`), a Unit of Work
  przed zapisem rozsyła go do handlerów (`Application/EventHandlers`). Handlery zwracają towar do magazynu po anulowaniu
  i zamieniają zdarzenia na integracyjne – wszystko w jednej transakcji ze zmianą. Nowa reakcja na zdarzenie =
  nowy handler, bez zmian w agregacie.
- **Outbox** – zdarzenia integracyjne zapisują się w tabeli `OutboxMessages` razem ze zmianą, a `OutboxProcessor`
  wysyła je w tle do RabbitMQ z ponawianiem (backoff do 1 h, max 10 prób). Awaria RabbitMQ nie gubi zdarzeń ani nie
  blokuje zamówień; kilka instancji może działać równolegle (`FOR UPDATE SKIP LOCKED`).
- **Idempotentne zamówienia** – nagłówek `Idempotency-Key` w `POST /api/orders` (np. GUID wygenerowany przy otwarciu
  podsumowania koszyka). Ponowne wysłanie zwraca 200 z tym samym zamówieniem zamiast tworzyć drugie; ten sam klucz
  z innymi pozycjami to 409 `order.idempotency_key_reused`. Wyścig równoległych żądań rozstrzyga unikalny indeks
  `(UserId, IdempotencyKey)`.
- **Money** – ceny i sumy to value object `Money` (nieujemna kwota w PLN, dokładność do grosza, dodawanie i mnożenie
  przez ilość). W bazie zwykła kolumna `numeric`, w API zwykła liczba.
- **Magazyn** – równoległe zakupy ostatniej sztuki chroni optymistyczna współbieżność (`xmin`) – przegrany dostaje
  409 `common.concurrency_conflict`.
- **Historia zamówień** – `OrderItem` przechowuje kopię nazwy i ceny produktu, a produktu użytego w zamówieniu
  nie da się usunąć (409 `product.used_in_orders`) – trzeba go oznaczyć jako nieaktywny.
- **Silnie typowane Id** – `CategoryId`, `ProductId`, `OrderId`, `OrderItemId`, `UserId` (jak w IdentityService):
  kompilator nie pozwoli pomylić identyfikatorów. W bazie to zwykłe kolumny `uuid`, w JSON i w adresach – zwykły GUID.
- **Kody błędów** – jak w IdentityService: każda odpowiedź błędu (ProblemDetails) ma pole `code`.

## Endpointy

| Metoda | Ścieżka | Dostęp | Opis |
|---|---|---|---|
| GET | `/api/categories`, `/api/categories/{id}` | publiczny | kategorie z liczbą aktywnych produktów |
| GET | `/api/products?categoryId=&search=&sort=&page=&pageSize=` | publiczny | aktywne produkty; `sort`: `Newest`, `Name`, `PriceAsc`, `PriceDesc` |
| GET | `/api/products/{id}` | publiczny | szczegóły produktu |
| POST | `/api/orders` (+ opcjonalny `Idempotency-Key`) | zalogowany | złożenie zamówienia (pozycje koszyka + adres dostawy) |
| GET | `/api/orders`, `/api/orders/{id}` | zalogowany | historia i szczegóły własnych zamówień |
| POST/PUT/DELETE | `/api/admin/categories`, `/{id}` | Admin | zarządzanie kategoriami |
| GET/POST/PUT/DELETE | `/api/admin/products`, `/{id}` | Admin | zarządzanie produktami (lista obejmuje nieaktywne) |
| GET | `/api/admin/orders?status=`, `/api/admin/orders/{id}` | Admin | wszystkie zamówienia |
| POST | `/api/admin/orders/{id}/mark-paid`, `/ship`, `/mark-delivered`, `/cancel` | Admin | obsługa zamówienia; `cancel` zwraca towar do magazynu |
| GET | `/health`, `/alive` | publiczny | readiness (z bazą i RabbitMQ) / liveness |

## Zdarzenia integracyjne (RabbitMQ)

Exchange `shop.events` (topic), routing key = nazwa zdarzenia, `message_id` unikalne (konsument powinien odrzucać
duplikaty – outbox gwarantuje dostarczenie co najmniej raz). Kontrakty w `ShopService.Contracts`.

| Routing key | Kiedy | Przykładowe użycie |
|---|---|---|
| `order.placed` | złożenie zamówienia | e-mail z potwierdzeniem, statystyki sprzedaży |
| `order.status_changed` | każda zmiana statusu (w tym anulowanie) | powiadomienie klienta o wysyłce |

## Do zrobienia w kolejnych krokach

- [ ] Testy integracyjne API na PostgreSQL w Testcontainers (wzorem IdentityService) – w tym rozsyłanie zdarzeń i idempotencja
- [ ] Konsument zdarzeń `identity.events` z RabbitMQ – anonimizacja zamówień po `user.deleted`, e-mail po `user.email_changed`
- [ ] Czyszczenie wysłanych wiadomości z `OutboxMessages` (jak `RetentionCleanupService` w IdentityService)
- [ ] Historia zmian statusu zamówienia (kto i kiedy) – kolejny handler `OrderStatusChanged`
- [ ] Anulowanie własnego zamówienia przez klienta (status `New`)
- [ ] Zdjęcia produktów (upload zamiast samego `ImageUrl`)
- [ ] Płatności – na razie `Paid` ustawia ręcznie administrator
