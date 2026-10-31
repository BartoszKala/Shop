# IdentityService

Mikroserwis kont użytkowników sklepu: rejestracja z potwierdzeniem e-maila i zgodami, logowanie (z opcjonalnym 2FA
przez aplikację Authenticator), sesje na urządzeniach, zmiana hasła i e-maila, RODO (zgody, eksport, usunięcie konta),
dziennik zdarzeń bezpieczeństwa, zarządzanie użytkownikami dla administratora oraz wydawanie tokenów JWT (role `User`, `Admin`).

## Uruchomienie

Najprościej przez Aspire z katalogu głównego repozytorium (`dotnet run --project Aspire/Shop.AppHost`).
Bez Aspire:

```bash
docker compose up -d          # PostgreSQL :5432, RabbitMQ :5672 (UI :15672), Mailpit :1025 (UI http://localhost:8025)
dotnet run --project src/IdentityService.Api --launch-profile https
```

- API `https://localhost:7001`, dokumentacja (Scalar) `https://localhost:7001/scalar`, gotowe żądania w `src/IdentityService.Api/IdentityService.Api.http`
- W trybie Development migracje stosują się przy starcie, a seed tworzy konta `admin@shop.local` / `Admin123!` i `user@shop.local` / `User1234!`
- Klucz RSA do JWT generuje się przy pierwszym starcie w `keys/signing-key.pem` (ignorowany przez git)

Nowa migracja (nigdy nie edytuj ani nie generuj od nowa tych, które trafiły na serwer):

```bash
dotnet ef migrations add <Nazwa> --project src/IdentityService.Infrastructure --startup-project src/IdentityService.Api -o Persistence/Migrations
```

## Architektura

```
src/
  IdentityService.Domain/          encje z fabrykami Create (Result), silnie typowane Id (UserId, SessionId...), Result/Error
  IdentityService.Application/     przypadki użycia: Auth, Account, TwoFactor, Privacy, AdminUser, Token; interfejsy; DTO
  IdentityService.Infrastructure/  EF Core + migracje, Identity, JWT/RSA, outbox (SMTP, RabbitMQ), HIBP, retencja danych
  IdentityService.Api/             kontrolery, uwierzytelnianie JWT, rate limiting, reverse proxy, OpenAPI
  IdentityService.Contracts/       zdarzenia integracyjne dla innych serwisów (bez zależności)
tests/
  IdentityService.UnitTests/       domena + testy architektury (reguła zależności warstw)
  IdentityService.IntegrationTests/  całe API na PostgreSQL w Testcontainers
```

Zależności: `Api → Infrastructure → Application → Domain` (pilnują tego testy architektury).

Najważniejsze decyzje:

- **Outbox** – e-maile i zdarzenia integracyjne zapisują się w tej samej transakcji co zmiana (`OutboxMessages`),
  a `OutboxProcessor` wysyła je w tle z ponawianiem (backoff do 1 h, max 10 prób). Nic nie ginie przy awarii SMTP
  czy RabbitMQ; kilka instancji serwisu może działać równolegle (`FOR UPDATE SKIP LOCKED`).
- **Sesje** – każde logowanie to sesja (`SessionId`, claim `sid`) przetrwała rotację refresh tokenów. Ponowne użycie
  wymienionego refresh tokenu = podejrzenie kradzieży → wylogowanie wszystkich sesji.
- **Role** – stały zbiór (`AppRoles`) w tabeli `UserRoles`; ASP.NET Core Identity działa bez tabel ról.
- **Kody błędów** – każda odpowiedź błędu (ProblemDetails) ma pole `code`, np. `auth.invalid_credentials`.
  Front-end tłumaczy po kodzie, nie po treści komunikatu.

## Endpointy

| Metoda | Ścieżka | Opis |
|---|---|---|
| POST | `/api/auth/register` | rejestracja (`acceptTerms` wymagane, `marketingConsent` opcjonalne) |
| POST | `/api/auth/confirm-email`, `/resend-confirmation` | aktywacja konta |
| POST | `/api/auth/login`, `/login/2fa`, `/login/recovery-code` | logowanie (drugi krok gdy włączone 2FA) |
| POST | `/api/auth/refresh`, `/logout` | rotacja tokenu, wylogowanie sesji |
| POST | `/api/auth/forgot-password`, `/reset-password` | reset hasła |
| GET/PUT | `/api/account/me` | profil (m.in. `requiresTermsAcceptance` po zmianie regulaminu) |
| POST | `/api/account/change-password`, `/change-email`, `/confirm-email-change` | zmiana hasła / e-maila |
| GET/DELETE | `/api/account/sessions`, `/sessions/{id}` | urządzenia, wylogowanie jednego lub wszystkich innych |
| GET | `/api/account/security-events` | historia logowań i zmian na koncie |
| GET/POST | `/api/account/2fa`, `/2fa/authenticator`, `/2fa/enable`, `/2fa/disable`, `/2fa/recovery-codes` | 2FA |
| GET/PUT/POST | `/api/account/privacy/consents`, `/consents/marketing`, `/accept-terms` | zgody |
| GET | `/api/account/privacy/export` | eksport wszystkich danych (art. 15 i 20 RODO) |
| POST | `/api/account/privacy/delete-account` | usunięcie konta (art. 17 RODO) |
| GET | `/api/admin/users`, `/api/admin/users/{id}` | wyszukiwanie i szczegóły użytkowników (rola Admin) |
| PUT/POST/DELETE | `/api/admin/users/{id}/roles`, `/lock`, `/unlock`, `/force-password-reset`, `/sessions` | zarządzanie kontem |
| GET | `/api/admin/users/{id}/security-events` | dziennik zdarzeń użytkownika |
| GET | `/health`, `/alive` | readiness (z bazą i RabbitMQ) / liveness |
| GET | `/.well-known/openid-configuration`, `/.well-known/jwks.json` | klucze publiczne dla innych serwisów |

## Zdarzenia integracyjne (RabbitMQ)

Exchange `identity.events` (topic), routing key = nazwa zdarzenia, `message_id` unikalne (konsument powinien odrzucać duplikaty).
Kontrakty w `IdentityService.Contracts`.

| Routing key | Kiedy | Co powinien zrobić Shop API |
|---|---|---|
| `user.registered` | nowe konto | utworzyć profil klienta |
| `user.email_changed` | potwierdzona zmiana e-maila | zaktualizować dane kontaktowe |
| `user.marketing_consent_changed` | zmiana zgody marketingowej | zapisać / wypisać z newslettera |
| `user.deleted` | usunięcie konta | zanonimizować dane osobowe (faktury zostają) |

## Weryfikacja JWT w innych serwisach

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:7001";   // IdentityService - klucze z JWKS, bez współdzielonego sekretu
        options.Audience = "shop-api";
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "email";
        options.TokenValidationParameters.RoleClaimType = "role";
    });
```

Claimy: `sub` (Id użytkownika), `sid` (sesja), `email`, `email_verified`, `name`, `given_name`, `family_name`, `role`.

## Wdrożenie produkcyjne – lista kontrolna

Konfiguracja (zmienne środowiskowe lub sekrety, nigdy w repozytorium):

- [ ] `ConnectionStrings__IdentityDb`, `ConnectionStrings__messaging`
- [ ] `Jwt__Issuer` (publiczny adres serwisu), klucz RSA w `Jwt__SigningKey__Path` (`openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out signing-key.pem`)
- [ ] `Smtp__*` – prawdziwy serwer pocztowy z SPF/DKIM/DMARC dla domeny nadawcy
- [ ] `Frontend__BaseUrl`, `Cors__AllowedOrigins__0`
- [ ] `ForwardedHeaders__KnownProxies__0` – adres reverse proxy (inaczej rate limiting i dziennik zobaczą IP proxy)
- [ ] `Legal__TermsOfServiceVersion`, `Legal__PrivacyPolicyVersion` – zgodne z opublikowanymi dokumentami
- [ ] `OTEL_EXPORTER_OTLP_ENDPOINT` – kolektor logów/metryk (Grafana, Seq, Azure Monitor...)

Organizacyjnie:

- [ ] Regulamin i polityka prywatności (okresy retencji z sekcji `Retention`, odbiorcy danych, podstawy prawne)
- [ ] Kopie zapasowe bazy (w tym tabeli `DataProtectionKeys` – bez niej linki z e-maili i sesje 2FA przestaną działać)
- [ ] Rotacja klucza JWT: nowy klucz w `Path`, stary w `PreviousKeyPaths` do wygaśnięcia tokenów
- [ ] Monitoring tabeli `OutboxMessages` (wiadomości z `Attempts` = `MaxAttempts` wymagają interwencji)
