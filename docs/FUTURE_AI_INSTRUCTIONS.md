# Future AI Instructions

Pracuj nad TotalCall tak, jak nad produktem fantasy sports dla fanow trojboju. To nie jest aplikacja hazardowa.

## Zasady stale

- Nie dodawaj backendu ani Supabase, jesli zadanie tego jawnie nie wymaga.
- Nie zmieniaj domeny, persistence ani scoringu przy zadaniach czysto UI.
- `ICompetitionProvider` ukrywa ladowanie zawodow.
- `IPredictionStore` ukrywa zapis typow.
- Scoring ma zostac testowalnym serwisem poza komponentami Razor.
- `PredictionGroup` jest wspolnym modelem dla roznych typow pytan.

## UI

- Uzywaj czystego CSS opartego o klasy komponentowe (bez Tailwinda).
- Uzywaj istniejacych komponentow w `Components/UI`, `Components/Layout`, `Components/Competitions`, `Components/Predictions`.
- Pages maja byc cienkie.
- UI primitives nie moga znac domeny.
- Nie hardcoduj UI pod Sheffield albo Worlds, jesli ten sam wzorzec da sie obsluzyc konfiguracja.
- Unikaj wygladu betting app: kursow, kuponow, agresywnych czerwieni/zieleni i kasynowej estetyki.

## Przed oddaniem zmian

- Uruchom `./scripts/build.sh`.
- Uruchom testy, jesli zmiana dotyka logiki albo wspolnych kontraktow.
- Sprawdz mobile i desktop przynajmniej przez uruchomiona aplikacje.
