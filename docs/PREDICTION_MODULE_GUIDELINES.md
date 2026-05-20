# Prediction Module Guidelines

## Adding a new prediction module

1. Add/update `PredictionGroup.Type` in competition JSON.
2. Create module component in `Components/Predictions/Modules/`.
3. Register mapping in `PredictionModuleRenderer.razor`.
4. Keep page + shell unchanged.

## Required module contract

Each module should accept at least:

- `Competition`
- `PredictionGroup`
- Group `Answers`
- `PredictionModuleValidationResult`
- `CanEditPredictions`
- `EventCallback<PredictionAnswer> AnswerChanged`

## Rules

- Do not hardcode event slugs or event-specific layout.
- Do not persist data in the module.
- Use `EventCallback` to emit answer changes.
- Do not run scoring logic in modules.
- Do not treat missing answers as global errors.
- Do not force completion of all modules.

## Shared components first

Before creating one-off UI, reuse `Components/Predictions/Shared/`:

- `RankingBuilder`
- `RankingSlot`
- `AthleteCandidateList`
- `AthleteCandidateRow`
- `CategoryPicker`
- `CategoryStatusList`
- `ModuleValidationSummary`
- `PredictionStatusBadge`
- `PredictionEmptyState`

## Validation and completion

- Use `IPredictionValidationService` as source of truth.
- Local module state should reflect:
  - not started
  - in progress
  - complete
- Global UX should use "remaining/not started/in progress", not "X validation errors" for empty sections.
