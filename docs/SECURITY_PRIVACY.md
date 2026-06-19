# Security And Privacy

TotalCall stores private prediction data and exposes only curated public projections. This document is the current privacy source of truth for v1.0.0 documentation.

## Privacy Model

Private by default:

- email address,
- Supabase Auth user id,
- auth access and refresh tokens,
- local drafts,
- cloud drafts,
- raw `answers_json`,
- full `PredictionSet` metadata,
- import logs and admin tables,
- admin operation history,
- private score snapshot table rows.

Public or public-safe:

- public competition metadata and published config,
- public athlete history and analytics,
- public event-level competition updates,
- display name,
- submitted participant list,
- public leaderboard safe fields,
- sanitized public board picks after lock and scoring.

## Email Is Private

Email is used by Supabase Auth for Magic Link sign-in.

The app shows the email address only to the signed-in user on `/profile`. Public participants, standings, and public boards use display name only.

Do not add email to:

- participant RPCs,
- leaderboard RPCs,
- public board RPCs,
- exports generated for public sharing,
- analytics events.

## User ID Is Private

`user_id` is required internally for RLS, ownership, imports, scoring snapshots, and joins.

It must not be returned by public RPCs. Public board links use an opaque score snapshot id as `board_ref`, not a user id.

## Drafts Are Private

Drafts exist in two places:

- browser localStorage,
- private `prediction_submissions` cloud row for signed-in users.

Drafts are not public. Public participants and leaderboards use submitted/scored state only.

## Raw Answers JSON Is Private

`prediction_submissions.answers_json` stores the full `PredictionSet`. It can contain fields that are not appropriate for public output, including metadata and future fields.

Never expose `answers_json` directly.

The public board RPC intentionally returns only:

```sql
coalesce(sub.answers_json -> 'answers', sub.answers_json -> 'Answers') as picks_json
```

That keeps public boards limited to the picks array and avoids leaking the full snapshot.

## Public Board Reveal Rules

Public boards are visible only when all these conditions are true:

- a `score_snapshots` row exists,
- the row has `scored_groups_count > 0`,
- the linked submission row is `submitted`,
- `submitted_at` is not null,
- the competition lock time exists and has passed.

Before that, `get_public_board` returns no row and the frontend shows a not-found/error state.

## Public Leaderboard Safe Fields

`get_competition_leaderboard` exposes:

- position,
- board ref,
- display name,
- total points,
- scored group count,
- total group count,
- status,
- last calculated time.

It does not expose email, user id, answers, drafts, raw `answers_json`, or tokens.

## Public Participants Safe Fields

`get_competition_participants` exposes:

- competition id,
- display name,
- submitted time,
- status.

It does not expose answers, email, or account identifiers.

## Own Score

`get_my_score` is authenticated-only and scoped to `auth.uid()`.

It may return the user's own breakdown JSON. It must not return other users' answers or identifiers.

## Supabase Key Boundaries

Frontend:

- may use `Supabase:Url`,
- may use `Supabase:PublishableKey`,
- must never include service-role keys.

Authenticated user requests:

- use the publishable key plus the user's access token.

Importer, scripts, GitHub Actions:

- may use `SUPABASE_SECRET_KEY` or service-role credentials.
- must keep those values in environment variables or secrets.

## RLS And Grants

Security relies on both RLS and grants:

- Private tables have RLS enabled.
- Direct privileges are revoked from `public`, `anon`, and often `authenticated`.
- Owner-only policies apply to `profiles` and `prediction_submissions`.
- Service-role tables have no public direct access.
- Security definer RPCs expose narrow, curated projections.

When adding a new public endpoint, document:

- why it is safe,
- which fields it returns,
- which private fields it omits,
- which tests or migration assertions cover the boundary.

## Analytics

The deploy workflow can inject `ANALYTICS_SNIPPET` into `index.html` after publish.

Current repo behavior:

- local development has only the placeholder,
- GitHub Actions replaces the placeholder if the secret exists,
- current docs and app copy refer to Cloudflare Web Analytics as cookieless visit statistics.

Do not send custom analytics events containing:

- email,
- auth tokens,
- prediction content,
- selected athletes,
- public board refs tied to a user in a way that bypasses the UI,
- localStorage contents.

## Known Privacy-Sensitive Areas

Review carefully when changing:

- `get_public_board`,
- `get_competition_leaderboard`,
- `get_competition_participants`,
- `get_my_score`,
- `prediction_submissions` policies,
- `score_snapshots` grants,
- public board UI,
- share/export flows,
- profile display name validation,
- analytics injection,
- localStorage session handling.

## In-App Privacy Copy

The in-app privacy modal and related notes should continue to match the current public leaderboard and sanitized public board behavior.

Current copy states:

- drafts and raw `answers_json` are private,
- submitted picks may be shown as sanitized public boards after lock and scoring,
- email and user id stay private,
- leaderboard fields are limited to display name, rank, points, progress, status, and calculation time.
