# TotalCall UI/UX Guidelines

TotalCall ma wygladac jak spokojny event companion dla fanow trojboju, nie jak betting app, kasyno ani panel administracyjny.

## Kierunek

- Sportowy, nowoczesny, czytelny i wiarygodny.
- Pierwszy ekran ma mowic uzytkownikowi, co moze zrobic: wybrac zawody, typowac, podejrzec zapisane predykcje.
- UI powinien wspierac powtarzalny rytm pracy: lista zawodow, szczegoly, typowanie, review.
- Nie stosuj agresywnego czarnego UI, losowych gradientow, dekoracyjnych blobow ani estetyki zakladow sportowych.

## Hierarchia

- Page title: okolo `1.9rem - 2.5rem`, `font-weight: 700`.
- Section title: okolo `1.25rem - 1.35rem`, `font-weight: 700`.
- Card title: okolo `1rem - 1.1rem`, `font-weight: 700`.
- Body: `0.9rem - 1rem` i spokojny line-height.
- Metadata/caption: `0.75rem - 0.85rem`, srednia lub pogrubiona waga.
- Letter spacing neutralny.

## Kolory

Design tokens sa zdefiniowane w `src/TotalCall.Client/wwwroot/css/app.css` jako CSS variables w `:root`.

- `brand-background` dla tla aplikacji.
- `brand-surface` dla kart i paneli.
- `brand-surface-muted` dla lekkich sekcji wewnetrznych.
- `brand-border` dla podzialow i ramek.
- `brand-ink` dla tekstu glownego.
- `brand-muted` dla tekstu pomocniczego.
- `brand-accent` dla glownej akcji.
- `brand-success`, `brand-warning`, `brand-danger` dla stanow.

## Mobile-first

- Kazdy ekran projektuj najpierw dla mobile.
- Klikalne elementy powinny miec minimum okolo 40-44 px wysokosci.
- Karty zawodow ukladaja sie w jedna kolumne na mobile i w siatke na szerszych ekranach.
- Nawigacja ma byc prosta: brand, jeden glowny link do zawodow, subtelny switch jezyka.

## Statusy i akcje

- Status nie moze byc komunikowany tylko kolorem. Badge zawsze zawiera tekst.
- Standardowe statusy: `Otwarte`, `Zablokowane`, `Zakonczone`, `Archiwum`.
- CTA w kartach zawodow musi byc jednoznaczne: `Typuj wyniki`, `Otworz zawody`, docelowo `Zobacz wyniki` dla zakonczonych zawodow.

## Dostepnosc

- Wszystkie przyciski i linki musza miec widoczny focus state.
- Zachowuj semantyczne naglowki.
- Nie wciskaj tekstu w male kontrolki. Jesli tekst jest dlugi, kontrolka ma sie rozszerzyc lub tekst ma przejsc do kolejnej linii.
