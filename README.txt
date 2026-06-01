# GameWiki

Aplikacja webowa typu wiki dla graczy, umożliwiająca przeglądanie gier, pisanie recenzji i artykułów, zarządzanie kolekcjami ulubionych gier oraz moderowanie treści.

## Technologie i biblioteki

| Biblioteka | Wersja |
|---|---|
| .NET / ASP.NET Core MVC | 9.0 |
| Microsoft.EntityFrameworkCore | 9.0.14 |
| Microsoft.EntityFrameworkCore.SqlServer | 9.0.14 |
| Microsoft.EntityFrameworkCore.Design | 9.0.14 |
| Microsoft.EntityFrameworkCore.Tools | 9.0.14 |
| BCrypt.Net-Next | 4.1.0 |
| QuestPDF | 2026.5.0 |
| Microsoft.AspNetCore.Http | 2.3.9 |
| Microsoft.AspNetCore.Session | 2.3.9 |
| Microsoft.VisualStudio.Web.CodeGeneration.Design | 9.0.12 |

Zewnętrzne API: [RAWG Video Games Database API](https://rawg.io/apidocs)

## Wymagania

- .NET 9.0 SDK
- SQL Server (lokalny lub zdalny, port 1433)

## Konfiguracja

### 1. Baza danych

W pliku `appsettings.json` ustaw connection string do swojej instancji SQL Server:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=GameWikiDb;User Id=TWOJ_LOGIN;Password=TWOJE_HASLO;TrustServerCertificate=True;"
}
```

### 2. Klucz API RAWG

W pliku `appsettings.json` (lub `appsettings.Development.json`) ustaw swój klucz API RAWG:

```json
"Rawg": {
  "ApiKey": "TWOJ_KLUCZ_API"
}
```

Klucz API można uzyskać bezpłatnie na [rawg.io](https://rawg.io/apidocs).

## Instalacja i uruchomienie

```bash
# 1. Sklonuj repozytorium
git clone <URL_REPOZYTORIUM>
cd GameWiki

# 2. Przejdź do katalogu projektu
cd GameWiki

# 3. Zastosuj migracje bazy danych
dotnet ef database update

# 4. Uruchom aplikację
dotnet run
```

Aplikacja domyślnie dostępna pod adresem `https://localhost:5001`.

## Role użytkowników

- **User** — rejestracja, pisanie recenzji i artykułów, zarządzanie kolekcjami, zgłaszanie treści
- **Moderator** — wszystkie uprawnienia użytkownika + panel administracyjny, weryfikacja artykułów i recenzji, banowanie użytkowników, generowanie raportów PDF
- **Admin** — pełne uprawnienia

## Struktura projektu

```
GameWiki/
├── Controllers/      # Kontrolery MVC
├── DTOs/             # Obiekty transferu danych
├── Data/             # DbContext (EF Core)
├── Models/           # Modele / encje
├── Services/         # Logika biznesowa (GameService, RawgService)
├── Views/            # Widoki Razor
└── wwwroot/          # Pliki statyczne (CSS, JS, obrazy)
```
