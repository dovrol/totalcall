# Architecture

## Decyzje bazowe

- Framework: Blazor WebAssembly na .NET 9.
- Styling: czysty CSS oparty o komponentowe klasy (`wwwroot/css/app.css`).
- Hosting: GitHub Pages.
- Dane zawodow: statyczne pliki JSON w `wwwroot/data`.
- Persistence MVP: `localStorage`.
- Persistence przyszlosciowe: Supabase, schowane za tym samym interfejsem.
- Formularze: renderowane z konfiguracji modulow, bez jednego sztywnego formularza typowania.

## Podzial odpowiedzialnosci

Aplikacja powinna byc podzielona na cztery lekkie warstwy:

- Domain - modele i reguly niezalezne od Blazora, JSON, localStorage i Supabase.
- Application - serwisy przypadkow uzycia, np. ladowanie zawodow, zapis typow, walidacja formularza.
- Infrastructure - implementacje techniczne, np. odczyt statycznych JSON, localStorage, w przyszlosci Supabase.
- UI - strony, komponenty Blazor i komponenty modulow typowania.

To nie musi byc osobny projekt dla kazdej warstwy w MVP. Wystarczy zachowac podzial folderami, zeby nie mieszac logiki domenowej z komponentami.

## Proponowana struktura folderow

```text
TotalCall/
  PRODUCT_SPEC.md
  ARCHITECTURE.md
  ROADMAP.md
  TotalCall.sln
  src/
    TotalCall.Client/
      TotalCall.Client.csproj
      Program.cs
      App.razor
      Routes.razor
      wwwroot/
        data/
          competitions/
            index.json
            sheffield-2026.json
            worlds-2026.json
          athletes/
            shared-athletes.json
        css/
          app.css
      Domain/
        Competitions/
          Competition.cs
          CompetitionStatus.cs
          CompetitionCategory.cs
          Athlete.cs
        Predictions/
          PredictionEntry.cs
          PredictionAnswer.cs
          PredictionSet.cs
          PredictionModuleDefinition.cs
          PredictionModuleType.cs
        Scoring/
          ScoreResult.cs
          ScoringRule.cs
      Application/
        Competitions/
          ICompetitionCatalog.cs
          CompetitionService.cs
        Predictions/
          IPredictionStore.cs
          PredictionService.cs
          PredictionValidator.cs
        Scoring/
          IScoringService.cs
      Infrastructure/
        StaticData/
          StaticJsonCompetitionCatalog.cs
        LocalStorage/
          LocalStoragePredictionStore.cs
          BrowserStorageInterop.cs
        Supabase/
          SupabasePredictionStore.cs
      Components/
        PredictionModules/
          AthleteRankingModule.razor
          CategoryPodiumModule.razor
          NumericAthletePredictionModule.razor
          SingleAthleteChoiceModule.razor
          MultiAthleteChoiceModule.razor
          YesNoModule.razor
          MultipleChoiceModule.razor
          NumericQuestionModule.razor
          PredictionModuleHost.razor
        Shared/
          AthleteSelect.razor
          CategorySelect.razor
          ValidationSummary.razor
      Pages/
        CompetitionListPage.razor
        CompetitionPage.razor
        PredictionReviewPage.razor
      Layout/
      Styles/
  tests/
    TotalCall.Tests/
```

`SupabasePredictionStore.cs` jest pokazany jako przyszly punkt rozszerzenia. Nie musi powstac w MVP.

## Przeplyw danych

1. UI prosi `CompetitionService` o liste zawodow.
2. `CompetitionService` uzywa `ICompetitionCatalog`.
3. W MVP `StaticJsonCompetitionCatalog` czyta pliki z `wwwroot/data`.
4. Strona zawodow przekazuje konfiguracje modulow do `PredictionModuleHost`.
5. `PredictionModuleHost` wybiera komponent na podstawie `PredictionModuleDefinition.Type`.
6. Komponent modulu emituje odpowiedz w neutralnym modelu `PredictionAnswer`.
7. `PredictionService` waliduje i zapisuje `PredictionSet`.
8. W MVP `IPredictionStore` jest implementowany przez `LocalStoragePredictionStore`.
9. W przyszlosci ta sama warstwa aplikacyjna moze uzyc `SupabasePredictionStore`.

## Glowne interfejsy

### `ICompetitionCatalog`

Odpowiada za odczyt konfiguracji zawodow.

Oczekiwane operacje:

- pobranie listy zawodow,
- pobranie szczegolow zawodow po identyfikatorze,
- opcjonalnie pobranie danych wspolnych, np. listy zawodnikow.

Implementacja MVP: statyczne JSON.

### `IPredictionStore`

Odpowiada za zapis i odczyt typow uzytkownika.

Oczekiwane operacje:

- pobranie typow dla zawodow,
- zapis typow dla zawodow,
- usuniecie lokalnych typow,
- sprawdzenie, czy typy istnieja.

Implementacja MVP: `LocalStoragePredictionStore`.

Implementacja pozniejsza: `SupabasePredictionStore`.

### `IPredictionValidator`

Waliduje odpowiedzi wedlug definicji modulow i konfiguracji zawodow.

Walidacja powinna byc deterministyczna i niezalezna od UI. Komponenty moga pokazywac szybkie walidacje UX, ale finalna walidacja przed zapisem powinna przejsc przez warstwe aplikacyjna.

### `IScoringService`

Liczy wynik typow na podstawie zapisanych odpowiedzi i oficjalnych rezultatow zawodow.

W MVP moze byc pominiety albo zaimplementowany tylko dla najprostszych modulow. Interfejs warto przewidziec, zeby punktacja nie trafila do komponentow UI.

## Glowne modele domenowe

### `Competition`

Reprezentuje zawody.

Pola koncepcyjne:

- `Id`
- `Slug`
- `Name`
- `Description`
- `StartDate`
- `EndDate`
- `PredictionOpenAt`
- `PredictionLockAt`
- `Status`
- `ConfigVersion`
- `Athletes`
- `Categories`
- `PredictionModules`
- `Results`

### `Athlete`

Reprezentuje zawodnika dostepnego w typowaniach.

Pola koncepcyjne:

- `Id`
- `DisplayName`
- `Country`
- `Sex`
- `WeightClassId`
- `LotNumber`
- `SeedTotal`
- `PersonalBestTotal`
- `WorldRecordReference`

Nie kazde zawody musza miec wszystkie pola. Model powinien tolerowac brak danych opcjonalnych.

### `CompetitionCategory`

Reprezentuje kategorie wagowa albo klasyfikacyjna.

Pola koncepcyjne:

- `Id`
- `Name`
- `Sex`
- `WeightLimitKg`
- `AthleteIds`

### `PredictionModuleDefinition`

Definicja pojedynczego pytania albo bloku pytan w formularzu.

Pola koncepcyjne:

- `Id`
- `Type`
- `Title`
- `Description`
- `Required`
- `Order`
- `Scope`
- `Options`
- `Validation`
- `Scoring`

`Options`, `Validation` i `Scoring` moga byc przechowywane jako struktury zalezne od typu modulu. W C# warto zaczac pragmatycznie: wspolny model definicji plus typowane klasy konfiguracji dla modulow, ktore faktycznie sa implementowane.

### `PredictionSet`

Zestaw typow uzytkownika dla jednych zawodow.

Pola koncepcyjne:

- `CompetitionId`
- `CompetitionConfigVersion`
- `UserId` opcjonalne, w MVP lokalny anonimowy identyfikator instalacji
- `SavedAt`
- `Answers`

W MVP `UserId` nie jest kontem. Moze byc losowym lokalnym identyfikatorem, jesli bedzie potrzebny do pozniejszego eksportu lub migracji.

### `PredictionAnswer`

Odpowiedz na jeden modul.

Pola koncepcyjne:

- `ModuleId`
- `ModuleType`
- `Value`
- `UpdatedAt`

`Value` musi umiec przechowac rozne ksztalty danych, np. liste zawodnikow, podium, liczbe albo wybor tak/nie. Najprostsza praktyczna opcja w MVP to trzymanie wartosci jako JSON-serializowalnego obiektu i mapowanie jej przez handler danego typu modulu.

### `ScoringRule`

Konfiguracja punktacji dla modulu.

Pola koncepcyjne:

- `Mode`
- `MaxPoints`
- `ExactPoints`
- `PartialPoints`
- `Tolerance`
- `TieBreakers`

Nie wszystkie pola beda uzywane przez kazdy modul.

### `ScoreResult`

Wynik punktacji po zawodach.

Pola koncepcyjne:

- `CompetitionId`
- `TotalPoints`
- `ModuleScores`
- `CalculatedAt`

## Rejestr modulow UI

`PredictionModuleHost` powinien pelnic role rejestru komponentow:

- dostaje `PredictionModuleDefinition`,
- sprawdza `Type`,
- renderuje odpowiedni komponent,
- przekazuje aktualna odpowiedz, liste zawodnikow, kategorie i callback zmiany.

W MVP mapowanie moze byc zwyklym `switch` w jednym miejscu. Nie ma potrzeby budowania dynamicznego systemu pluginow.

## Strategie konfiguracji JSON

Konfiguracje powinny byc stabilne i czytelne dla czlowieka. Zalecany podzial:

- `wwwroot/data/competitions/index.json` - lekka lista zawodow do ekranu startowego.
- `wwwroot/data/competitions/{slug}.json` - pelna konfiguracja zawodow.
- `wwwroot/data/athletes/*.json` - opcjonalne dane wspolne, jesli zawodnicy beda wspoldzieleni.

Na poczatku mozna trzymac zawodnikow bezposrednio w pliku zawodow. Wydzielenie do osobnych plikow ma sens dopiero, gdy realnie pojawi sie duplikacja.

## LocalStorage

Klucze powinny byc wersjonowane:

```text
totalcall:predictions:{competitionId}
totalcall:installation-id
```

Zapisany payload powinien zawierac `CompetitionConfigVersion`, aby aplikacja mogla wykryc, ze lokalne typy dotycza starszej wersji konfiguracji.

## Supabase w przyszlosci

Supabase powinien wejsc jako nowa implementacja `IPredictionStore`, bez zmiany komponentow:

- `LocalStoragePredictionStore` zostaje dla trybu offline albo goscia,
- `SupabasePredictionStore` zapisuje typy per uzytkownik,
- `PredictionService` wybiera aktywny store przez DI,
- modele domenowe pozostaja te same,
- migracja moze polegac na wyslaniu lokalnego `PredictionSet` do Supabase po zalogowaniu.

Nie nalezy dodawac zaleznosci Supabase do MVP, dopoki nie ma backendu.

## Testy

Najwieksza wartosc w MVP dadza testy bez UI:

- walidacja `athlete-ranking`,
- walidacja `category-podium`,
- serializacja i deserializacja konfiguracji zawodow,
- zapis/odczyt `PredictionSet` przez kontrakt `IPredictionStore`,
- wykrywanie niezgodnej wersji konfiguracji.

Komponenty UI mozna testowac pozniej, gdy ksztalt modulow sie ustabilizuje.

## Ryzyka techniczne

- Zbyt elastyczny JSON moze utrudnic walidacje. Dlatego warto zaczac od kilku wspieranych typow modulow, a nie od dowolnego schematu.
- Przechowywanie odpowiedzi jako ogolnego JSON upraszcza MVP, ale wymaga centralnych mapperow per typ modulu.
- GitHub Pages wymaga poprawnej konfiguracji base path dla Blazor WebAssembly.
- `localStorage` nie jest bezpiecznym storage. W MVP jest to akceptowalne, bo aplikacja nie ma nagrod pienieznych ani backendowej rywalizacji.
