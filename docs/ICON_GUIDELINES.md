# ICON GUIDELINES

## Source of truth
- UI icons in TotalCall come from **Lucide Icons** style (outline, stroke-based).
- Do not mix icon systems on the same screen (no Heroicons, Font Awesome, Bootstrap Icons).
- Brand/logo assets are separate and stay in `wwwroot/brand`.

## Implementation
- Use [`AppIcon`](/Users/wiktorkubis/Code/TotalCall/src/TotalCall.Client/Components/UI/Icons/AppIcon.razor) with [`AppIconName`](/Users/wiktorkubis/Code/TotalCall/src/TotalCall.Client/Components/UI/Icons/AppIconName.cs).
- Keep icon logic out of domain/services. Icons are presentational only.
- Default icon settings:
  - `Size = 20`
  - `StrokeWidth = 2`
  - `stroke="currentColor"` (inherits text color)
- Size guidance:
  - metadata/icon-inline: `16`
  - standard UI icon: `20`
  - empty states / stat emphasis: `24` or `32`

## Accessibility
- Decorative icons should stay `Decorative = true` (default, `aria-hidden=true`).
- If icon communicates meaning on its own, set `Decorative = false` and pass `AriaLabel`.
- Never communicate status using icon/color only. Always include text.

## Usage rules
- Prefer icons next to labels, not as standalone controls unless meaning is obvious.
- Keep icon usage consistent per pattern:
  - status badges: icon + text
  - metadata rows: small icon + short label
  - CTA buttons: optional trailing chevron/action icon
- If you reuse the same icon pattern 3+ times, extract/standardize it in a shared component.

## Current approved icon set
- `Trophy`
- `Calendar`
- `Clock`
- `Timer`
- `Users`
- `User`
- `Target`
- `Medal`
- `BarChart`
- `Check`
- `X`
- `Lock`
- `Unlock`
- `Share`
- `Copy`
- `Search`
- `Filter`
- `ChevronRight`
- `Settings`
- `AlertCircle`
