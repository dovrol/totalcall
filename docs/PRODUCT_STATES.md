# Product States

This document describes the main user-visible states and privacy rules. "Board" means the user's prediction board for one competition.

## Common Rules

- Editable means the user can change answers in the browser and save them if the backend accepts the write.
- Submit requires authentication and an open prediction window.
- Anonymous users can keep local drafts but cannot submit.
- Signed-in users keep local drafts plus private Cloud Save.
- Public standings never expose email, user ID, raw `answers_json`, auth tokens, or drafts.
- Public boards expose sanitized submitted picks only after lock and scoring.
- Missing answers are allowed.

## Open Competition

User sees:

- Competition on the home page under open competitions.
- Prediction board with active controls.
- Deadline chip when `predictionLockAt` exists.
- Local or cloud save status in the action bar.
- Participant/standings link.

Editable: yes, while `PredictionDeadline.CanEdit` resolves to open/soon/urgent.

Can submit: yes for authenticated users with synchronized cloud state. Anonymous users see a login-to-submit action.

Results visible: no official results on the board unless the backend already has snapshots and the UI is no longer editable. In normal open state, results are not shown.

Public boards visible: no. `get_public_board` requires `prediction_lock_at <= now()` and a scored snapshot.

Private/public data:

- Draft answers stay private.
- Submitted participant display names may be visible if users submit.
- Email and user IDs stay private.

## Open With Draft

User sees:

- Draft progress and completion status.
- Local draft status when signed out.
- Cloud save/private draft status when signed in and synchronized.

Editable: yes.

Can submit: only after sign-in and successful cloud synchronization.

Results visible: no.

Public boards visible: no.

Private/public data:

- Draft answers are private local data, and private cloud data after sign-in.
- The draft does not appear in public participants or standings until submitted.

## Open With Submitted Prediction

User sees:

- Submitted status and submitted timestamp.
- The board remains editable before lock in current UI because save/submit writes can update the same submitted row through backend endpoints while the window is open.
- Participant/standings page includes the user's public display name as a submitted participant.

Editable: yes until lock.

Can submit: yes, effectively re-submit/update submitted answers while the backend window accepts writes.

Results visible: no in the normal open state.

Public boards visible: no before lock/scoring.

Private/public data:

- Public participants expose display name, submitted time, competition id, and submitted status.
- Full answers remain private until a later public board reveal condition is met.

## Locked, No Results

User sees:

- Read-only prediction board.
- Locked action-bar state.
- Standings page in submissions mode if submitted participants exist.
- Empty standings state if there are no submissions.

Editable: no.

Can submit: no.

Results visible: no, because no score snapshot exists.

Public boards visible: no, because public board links come from scored leaderboard rows.

Private/public data:

- Submitted display names and submission times can be public.
- Raw answers remain private.

## Partial Results

User sees:

- Standings in live/partial mode.
- Progress such as scored groups out of total groups.
- Own locked board may show score breakdown from `get_my_score`.
- Public leaderboard rows may link to public boards if `board_ref` is returned.

Editable: no.

Can submit: no.

Results visible: yes for finalized groups only.

Public boards visible: yes for scored rows after lock. Boards show sanitized submitted picks plus score breakdown.

Private/public data:

- Public leaderboard exposes display name, rank, points, scored/total progress, status, calculation time, and opaque board ref.
- Public board exposes display name, rank, score, sanitized picks array, and breakdown.
- Email, user_id, draft metadata, local owner id, app/schema metadata, and raw `answers_json` remain private.

## Final Results

User sees:

- Standings in final mode when all leaderboard rows have final snapshot status.
- Full final score and breakdown for scored groups.
- Public boards for ranked users.

Editable: no.

Can submit: no.

Results visible: yes.

Public boards visible: yes for scored rows.

Private/public data:

- Same public/private boundary as partial results.
- `score_snapshots` remains private; only RPC projections are public.

## No Submissions

User sees:

- Empty standings state.
- Call to return to predictions.

Editable: depends on competition status. Open competitions remain editable; locked/completed competitions do not.

Can submit: only if the competition is open and the user is authenticated.

Results visible: no public leaderboard rows because no submitted rows can be scored.

Public boards visible: no.

Private/public data:

- No participant rows are public.
- Local drafts remain private.

## No Results Imported

User sees:

- Submitted participant list if there are submissions.
- No points, rank, or score breakdown.
- Progress remains `0/{requiredGroups}` in standings.

Editable: no if the competition is locked; yes if still open.

Can submit: only when still open and authenticated.

Results visible: no official scoring.

Public boards visible: no.

Private/public data:

- Submitted display names and submitted times can be public.
- Answers remain private.

## Withdrawn Athlete Before Deadline

User sees:

- Roster update in competition updates.
- Existing selected withdrawn athlete remains visible and marked.
- New picks skip withdrawn athletes.
- Warning in the prediction UI and submit confirmation if their picks are affected.

Editable: yes if the competition is still open.

Can submit: yes for authenticated users. The UI warns that unchanged withdrawn slots score 0.

Results visible: no unless results mode is otherwise active.

Public boards visible: no before lock/scoring.

Private/public data:

- Roster updates are public event-level data.
- "Your picks are affected" warnings are private to the current local board.

## Withdrawn Athlete After Lock

User sees:

- Read-only board with withdrawn athlete still visible.
- Locked warning that withdrawn slots score 0.
- Results mode later marks the slot as withdrawn if scoring snapshots include that verdict.

Editable: no.

Can submit: no.

Results visible: only if imported/scored.

Public boards visible: yes only after scored leaderboard rows exist.

Private/public data:

- Public board can show the submitted pick containing the withdrawn athlete after reveal conditions are met.
- Raw submission JSON and user identifiers remain private.

## Public Board Hidden Before Reveal/Lock

User sees:

- Public board route returns the not-found/error state if the RPC returns no row.

Editable: no.

Can submit: not applicable.

Results visible: no.

Public boards visible: no.

Private/public data:

- `get_public_board` requires a scored snapshot, a submitted row, and `prediction_lock_at <= now()`.
- No picks are returned before these conditions are true.

## Public Board Visible

User sees:

- Read-only Top-N workspace for another user's board.
- Public board name, competition context, rank, points, progress, and breakdown.
- Back link to standings.

Editable: no.

Can submit: no.

Results visible: yes for scored groups returned in the snapshot breakdown.

Public boards visible: yes through standings board links.

Private/public data:

- Public: display name, sanitized picks array, score, breakdown, status, calculation time.
- Private: email, user_id, full `PredictionSet`, draft metadata, local owner id, raw `answers_json`.

## Pending Category

User sees:

- Category or group has no official final result yet.
- In standings, pending groups are reflected in the progress denominator but not scored.
- On boards, categories without breakdown are displayed as not yet scored.

Editable: no if locked; yes if still open.

Can submit: only if open and authenticated.

Results visible: no points for the pending group.

Public boards visible: only if at least one other group has produced a scored snapshot row.

Private/public data:

- Pending groups are not counted as zero.
- They are simply absent from score totals until final.

## Final Category

User sees:

- Official placement and lift/total results for that category when included in breakdown.
- Slot verdicts and category points.

Editable: no if in results mode.

Can submit: no if locked.

Results visible: yes.

Public boards visible: yes if board reveal conditions are met.

Private/public data:

- Official sports result data is public.
- Per-user picks are public only through sanitized public board RPCs after reveal.

## Authenticated Vs Anonymous

Anonymous users:

- Can edit and save local drafts.
- Cannot submit.
- Cannot access `/profile`.
- Cannot call authenticated `get_my_score`.
- Can view public competition config, athlete data, participants, standings, and public boards.

Authenticated users:

- Can sync private cloud drafts.
- Can submit while open.
- Can manage display name.
- Can view their own private score snapshot through `get_my_score`.
- Still cannot read other users' raw submissions.

## Local Draft Vs Cloud Saved Draft

Local draft:

- Stored in browser localStorage.
- Exists for anonymous and authenticated users.
- Cleared only by local browser data operations or app delete paths.
- Not visible to Supabase or other devices until synchronized.

Cloud saved draft:

- Stored in private `prediction_submissions`.
- Requires authentication.
- Scoped by RLS to the owning user.
- Used as the source for submit and future score snapshots only after submitted.
- Not exposed publicly while in draft status.
