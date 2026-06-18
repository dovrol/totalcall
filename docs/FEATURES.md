# Features

This document describes the functionality currently present in the repo. It does not describe planned features unless they are marked as a known limitation or release checklist item.

## Competition List

Route: `/`

The home page renders a scoreboard-style competition list loaded through `CompetitionService` and `ICompetitionProvider`.

Implemented behavior:

- Loads published competitions from Supabase, falling back to bundled JSON.
- Shows open, locked, completed, and archived competitions.
- Applies current status based on configured status, lock time, and end time.
- Supports search by competition name, city, or country code.
- Supports status filters and tier filters when tier metadata exists.
- Shows an "up next" row for the nearest upcoming or locked competition.

## Competition Details And Prediction Board

Route: `/competitions/{slug}/predictions`

The prediction page loads one competition and the user's current `PredictionSet`.

Implemented behavior:

- Config-driven prediction groups and questions.
- Specialized Top-N by category workspace for the current Worlds-style board.
- Generic module shell and renderer for other module types.
- Local validation and completion status per module.
- Partial drafts are allowed.
- Share/export modal for local exports and copied summaries.
- Read-only locked board after the prediction window closes.
- Backend score snapshot loaded after lock when available through `get_my_score`.

## Top-N Board

The current main prediction experience is a Top-N by category board.

Implemented behavior:

- Desktop sheet and mobile card/list layout.
- Category switcher, module menu, command palette, and mobile switcher.
- Athlete context drawer, history/analytics lookups, and comparison tooling.
- Manual ordering and predicted total/lift fields.
- Bulk fill/sort helpers.
- Auto-seeded nomination picks and manually edited picks are visually distinguished.
- Withdrawn athletes remain visible in existing picks but are skipped for new picks.
- Results mode renders official placement, actual totals, slot verdicts, points, and breakdown when a score snapshot exists.

## Auth / Magic Link

Routes:

- `/auth/login`
- `/auth/callback`

Implemented behavior:

- Supabase Magic Link sign-in using PKCE.
- Email-based account creation is enabled by Supabase Auth.
- Session, refresh token, PKCE verifier, and resend cooldown are stored in localStorage.
- Session refresh happens on app startup and on demand.
- Sign-out revokes the session best-effort and clears local session data.
- Submit and profile management require authentication.

## Local Draft

Anonymous users can create and edit local drafts.

Implemented behavior:

- Drafts are stored under `totalcall:predictions:{competitionId}` in localStorage.
- Drafts include the `PredictionSet`, app version, schema version, saved time, config version, submission status, and answers.
- Local drafts survive reloads.
- Missing answers are allowed.
- Anonymous drafts cannot be submitted until the user signs in.

## Cloud Save

Signed-in users synchronize private drafts to Supabase.

Implemented behavior:

- `SynchronizedPredictionStore` wraps local storage and `SupabasePredictionStore`.
- Cloud rows live in private `prediction_submissions`.
- One row exists per user and competition.
- Authenticated users can read, insert, and update only their own rows.
- Local and cloud snapshots are reconciled by owner and answer update times.
- Anonymous local drafts may be adopted after sign-in only when safe for the current account.
- Drafts owned by another account are not uploaded by the current account.

## Submit

Signed-in users can submit predictions before lock.

Implemented behavior:

- Submit calls the `submit_prediction` RPC.
- The backend stamps or preserves `submitted_at`.
- Submit creates or updates the user's `prediction_submissions` row as `submitted`.
- Backend trigger blocks writes outside the prediction window.
- UI confirms submit and warns when selected withdrawn athletes remain in the board.
- Submitted entries appear in the public participant list.

## Participants / Standings

Routes:

- `/competitions/{slug}/participants`
- `/competitions/{slug}/standings`

Implemented behavior:

- Before scoring snapshots exist, the page shows submitted participants from `get_competition_participants`.
- After scoring snapshots exist, the page switches to the leaderboard from `get_competition_leaderboard`.
- Search filters participants or leaderboard rows by display name.
- Standings show display name, rank, points, scored/total progress, status, and calculation time.
- Leaderboard rows include an opaque `board_ref` when public boards are available.
- Raw answers, email, and user IDs are not returned by the standings RPC.

## Deadline And Lock

Implemented behavior:

- `CompetitionService` resolves status from configured status, `predictionLockAt`, and `endDate`.
- `PredictionDeadline` drives UI phases: open, soon, urgent, locked, ended.
- UI disables edits after lock or end.
- Supabase enforces `prediction_open_at`, `prediction_lock_at`, and locked/completed/archived status for authenticated writes.
- Service-role importer/admin writes bypass end-user lock checks by design.

## Results Mode

Implemented behavior:

- Official result files are imported by `TotalCall.Sync results`.
- Final result groups are scored; pending groups are not counted as zero.
- Own locked board can load `get_my_score` and render the user's score breakdown.
- Public boards can load sanitized submitted picks plus score breakdown after lock and scoring.
- Standings can show partial or final leaderboard state.

## Partial / Live Scoring

Implemented behavior:

- Official result import status can be `partial` or `final`.
- Individual result groups can be `pending` or `final`.
- Score snapshots are `partial` when not all required groups are final.
- Leaderboard progress shows scored groups out of total groups.
- The public leaderboard includes users only when `scored_groups_count > 0`.

## Final Scoring

Implemented behavior:

- A snapshot is final when all required scoreable groups for that user's submitted competition version are final.
- Standings render the final stage when all leaderboard rows have final status.
- Final scoring still uses the submitted competition version associated with each submission.

## Public User Boards

Route: `/competitions/{slug}/board/{boardRef}`

Implemented behavior:

- Board links come from `get_competition_leaderboard`.
- `board_ref` is the opaque score snapshot id.
- `get_public_board` returns display name, rank, points, progress, status, sanitized picks array, breakdown, and last calculation time.
- Public boards are read-only.
- Public boards are available only after the competition lock has passed and a scored snapshot exists.
- The RPC returns only `answers_json -> 'answers'`, never the full `PredictionSet`.

## Withdrawals / Roster Updates

Implemented behavior:

- Athletes can have `status = "withdrawn"` plus optional withdrawal metadata in competition JSON.
- New picks skip withdrawn athletes.
- Existing picks remain visible so users understand what changed.
- Open boards warn users when their picks include withdrawn athletes.
- Submit confirmation warns about withdrawn picks.
- Locked boards explain that withdrawn athlete slots score 0.
- Scoring marks withdrawn picks as `withdrawn`, awards 0 for that slot, and does not auto-shift or replace picks.
- Competition timeline generates roster updates for withdrawn athletes when explicit updates are missing.

## Competition Updates Timeline

Implemented behavior:

- Competition config supports `updates`.
- Timeline can show roster, deadline, results, scoring, and general updates.
- Generated roster updates are added for withdrawn athletes not covered by manual updates.
- The timeline can highlight updates affecting the current user's picks.
- Event-level updates are public; "affects your picks" warnings are private local UI derived from the user's board.

## Profile / Display Name

Route: `/profile`

Implemented behavior:

- Requires authentication.
- Shows current display name and email to the signed-in user.
- Email is explicitly marked private.
- Allows changing display name.
- Display names are normalized and validated client-side and database-side.
- Public standings and boards use display name or a generated fallback.

## Privacy / How It Works Copy

Implemented behavior:

- Footer opens an in-app "Privacy & data" modal.
- The app includes a roster-withdrawal rules note in the prediction summary panel.
- In-app privacy copy must match the current public-board and leaderboard model before v1.0.0. See [Security and Privacy](SECURITY_PRIVACY.md) and [Release](RELEASE.md) for the checklist.

## Dev Scenarios

Implemented behavior:

- `TotalCall.Sync scenario` and `./scripts/dev-scenarios.sh` seed local product states.
- Scenarios include open, open with submissions, locked without results, partial results, final results, empty, roster update, and locked roster update.
- `all-states` seeds all scenarios.
- The scenario runner requires `--local` and a loopback Supabase URL.
