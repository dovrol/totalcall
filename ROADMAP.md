# Roadmap

## Cel roadmapy

Roadmapa prowadzi do MVP w malych krokach. Priorytetem jest dzialajacy config-driven prediction engine, a nie kompletna platforma z backendem.

## Faza 0: Decyzje i szkielety danych

1. Utworzyc projekt Blazor WebAssembly na .NET 9.
2. Dodac Tailwind CSS w minimalnej konfiguracji.
3. Przygotowac katalog `wwwroot/data`.
4. Ustalic pierwszy stabilny format JSON dla zawodow.
5. Przygotowac dwie reczne konfiguracje testowe:
   - Sheffield,
   - Worlds.

Wynik fazy: aplikacja startuje lokalnie, a repo ma miejsce na statyczne dane.

## Faza 1: Modele domenowe i odczyt zawodow

1. Dodac modele `Competition`, `Athlete`, `CompetitionCategory`, `PredictionModuleDefinition`.
2. Dodac `ICompetitionCatalog`.
3. Zaimplementowac `StaticJsonCompetitionCatalog`.
4. Dodac ekran listy zawodow.
5. Dodac ekran szczegolow zawodow bez formularza.

Wynik fazy: uzytkownik widzi liste zawodow ladowana z JSON i moze wejsc w szczegoly.

## Faza 2: Pierwszy pionowy wycinek typowania

1. Dodac modele `PredictionSet` i `PredictionAnswer`.
2. Dodac `IPredictionStore`.
3. Zaimplementowac `LocalStoragePredictionStore`.
4. Dodac `PredictionService`.
5. Zaimplementowac jeden modul od konca do konca, najlepiej `yes-no`.
6. Dodac `PredictionModuleHost` z prostym mapowaniem typu modulu na komponent.
7. Zapisywac i odtwarzac odpowiedz po odswiezeniu strony.

Wynik fazy: pierwszy modul jest config-driven i zapisuje dane lokalnie.

## Faza 3: Moduly wyboru zawodnikow

1. Dodac wspolny komponent `AthleteSelect`.
2. Zaimplementowac `single-athlete-choice`.
3. Zaimplementowac `multi-athlete-choice`.
4. Dodac walidacje wymaganej odpowiedzi.
5. Dodac walidacje limitow min/max dla wyboru wielu zawodnikow.

Wynik fazy: Sheffield moze obsluzyc pytania typu "kto pobije rekord", a Worlds pytania typu "best lifter".

## Faza 4: Moduly rankingowe i podium

1. Zaimplementowac `athlete-ranking`.
2. Dodac walidacje braku duplikatow.
3. Dodac obsluge limitu pozycji, np. top 3 albo top 5.
4. Zaimplementowac `category-podium`.
5. Dodac filtrowanie zawodnikow po kategorii.

Wynik fazy: Sheffield ma top overall ranking, a Worlds ma podium w kategoriach.

## Faza 5: Moduly liczbowe i bonusowe

1. Zaimplementowac `numeric-question`.
2. Zaimplementowac `numeric-athlete-prediction`.
3. Zaimplementowac `multiple-choice`.
4. Dodac walidacje zakresow, kroku i jednostek, np. procent, kilogramy, liczba rekordow.
5. Uporzadkowac wyswietlanie pytan bonusowych jako zwyklych modulow z osobna sekcja w konfiguracji.

Wynik fazy: aplikacja obsluguje pelny podstawowy zestaw modulow z wymagan.

## Faza 6: Dwa realne formularze

1. Dopelnic konfiguracje Sheffield:
   - top overall ranking,
   - procent rekordu swiata,
   - kto pobije rekord swiata,
   - pytania bonusowe.
2. Dopelnic konfiguracje Worlds:
   - quick mode,
   - wybor kategorii wagowych,
   - podium w kategoriach,
   - best lifter,
   - pytania bonusowe.
3. Sprawdzic, ze oba formularze korzystaja z tych samych komponentow.
4. Dodac status zapisu i date ostatniego zapisu.

Wynik fazy: widac glowna decyzje architektoniczna w praktyce.

## Faza 7: Walidacja i UX MVP

1. Dodac centralna walidacje przed zapisem.
2. Pokazywac bledy przy modulach i w podsumowaniu.
3. Dodac tryb podgladu zapisanych typow.
4. Dodac blokade edycji po `PredictionLockAt`.
5. Dodac komunikat o lokalnym charakterze zapisu.
6. Zadbac o responsywnosc na telefonie.

Wynik fazy: uzytkownik moze realnie wypelnic typy bez zgubienia danych.

## Faza 8: GitHub Pages

1. Skonfigurowac build statyczny.
2. Ustawic poprawny base path dla GitHub Pages.
3. Dodac workflow GitHub Actions.
4. Sprawdzic ladowanie JSON po deployu.
5. Sprawdzic odswiezenie strony na trasach Blazora.

Wynik fazy: MVP jest dostepne publicznie jako statyczna aplikacja.

## Faza 9: Punktacja lokalna

Ta faza moze wejsc po pierwszym MVP, jesli formularze sa juz stabilne.

1. Dodac opcjonalne wyniki zawodow do JSON.
2. Dodac `IScoringService`.
3. Zaimplementowac punktacje dla najprostszych modulow:
   - `yes-no`,
   - `multiple-choice`,
   - `single-athlete-choice`,
   - `numeric-question` z tolerancja.
4. Pozniej dodac punktacje dla:
   - `athlete-ranking`,
   - `category-podium`,
   - `multi-athlete-choice`,
   - `numeric-athlete-prediction`.

Wynik fazy: po dodaniu wynikow statycznych uzytkownik widzi lokalnie policzony rezultat.

## Faza 10: Przygotowanie pod Supabase

Nie implementowac przed stabilnym MVP.

1. Doprecyzowac model uzytkownika.
2. Dodac autoryzacje Supabase.
3. Zaimplementowac `SupabasePredictionStore`.
4. Dodac migracje lokalnych typow do konta.
5. Dodac ranking online, jesli produkt tego potrzebuje.

Wynik fazy: persistence przechodzi z lokalnego storage na backend bez przepisywania modulow UI.

## Minimalny MVP slice

Najmniejszy sensowny zakres pierwszej wersji:

1. Lista zawodow.
2. Szczegoly zawodow.
3. Modul `yes-no`.
4. Modul `single-athlete-choice`.
5. Modul `athlete-ranking`.
6. Zapis i odczyt z `localStorage`.
7. Dwie konfiguracje JSON: Sheffield i Worlds.
8. Deploy na GitHub Pages.

Ten zakres wystarczy, zeby potwierdzic architekture config-driven. Kolejne moduly mozna dodawac bez zmiany fundamentow.

## Kolejnosc implementacji modulow

Rekomendowana kolejnosc:

1. `yes-no` - najprostszy test calego przeplywu.
2. `single-athlete-choice` - pierwszy modul zalezy od listy zawodnikow.
3. `multiple-choice` - ogolny wariant opcji.
4. `athlete-ranking` - pierwsza trudniejsza walidacja.
5. `category-podium` - filtrowanie po kategoriach i unikalnosc miejsc.
6. `multi-athlete-choice` - limity i wiele wyborow.
7. `numeric-question` - zakresy, jednostki, tolerancje.
8. `numeric-athlete-prediction` - polaczenie zawodnika i liczby.

## Definition of Done dla MVP

- Aplikacja buduje sie lokalnie.
- Dane zawodow sa ladowane z `wwwroot/data`.
- Sheffield i Worlds maja rozne konfiguracje pytan.
- Formularz jest generowany z konfiguracji.
- Typy sa zapisywane przez `IPredictionStore`, nie bezposrednio przez komponenty.
- Po odswiezeniu strony typy wracaja.
- Podstawowe walidacje dzialaja.
- Aplikacja dziala po deployu na GitHub Pages.
- Dokumentacja opisuje, gdzie dodac kolejny typ modulu i kolejna implementacje persistence.
