# Scripts

- `./scripts/dev.sh [port]` - uruchamia aplikacje lokalnie (domyslnie port `5010`).
- `./scripts/restore.sh` - przywraca paczki NuGet.
- `./scripts/build.sh` - buduje cale rozwiazanie.
- `./scripts/test.sh` - uruchamia testy.
- `./scripts/clean.sh` - usuwa wszystkie katalogi `bin` i `obj`.
- `./scripts/import-athlete-data.sh [competition-json] [both|openipf|openpowerlifting]` - importuje dane zawodnikow; wymaga `SUPABASE_URL` i `SUPABASE_SECRET_KEY`.
