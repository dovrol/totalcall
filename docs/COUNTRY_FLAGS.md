# Country Flags (MVP)

## Current approach

TotalCall uses `countryCode` in athlete data and renders flags as emoji in UI.

- Data stores text values only (`countryName`, `countryCode`).
- JSON must not store flag emoji.
- `countryCode` must be ISO 3166-1 alpha-2.

Examples:

- `PL`
- `US`
- `FR`
- `GB` (use `GB`, not `UK`)
- `JP`
- `CA`
- `AU`

## UI component

Flag rendering is centralized in:

- `Components/UI/CountryFlag.razor`

The component:

- accepts `Code`, `CountryName`, and optional CSS class,
- converts code to emoji using regional indicator symbols,
- falls back to `🏳️` for missing/invalid code,
- always keeps textual country name nearby in UI.

## Accessibility and product rules

- Do not rely on flag alone.
- Always show country text next to flag.

## Future upgrade path

Emoji is MVP only.  
Later we can replace `CountryFlag` internals with SVG rendering without changing module components or JSON structure.
