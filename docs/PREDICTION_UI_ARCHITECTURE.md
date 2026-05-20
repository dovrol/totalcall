# Prediction UI Architecture

## Goal

Prediction UI is module-driven, not event-page-driven.  
Do not build `WorldsTop5PredictionPage` or any event-specific prediction page.

## Main building blocks

- `Pages/CompetitionPredictionPage.razor`
  - Loads `Competition`.
  - Loads/creates `PredictionSet`.
  - Tracks active `PredictionGroup`.
  - Saves through `PredictionService` (`IPredictionStore` underneath).
  - Does not render module-specific internals.
- `Components/Predictions/PredictionShell.razor`
  - Stable page shell: header, deadline, save status, progress, module nav, active module, summary, navigation CTA.
- `Components/Predictions/PredictionModuleRenderer.razor`
  - Resolves component by `PredictionGroup.Type` (with fallback inference).
  - Renders the correct module component.
- `Components/Predictions/Modules/*`
  - Module-specific UX and local validation hints.
  - Emit `PredictionAnswer` via `EventCallback`.
  - Must not save to localStorage directly.

## Data and responsibility boundaries

- Shell and page handle save lifecycle.
- Modules handle only module-specific interaction.
- Scoring stays outside UI components.
- Validation/completion state comes from `IPredictionValidationService`.

## Completion policy

- Allowed: partial entries.
- Missing answers are not global validation errors.
- Status model:
  - `NotStarted`
  - `InProgress`
  - `Complete`
- Only completed predictions score points.

## Current module state

- Implemented now: `top-n-by-category` (`TopNByCategoryPrediction`).
- Other module types are wired in renderer with explicit placeholders.
