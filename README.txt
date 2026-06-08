# GameWiki

Aplikacja webowa typu wiki dla graczy, umożliwiająca przeglądanie gier, pisanie recenzji i artykułów, zarządzanie kolekcjami ulubionych gier oraz moderowanie treści.

## Wersja produkcyjna

Aplikacja dostępna publicznie pod adresem:
**[https://gamewiki-app.azurewebsites.net](https://gamewiki-app.azurewebsites.net)**

Hostowana na Azure App Service, baza danych SQL Server.

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
| xUnit | 2.9.2 |
| Moq | 4.20.72 |
| Microsoft.EntityFrameworkCore.InMemory | 9.0.16 |

Zewnętrzne API: [RAWG Video Games Database API](https://rawg.io/apidocs)

## Uruchomienie lokalne

> **Uwaga:** Lokalne uruchomienie wymaga dostępu do bazy danych oraz skonfigurowania zapory sieciowej. Zalecane jest korzystanie z wersji produkcyjnej pod adresem powyżej.

### Wymagania

- .NET 9.0 SDK
- SQL Server (lokalny lub zdalny, port 1433)
- Dostęp do instancji bazy danych (wymagane dodanie adresu IP do reguł zapory)

### Konfiguracja

W pliku `appsettings.json` ustaw connection string:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=ADRES_SERWERA,1433;Database=GameWikiDb;User Id=TWOJ_LOGIN;Password=TWOJE_HASLO;TrustServerCertificate=True;"
}
```

Klucz API RAWG (uzyskasz bezpłatnie na [rawg.io](https://rawg.io/apidocs)):

```json
"Rawg": {
  "ApiKey": "TWOJ_KLUCZ_API"
}
```

### Uruchomienie

```bash
git clone <URL_REPOZYTORIUM>
cd GameWiki
dotnet ef database update
dotnet run
```

Lub otwórz solution w Visual Studio i uruchom przez IIS Express (F5).

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
├── Services/         # Logika biznesowa
├── Views/            # Widoki Razor
├── wwwroot/          # Pliki statyczne (CSS, JS, obrazy)
└── GameWiki.tests/   # Testy jednostkowe (xUnit + Moq)
```