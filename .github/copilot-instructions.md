# Copilot Instructions for TotalCall

TotalCall is a Blazor WebAssembly fantasy/prediction game for powerlifting fans. It is not a betting product.

- Use plain CSS from `src/TotalCall.Client/wwwroot/css/app.css` (Tailwind is removed).
- Keep pages thin and compose them from Blazor components.
- Use `Components/UI` primitives before adding new styling patterns.
- UI primitives must not reference domain models.
- Domain components may reference domain models but must not contain scoring or persistence logic.
- Do not hardcode Sheffield/Worlds-specific layouts.
- Do not add backend, Supabase or a large UI library unless explicitly requested.
- Avoid betting/casino visual language.
- Use `ICompetitionProvider` for competition loading and `IPredictionStore` for prediction persistence.
- Keep scoring outside Razor components.
- Run `./scripts/build.sh` (and tests when needed) after code changes.
