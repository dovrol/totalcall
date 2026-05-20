# UX Rules for Prediction Workspace

## Product language

Use: prediction, pick, call, entry, forecast, fantasy.  
Do not use betting vocabulary.

## Mandatory UX behavior

- Users can save partial predictions.
- Incomplete predictions are allowed.
- Incomplete predictions do not score.
- Missing answers are not global errors.

## Status model

- `Not started`
- `In progress`
- `Complete`

Display progress as completion counts and remaining items, for example:

- `2 / 16 categories completed`
- `1 in progress`
- `13 not started`

## Shell vs module UX

- Shell is generic and stable.
- Module owns its internal workspace layout.
- Shell must not assume categories/ranking/athletes always exist.

## Top-N by category UX

- Desktop: vertical category list (no primary horizontal overflow strip).
- Mobile: category picker/dropdown as primary navigation.
- Ranking slots: clear status (`0/5`, `3/5`, `5/5`).
- Athlete list: search + sort + country + nominated total.
- Drag and drop must have click/tap fallback (`Pick`).

## Navigation copy

Prefer:

- `Save & next`
- `Next category`
- `Skip for now`

Avoid implying full completion is mandatory.

## Accessibility

- Do not rely on color alone for status.
- Keep focus states visible.
- Ensure touch targets are comfortable on mobile.
- Keep drag-and-drop fallback available.
