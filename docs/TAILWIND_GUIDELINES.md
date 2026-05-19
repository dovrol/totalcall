# TotalCall CSS Guidelines

Projekt uzywa czystego CSS jako podstawowego sposobu stylowania. Tailwind zostal usuniety.

## Konfiguracja

- Jedyny plik styli to `src/TotalCall.Client/wwwroot/css/app.css`.
- Nie ma osobnego kroku budowania CSS.
- Nie dodawaj Tailwinda z powrotem bez decyzji architektonicznej.

## Komendy

```bash
./scripts/dev.sh
./scripts/build.sh
./scripts/test.sh
```

## Jak pisac klasy

- Uzywaj klas semantycznych (`app-*`, `competition-*`, `prediction-*`) zamiast utility classes.
- Strony skladaj z komponentow i nie duplikuj dlugich list styli.
- Trzymaj tokeny kolorow i spacingu w jednym miejscu (`:root`).
- CSS isolation jest wyjatkiem, nie standardem.

## Czego unikac

- Nie duplikuj tych samych deklaracji w wielu komponentach.
- Nie stosuj przypadkowych cieni, ramek i radiusow.
- Karty trzymaj przy `rounded-lg` lub mniejszym radiusie.
- Nie buduj osobnych layoutow dla Sheffield i Worlds.
- Nie dodawaj duzej biblioteki UI bez decyzji architektonicznej.

## Badge, buttony, karty, sekcje

- Badge ma miec tekst i wariant semantyczny.
- Buttony przechodza przez `AppButton`.
- Karty pojedynczych obiektow przechodza przez `AppCard`.
- Sekcje stron przechodza przez `AppSection`.
- Naglowki stron przechodza przez `AppPageHeader`.
