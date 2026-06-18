# Scoring

TotalCall's current official scoring rule version is `placement-v2`.

The database migration that introduced result tables is named `20260611130000_add_scoring_v1_results.sql`, but the active scoring rule version written to `score_snapshots.rules_version` is `placement-v2` from `ScoreSnapshotBuilder.RulesVersion`.

## Ownership

Official scoring is computed by `tools/sync/TotalCall.Sync`, not by the browser.

The flow is:

1. A service-role import runs `TotalCall.Sync results`.
2. The importer validates an official results JSON file against the published competition config.
3. It upserts `official_results` and `official_result_groups`.
4. It loads submitted `prediction_submissions`.
5. It computes scores with the shared C# scoring service.
6. It upserts private `score_snapshots`.
7. The app reads safe projections through RPCs.

The frontend renders score snapshots and breakdowns. It must not become the source of truth for official scoring.

## Scoreable Questions

The currently registered scorers are:

- `AthleteRankingQuestionScorer`
- `CategoryPodiumQuestionScorer`

Both inherit `PlacementQuestionScorer` and share the same `placement-v2` rules.

The scoring service only counts required groups and required questions with a registered scorer. Unsupported question types are skipped.

## Placement-v2 Rules

For each scoreable Top-N placement group:

- Exact athlete in exact position: `+3`.
- Correct athlete in the wrong Top-N position: `+1`.
- Miss: `+0`.
- Withdrawn athlete pick: `+0`.
- Set bonus: `+1` if every official Top-N athlete is present among the non-withdrawn picks.
- Perfect order bonus: `+2` if every scored slot is exact.

For a Top 3 group:

- Placement max: `3 slots * 3 = 9`.
- Set bonus max: `1`.
- Perfect order bonus max: `2`.
- Total max: `12`.

## Incomplete Predictions

A scoreable group with fewer predictions than the required count scores `0` for that group.

The group still counts as scored when its official result group is final, because the official result exists and the user's prediction was incomplete.

The breakdown explanation is `Incomplete prediction.`

## Pending Groups

Pending result groups are not counted as zero.

They are excluded until the group becomes final:

- They do not add question score rows.
- They do not add points.
- They keep the leaderboard status partial if at least one required group remains pending.
- They are reflected in `total_groups_count`, not `scored_groups_count`.

## Withdrawals

Withdrawn athletes are modeled in the competition config as athlete status metadata.

Scoring behavior:

- A withdrawn athlete left in a submitted pick receives a `withdrawn` slot verdict.
- The slot receives `0`.
- Other non-withdrawn hits in the same group still score normally.
- Withdrawn picks do not count toward the set bonus.
- Picks are not shifted or replaced automatically.

## Partial Vs Final Scores

`PredictionScoringService` calculates:

- `total_points`
- `question_scores`
- `scored_groups_count`
- `total_groups_count`
- `status`

Status is:

- `final` when all required scoreable groups are final.
- `partial` otherwise.

The standings page treats the leaderboard as final only when all returned rows have final status.

## Score Snapshots

`score_snapshots` is a private service-role table.

Important columns:

- `competition_id`
- `competition_version_id`
- `prediction_submission_id`
- `user_id`
- `total_points`
- `scored_groups_count`
- `total_groups_count`
- `status`
- `results_hash`
- `rules_version`
- `breakdown_json`
- `calculated_at`

Public callers cannot select this table directly. They must use RPC projections.

## Breakdown JSON

`ScoreSnapshotBuilder` writes `breakdown_json` with:

- `questionScores[]`
- `groupId`
- `questionId`
- `categoryId`
- `points`
- `maxPoints`
- `placement`
- `placementMax`
- `setBonus`
- `orderBonus`
- `explanation`
- `slots[]`
- `official[]`

Slot entries include:

- `position`
- `athleteId`
- `verdict`
- `points`

Official entries include:

- `position`
- `athleteId`
- optional `squatKg`
- optional `benchKg`
- optional `deadliftKg`
- optional `totalKg`

## Public Leaderboard

`get_competition_leaderboard` returns a safe public projection:

- rank position,
- opaque `board_ref`,
- display name,
- total points,
- scored group count,
- total group count,
- status,
- last calculated time.

It does not return email, user_id, answers, drafts, or raw `answers_json`.

Only rows with `scored_groups_count > 0` appear in the leaderboard.

## Own Score

`get_my_score` returns the signed-in user's own score snapshot for one competition:

- rank,
- points,
- progress,
- status,
- results hash,
- rules version,
- breakdown JSON,
- calculation time.

It is granted to `authenticated` only and filters by `auth.uid()`.

## Public Board Scoring Breakdown

`get_public_board` returns:

- display name,
- rank,
- points,
- progress,
- status,
- sanitized picks array,
- breakdown JSON,
- calculation time.

The picks payload is only `answers_json -> 'answers'`. It is not the full `PredictionSet`.

Public board rows require:

- an existing score snapshot,
- `scored_groups_count > 0`,
- a submitted prediction row,
- `submitted_at is not null`,
- competition lock time has passed.

## Changing Scoring Rules

When scoring rules change:

1. Update scoring implementation and tests.
2. Change `ScoreSnapshotBuilder.RulesVersion`.
3. Re-run the official results importer for affected competitions.
4. Confirm `score_snapshots.rules_version` changed.
5. Verify public leaderboard, own score, and public board breakdowns.
6. Update this document and release notes.

Do not assume old snapshots reflect new rules. Existing snapshots remain whatever was written at import time.
