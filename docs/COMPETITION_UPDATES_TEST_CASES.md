# Competition Updates and Withdrawals Test Cases

These cases cover the competition updates drawer, roster withdrawal handling, and
the scoring/versioning behavior that sits behind the UI.

## Fixtures

Use the local dev scenarios:

```bash
./scripts/dev-scenarios.sh roster-update
./scripts/dev-scenarios.sh roster-update-locked
./scripts/dev.sh 5010
```

Open `http://localhost:5010` and use:

- `dev-roster-update` for editable predictions before the deadline.
- `dev-roster-update-locked` for locked predictions and final scoring.

Use the seeded local users to switch configurations:

| Email | State |
| --- | --- |
| `dev-alice@totalcall.local` | Affected by several withdrawn athletes in scored slots. |
| `dev-bruno@totalcall.local` | Submitted but unaffected; withdrawn athletes are outside scored slots. |
| `dev-casey@totalcall.local` | Affected only by metadata-generated roster updates. |
| `dev-dana@totalcall.local` | Fresh account with no submitted predictions. |

For automated checks, run:

```bash
./scripts/test.sh
```

## Automated Coverage

The xUnit suite should cover these behaviors:

| ID | Area | Expected coverage |
| --- | --- | --- |
| AUTO-01 | Athlete status | Missing `status` deserializes as active. |
| AUTO-02 | Athlete status | `status = "withdrawn"` and optional withdrawal metadata deserialize from JSON. |
| AUTO-03 | Timeline JSON | Missing `updates` deserializes to an empty list. |
| AUTO-04 | Timeline JSON | `updates` reads snake_case fields: `occurred_at`, `athlete_ids`. |
| AUTO-05 | Timeline JSON | Optional update fields can be omitted without breaking the model. |
| AUTO-06 | Timeline service | Withdrawn athletes generate roster update timeline entries. |
| AUTO-07 | Timeline service | A manual `roster_update` suppresses the generated item only for referenced athletes. |
| AUTO-08 | Timeline service | Manual athlete references are distinct and case-insensitive; unknown IDs are ignored. |
| AUTO-09 | Timeline service | Missing manual update IDs get deterministic generated IDs. |
| AUTO-10 | Timeline service | Duplicate update IDs keep the first update only. |
| AUTO-11 | Timeline service | Timeline sorts latest first, then roster/deadline/results/scoring/general priority. |
| AUTO-12 | Timeline service | Undated updates sort after dated updates. |
| AUTO-13 | Affected picks | Withdrawn athletes are detected in scored Top N slots. |
| AUTO-14 | Affected picks | Non-scored field rows are not treated as picks. |
| AUTO-15 | Affected picks | Single-athlete and multi-athlete question values are detected. |
| AUTO-16 | Affected picks | Group/question filters are case-insensitive. |
| AUTO-17 | Affected picks | Duplicate withdrawn selections in the same answer/position are deduped. |
| AUTO-18 | Affected picks | Blank, unknown, and active athlete IDs are ignored. |
| AUTO-19 | Scoring | A withdrawn pick that misses official results scores 0 for that slot. |
| AUTO-20 | Scoring | The rest of the group still scores normally; the whole group is not zeroed. |
| AUTO-21 | Sync hashing | Athlete status changes alter the config hash. |
| AUTO-22 | Sync hashing | Timeline updates alter the config hash. |

## Prediction Board UI

| ID | Setup | Action | Expected result |
| --- | --- | --- | --- |
| UI-01 | Open `dev-roster-update` as a user without withdrawn picks. | Load the prediction board. | Header shows an `Aktualizacje` chip in a neutral state. No full-width blocking alert is shown. |
| UI-02 | Open `dev-roster-update` as a user with a withdrawn pick. | Load the prediction board. | Header chip switches to the affected/error state and shows the affected count. |
| UI-03 | Any roster update scenario. | Click the `Aktualizacje` chip. | Right drawer opens on desktop, bottom sheet opens on mobile. Board remains visible behind it. |
| UI-04 | Drawer open with a withdrawn pick before deadline. | Inspect the pinned warning. | Copy explains that the user has withdrawn athlete(s), can update before deadline, and unchanged withdrawn slots score 0. |
| UI-05 | Drawer open without affected picks. | Inspect the drawer header and entries. | Drawer shows roster updates as neutral competition history, without the affected-user warning. |
| UI-06 | Drawer has roster and results/scoring updates. | Use filters `Wszystkie`, `Akcja`, and `Wyniki`. | `Akcja` keeps actionable roster/deadline items; `Wyniki` keeps results/scoring items; empty states are readable. |
| UI-07 | Multiple updates on different days. | Inspect the drawer list. | Items are grouped by day labels and sorted newest first inside the drawer. |
| UI-08 | Manual update references one withdrawn athlete and another withdrawn athlete has only athlete metadata. | Open the drawer. | Manual update appears once; missing manual update is generated from `athlete.status = "withdrawn"`. |
| UI-09 | Open `dev-roster-update`. | Find a withdrawn athlete in Top N. | Athlete row/card is visible with a `Withdrawn` badge. |
| UI-10 | Open an editable group where withdrawn athlete is not already selected. | Try to select the withdrawn athlete as a new pick. | Withdrawn athlete is not selectable for new picks/default fills. |
| UI-11 | Open an editable group where withdrawn athlete is already selected. | Inspect the row/card. | The selected athlete is preserved, visible, and marked with row-level warning. No automatic replacement or shifting happens. |
| UI-12 | Editable group with withdrawn pick. | Submit predictions. | Submit confirmation shows a warning, but allows the user to confirm intentionally. |
| UI-13 | Open `dev-roster-update-locked` with withdrawn pick. | Inspect board and drawer. | Warning copy says picks are locked and withdrawn athletes score 0. No edit CTA is shown. |
| UI-14 | Mobile viewport, affected user. | Open board, tap updates icon, close sheet. | Mobile header shows affected state; sheet opens without layout overlap and closes cleanly. |
| UI-15 | Standings/participants page for a scenario with updates. | Open updates from the toolbar. | Same drawer content appears from the standings view. |

## Public Results and Privacy

| ID | Setup | Action | Expected result |
| --- | --- | --- | --- |
| PUB-01 | `dev-roster-update-locked` with final results. | Open public/participants/results mode. | Submitted picks with withdrawn athletes stay visible after reveal. |
| PUB-02 | Final results exclude withdrawn athlete. | Inspect scoring breakdown/export/review. | Withdrawn pick is shown as miss/0 for that slot; other hits in the same group still score. |
| PUB-03 | Public board loaded with submitted users. | Inspect visible user data and network payloads if needed. | UI does not expose email, `user_id`, draft predictions, or raw `answers_json`. |

## Sync and Versioning

| ID | Setup | Action | Expected result |
| --- | --- | --- | --- |
| SYNC-01 | Published competition version exists. | Re-run competition sync with identical JSON. | No new `competition_versions` row is created; existing identical config is reused. |
| SYNC-02 | Published competition version exists with an active athlete. | Change only `athlete.status` to `"withdrawn"` and run sync. | A new immutable version is inserted and published. Historical versions remain unchanged. |
| SYNC-03 | Existing row already uses the same `configVersion` label. | Re-run sync after roster update without manually bumping `configVersion`. | Importer creates a derived immutable version like `<version>+roster.<hash>`. |
| SYNC-04 | Config includes manual `updates` entry. | Run sync and load frontend runtime from Supabase. | Runtime competition config includes the `updates` entry and withdrawn athlete status. |
| SYNC-05 | JSON includes withdrawal metadata only on athlete. | Run sync and open drawer. | UI generates roster update from `withdrawn_at`/`updated_at`, reason, and source when no manual update covers the athlete. |

## Scoring Rules

| ID | Setup | Action | Expected result |
| --- | --- | --- | --- |
| SCORE-01 | Submitted Top N contains one withdrawn athlete and two correct active athletes. | Score final results where withdrawn athlete is absent. | Withdrawn slot scores 0; exact/wrong-position hits for active athletes still score. |
| SCORE-02 | Pending group contains withdrawn pick but official result is not final. | View leaderboard before final result. | Pending group is not counted as zero. |
| SCORE-03 | Official final result unexpectedly includes a withdrawn athlete. | Score the result. | Normal scoring rules apply based on the official result; no special group-level zeroing is introduced. |

## Copy Checks

| ID | State | Expected copy intent |
| --- | --- | --- |
| COPY-01 | Withdrawn badge | Short label: `Withdrawn`. |
| COPY-02 | Editable affected pick | User can update before deadline. |
| COPY-03 | Locked affected pick | Pick is locked and withdrawn athletes score 0 points. |
| COPY-04 | Neutral roster update | Announces who withdrew, or summarizes count when there are multiple athletes. |
| COPY-05 | Rules/how-it-works | Explains no automatic shifting/replacement and 0 points for unchanged withdrawn slots. |
