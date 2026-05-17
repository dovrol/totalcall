# TotalCall Component Guidelines

Komponenty maja utrzymywac spojnosc UI i chronic logike domenowa przed przypadkowym rozproszeniem po Razor pages.

## Warstwy komponentow

- `Components/UI` zawiera prymitywy UI. Nie moga znac modeli domenowych.
- `Components/Layout` zawiera shell, header, language switcher i kontener strony.
- `Components/Competitions` moze znac modele zawodow.
- `Components/Predictions` moze znac modele typowan, ale nie powinno zawierac scoringu.
- `Pages` maja byc cienkie: pobieraja dane, obsluguja routing i skladaja komponenty.

## Nazewnictwo

- Prymitywy UI zaczynaja sie od `App`: `AppButton`, `AppCard`, `AppBadge`.
- Komponenty domenowe uzywaja nazwy domeny: `CompetitionCard`, `CompetitionMeta`, `PredictionSummaryCard`.
- Nie tworz komponentow nazwanych pod konkretne zawody, np. `SheffieldCard` albo `WorldsLayout`.

## Kiedy tworzyc komponent

- Ten sam wzorzec UI pojawia sie trzeci raz.
- Komponent Razor przekracza okolo 200-250 linii.
- Strona zaczyna zawierac powtarzalne bloki Tailwinda.
- Wzorzec ma jasne API przez parametry i `EventCallback`.

## Granice odpowiedzialnosci

- Scoring nie trafia do komponentow Razor.
- Komponent UI nie wywoluje bezposrednio storage ani providerow danych.
- Komponent domenowy moze renderowac model domenowy, ale decyzje biznesowe powinny zostac w serwisach aplikacyjnych.
- Persistence zostaje za `IPredictionStore`.
- Ladowanie zawodow zostaje za `ICompetitionProvider`.

## Istniejace komponenty bazowe

- `AppButton` do linkow i buttonow.
- `AppCard` do pojedynczych kart, nie do opakowywania calych sekcji strony.
- `AppBadge` do statusow i lekkich etykiet.
- `AppPageHeader` do naglowka ekranu.
- `AppSection` do sekcji strony.
- `AppStatCard` do metryk.
- `AppEmptyState`, `AppLoadingState`, `AppErrorState` do stanow systemowych.

Nowe ekrany powinny najpierw uzyc tych komponentow. Nowy komponent tworz dopiero, gdy istniejace API nie pasuje.
