# TotalCall Tailwind Guidelines

Projekt uzywa Tailwind CSS 4 jako podstawowego sposobu stylowania.

## Konfiguracja

- Tailwind jest skonfigurowany w modelu CSS-first.
- Wejsciowy plik to `src/TotalCall.Client/Styles/app.css`.
- Wyjsciowy plik to `src/TotalCall.Client/wwwroot/css/app.css`.
- Tokeny projektu sa w `@theme`.
- Jawne zrodla klas sa dodane przez `@source`.
- Nie przywracaj `tailwind.config.js`, dopoki nie ma technicznego powodu.

## Komendy

```bash
npm run css:build
npm run css:watch
```

## Jak pisac klasy

- Tailwind utility classes powinny byc skupione w komponentach bazowych i domenowych.
- Pages moga miec proste klasy ukladu, np. `grid gap-4`, ale nie powinny zawierac duzych blokow losowych utility classes.
- Preferuj tokeny `brand-*` zamiast przypadkowych kolorow.
- CSS isolation jest wyjatkiem, nie standardem.
- Globalne klasy w `@layer components` sa dopuszczalne dla wspolnych wzorcow formularzy i istniejacych komponentow typowan.

## Czego unikac

- Nie duplikuj dlugich zestawow klas w wielu miejscach.
- Nie stosuj przypadkowych cieni, ramek i radiusow.
- Karty trzymaj przy `rounded-lg` lub mniejszym radiusie.
- Nie uzywaj `tracking-tight`.
- Nie buduj osobnych layoutow dla Sheffield i Worlds.
- Nie dodawaj duzej biblioteki UI bez decyzji architektonicznej.

## Badge, buttony, karty, sekcje

- Badge ma miec tekst i wariant semantyczny.
- Buttony przechodza przez `AppButton`.
- Karty pojedynczych obiektow przechodza przez `AppCard`.
- Sekcje stron przechodza przez `AppSection`.
- Naglowki stron przechodza przez `AppPageHeader`.
