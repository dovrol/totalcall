# Product Spec

## Cel produktu

Aplikacja webowa w Blazor WebAssembly do typowania wynikow zawodow trojbojowych. Produkt ma byc gra fantasy/prediction dla fanow trojboju, nie aplikacja hazardowa. W MVP aplikacja dziala w pelni statycznie: zawody, zawodnicy i pytania sa ladowane z plikow JSON, a typy uzytkownika sa zapisywane w `localStorage`.

Od pierwszej wersji aplikacja ma byc projektowana tak, aby pozniejsza integracja z backendem Supabase nie wymagala przebudowy UI ani logiki domenowej. Warstwa zapisu typow musi byc ukryta za interfejsem.

## Zasady produktu

- Brak realnych pieniedzy, zakladow, kursow, wyplat i mechanik hazardowych.
- Kazde zawody moga miec inny zestaw pytan i modulow.
- UI korzysta z tych samych komponentow niezaleznie od zawodow.
- Konfiguracja zawodow definiuje pytania, warianty odpowiedzi, ograniczenia i zasady punktacji.
- MVP nie wymaga logowania, backendu ani bazy danych.
- Dane uzytkownika pozostaja lokalnie w przegladarce.

## Uzytkownik docelowy

Glowny uzytkownik to fan trojboju, ktory chce przed zawodami:

- wybrac zawody,
- przejsc przez zestaw typowan,
- zapisac swoje predykcje lokalnie,
- wrocic pozniej i poprawic typy przed zamknieciem typowania,
- po zawodach porownac typy z wynikami, jesli wyniki zostana dodane do statycznych danych.

## Zakres MVP

MVP obejmuje:

- liste dostepnych zawodow,
- strone zawodow z opisem, datami i statusem typowania,
- config-driven formularz typowania zbudowany z modulow,
- zapis i odczyt typow z `localStorage`,
- walidacje odpowiedzi wynikajace z konfiguracji,
- tryb podgladu zapisanych typow,
- statyczne dane w `wwwroot/data`,
- deploy jako statyczna aplikacja na GitHub Pages.

MVP nie obejmuje:

- kont uzytkownikow,
- synchronizacji miedzy urzadzeniami,
- rankingow online,
- panelu administracyjnego,
- edytora zawodow w UI,
- bezpiecznego blokowania typow po deadline po stronie serwera,
- platnosci, nagrod pienieznych lub jakichkolwiek mechanik hazardowych.

## Model zawodow

Kazde zawody sa osobna konfiguracja. Konfiguracja powinna okreslac:

- identyfikator zawodow,
- nazwe i opis,
- typ zawodow, np. `sheffield` albo `worlds`,
- daty wydarzenia i termin zamkniecia typowania,
- liste zawodnikow,
- kategorie wagowe, jesli dotyczy,
- moduly typowania,
- opcjonalne wyniki oficjalne,
- wersje konfiguracji.

Wersja konfiguracji jest wazna, poniewaz typy zapisane lokalnie powinny byc powiazane z konkretna wersja danych. Jesli konfiguracja zawodow zostanie zmieniona, aplikacja powinna moc wykryc potencjalnie nieaktualne typy.

## Config-Driven Prediction Engine

Aplikacja nie ma jednego sztywnego formularza. Formularz typowania jest renderowany na podstawie listy modulow w konfiguracji zawodow.

Kazdy modul powinien miec:

- `id` unikalne w ramach zawodow,
- `type` okreslajacy typ modulu,
- tytul i opcjonalny opis,
- konfiguracje specyficzna dla typu modulu,
- zasady walidacji,
- opcjonalne zasady punktacji,
- flage `required`.

Ten sam typ modulu moze byc uzyty w wielu zawodach z innymi danymi i innymi zasadami.

## Typy modulow

### `athlete-ranking`

Uzytkownik uklada ranking zawodnikow, np. top 5 overall. Modul powinien obslugiwac minimalna i maksymalna liczbe pozycji oraz blokade duplikatow.

Przyklady:

- Sheffield: top overall ranking.
- Worlds: ranking best lifter albo top zawodnikow w wybranym zakresie.

### `category-podium`

Uzytkownik typuje podium w konkretnej kategorii wagowej: pierwsze, drugie i trzecie miejsce. Modul powinien pilnowac, zeby ten sam zawodnik nie pojawil sie dwa razy na podium.

Przyklady:

- Worlds: podium w wybranych kategoriach wagowych.

### `numeric-athlete-prediction`

Uzytkownik wybiera zawodnika i podaje wartosc liczbowa zwiazana z tym zawodnikiem.

Przyklady:

- Sheffield: procent rekordu swiata dla wybranego zawodnika.
- Worlds: przewidywany total albo przewidywany wynik konkretnego boju.

### `single-athlete-choice`

Uzytkownik wybiera jednego zawodnika z listy.

Przyklady:

- Sheffield: kto pobije rekord swiata.
- Worlds: best lifter.

### `multi-athlete-choice`

Uzytkownik wybiera wielu zawodnikow z listy, z limitem minimalnym i maksymalnym.

Przyklady:

- Sheffield: ktorzy zawodnicy pobija rekord swiata.
- Worlds: zawodnicy, ktorzy zdobeda medal.

### `yes-no`

Pytanie binarne.

Przyklady:

- Czy padnie rekord swiata?
- Czy zwyciezca kategorii pobije rekord zawodow?

### `multiple-choice`

Pytanie z jedna odpowiedzia z listy opcji.

Przyklady:

- Ktory kraj zdobedzie najwiecej medali?
- Ktora kategoria bedzie najbardziej konkurencyjna?

### `numeric-question`

Pytanie liczbowe bez przypisania do zawodnika.

Przyklady:

- Jaki bedzie najwyzszy total zawodow?
- Ile rekordow swiata padnie podczas zawodow?

## Sheffield: przykladowy zakres typowan

Konfiguracja Sheffield powinna pokazac elastycznosc silnika przez moduly wokol:

- top overall ranking,
- procentu rekordu swiata,
- wyboru zawodnikow, ktorzy pobija rekord swiata,
- pytan bonusowych.

Sheffield moze byc bardziej skupione na klasyfikacji overall, rekordach i predykcjach indywidualnych.

## Worlds: przykladowy zakres typowan

Konfiguracja Worlds powinna korzystac z tych samych komponentow, ale z innym przebiegiem:

- quick mode dla szybkich typowan,
- wybor kategorii wagowych,
- podium w wybranych kategoriach,
- best lifter,
- bonusowe pytania.

Worlds moze byc bardziej rozbudowane, bo zawody maja wiele kategorii wagowych i wieksza liczbe zawodnikow.

## Tryby typowania

MVP powinno wspierac jeden podstawowy przeplyw: pelne typowanie z listy modulow.

Worlds moze dodatkowo zdefiniowac `quick mode` jako wariant konfiguracji:

- osobna sekcja modulow oznaczona jako szybka,
- mniejsza liczba wymaganych pytan,
- mozliwosc pozniejszego przejscia do pelnego typowania.

W MVP quick mode moze byc tylko zestawem modulow oznaczonych w konfiguracji, bez osobnego silnika.

## Punktacja

Punktacja powinna byc konfigurowalna per modul, ale MVP nie musi implementowac wszystkich wariantow od razu.

Minimalny kierunek:

- kazdy modul moze miec `maxPoints`,
- logika punktacji jest przypisana do typu modulu,
- konfiguracja moze doprecyzowac parametry punktacji,
- wyniki sa liczone lokalnie na podstawie statycznych wynikow zawodow, jesli istnieja.

W pierwszym kroku MVP priorytetem jest poprawne zbieranie i zapisywanie typow. Automatyczna punktacja moze wejsc jako kolejny maly etap po stabilizacji formularzy.

## Stany zawodow

Aplikacja powinna rozroznic:

- `upcoming` - mozna typowac,
- `locked` - termin typowania minal, typy sa tylko do odczytu,
- `completed` - zawody zakonczone, mozna pokazac wyniki i punktacje, jesli dane istnieja,
- `archived` - starsze zawody.

W MVP blokada po terminie jest mechanizmem UX opartym na czasie klienta i danych statycznych. Nie jest to zabezpieczenie przed manipulacja.

## Kryteria akceptacji MVP

- Uzytkownik widzi liste zawodow z plikow JSON.
- Uzytkownik moze otworzyc Sheffield i zobaczyc formularz z modulow Sheffield.
- Uzytkownik moze otworzyc Worlds i zobaczyc inny formularz zbudowany z tych samych typow modulow.
- Uzytkownik moze uzupelnic typy i zapisac je lokalnie.
- Po odswiezeniu strony typy zostaja odtworzone z `localStorage`.
- Walidacje blokuja oczywiscie niepoprawne odpowiedzi, np. duplikaty na podium.
- Warstwa UI nie zalezy bezposrednio od `localStorage`.
- Zmiana implementacji zapisu na Supabase w przyszlosci nie wymaga zmiany komponentow modulow.
