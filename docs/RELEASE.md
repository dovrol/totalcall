# Release

This document describes the release process and the v1.0.0 documentation checklist. Do not create a tag, GitHub release, deploy, or run production migrations without explicit owner confirmation.

## Current Release Status

Status: preparing documentation and release checklist for `v1.0.0`.

No `v1.0.0` tag or GitHub release has been created by this documentation pass.

Known versioning inconsistency to resolve before release:

- `src/TotalCall.Client/TotalCall.Client.csproj` currently has package/app version metadata at `0.5.0-beta.1`.
- `src/TotalCall.Client/wwwroot/data/changelog.json` latest entry is `0.12.1-beta.1`.
- The deploy workflow derives production `InformationalVersion` from the git tag.

## Branch And Main Policy

To verify before release:

- main branch policy is not documented in code.
- GitHub branch protection settings are outside this repo and must be checked in GitHub.

Recommended release practice:

- merge release-ready work to `main`,
- ensure the working tree is clean,
- run build/tests,
- apply production migrations deliberately,
- sync current competition config and results deliberately,
- create the release tag only after smoke checks are planned.

## Tag-Based Production Deploy

`.github/workflows/deploy-github-pages.yml` deploys on tags matching `v*` and on manual dispatch.

For a tag build:

- `APP_VERSION` is the tag without the leading `v`.
- Blazor is published in Release.
- GitHub Pages base href is rewritten.
- `ANALYTICS_SNIPPET` is injected if the secret is configured.
- The Pages artifact is deployed.

Do not tag until the v1.0.0 checklist is complete.

## Data Sync Workflow

`.github/workflows/sync-data.yml` runs manually and weekly.

It calls:

```bash
./scripts/sync-supabase.sh "$COMPETITION_JSON" "$SOURCE" "$RESULTS"
```

The wrapper:

1. syncs competition config,
2. imports athlete history for requested source(s),
3. imports official results when results mode is `auto` or a file path.

## Migrations

Production migration process must be deliberate:

1. Review pending migrations.
2. Confirm no migration contains fixture/dev data.
3. Confirm no migration exposes private tables publicly.
4. Apply to staging or local first.
5. Apply to production only with explicit confirmation.
6. Smoke test auth, Cloud Save, submit lock, standings, public board, and profile.

Do not run production migrations from an AI agent session unless explicitly instructed.

## Competition Config Sync

Before release:

1. Review current competition JSON.
2. Confirm `configVersion` is correct.
3. Run the competition sync with production service-role credentials only after approval.
4. Confirm `competitions.published_version_id` points to the intended immutable version.
5. Confirm the app loads config from Supabase and falls back to bundled JSON only when expected.

## Results Import And Scoring Recompute

Before release:

1. Confirm official results JSON files are current.
2. Run the results import after approval.
3. Confirm `score_snapshots.rules_version = "placement-v2"`.
4. Confirm `scored_groups_count`, `total_groups_count`, and status are expected.
5. Confirm public leaderboard ordering.
6. Confirm own score and public board breakdown render correctly.

Changing scoring rules requires rerunning the importer for affected competitions.

## Frontend Deploy

Expected production deploy path:

```bash
git tag v1.0.0
git push origin v1.0.0
```

Only do this after explicit confirmation.

## Production Smoke Checks

After deploy:

- App loads on the production URL.
- Home page loads competitions from Supabase.
- No browser-delivered competition JSON fallback is present or used.
- Magic Link can be requested.
- Auth callback works.
- Profile page loads and display name is correct.
- Anonymous draft saves locally.
- Signed-in draft syncs privately.
- Submit is accepted before lock and blocked after lock.
- Standings show submitted participants or leaderboard as expected.
- Public leaderboard safe fields are visible.
- Public board opens from standings after lock/scoring.
- Public board does not expose raw `answers_json`.
- Own locked board loads `get_my_score` for the signed-in user.
- Withdrawn athlete state renders correctly.
- Privacy copy is accurate.
- Analytics snippet, if enabled, loads without sending custom prediction data.

## Rollback Considerations

Frontend rollback:

- Re-deploy an earlier tag or manually dispatch a known-good commit.
- Confirm base href and analytics injection are still correct.

Data rollback:

- Competition versions are immutable; republish an older known-good `competition_versions` row if needed.
- Official results and score snapshots can be re-imported from known-good result files.
- Avoid manual production edits except for controlled emergency operations.

Database rollback:

- Supabase migrations are forward-oriented in this repo.
- Have a database backup/restore plan outside this repo before applying production schema changes.

## v1.0.0 Checklist

- [ ] Build passes: `./scripts/restore.sh && ./scripts/build.sh`.
- [ ] Tests pass: `./scripts/test.sh`.
- [ ] Supabase migrations applied intentionally.
- [ ] Competition config synced.
- [ ] Current results imported.
- [ ] Score snapshots recomputed with `rules_version = "placement-v2"`.
- [ ] Public leaderboard works.
- [ ] Public board safe fields verified.
- [ ] Public board does not expose raw `answers_json`.
- [ ] Public board does not expose email or user_id.
- [ ] `get_my_score` returns only the signed-in user's score.
- [ ] Privacy copy is accurate for public boards and leaderboard.
- [ ] Withdrawal state works before deadline.
- [ ] Withdrawal state works after lock.
- [ ] Dev scenarios documented and usable.
- [ ] Production smoke test done.
- [ ] Version metadata/changelog inconsistency resolved.
- [ ] No service-role keys committed or exposed in frontend config.

## v1.0.0 Release Notes Draft

Draft only. Do not publish without owner review.

### TotalCall v1.0.0

TotalCall v1.0.0 documents and stabilizes the production prediction flow for powerlifting competitions:

- Supabase Magic Link sign-in and private Cloud Save.
- Runtime competition config from Supabase.
- Submit and backend-enforced prediction lock.
- Participants and standings.
- `placement-v2` scoring for Top-N/category placement boards.
- Partial and final official results imports.
- Public leaderboard and sanitized public boards after lock/scoring.
- Roster withdrawal handling and competition updates timeline.
- Local dev scenarios for open, locked, partial, final, empty, and roster-update states.

Known limitations for v1.0.0:

- In-app privacy copy must be rechecked whenever public standings or board RPC fields change.
- Public self-service account deletion/cloud-data deletion is not implemented.
- Branch protection policy is outside the repo and must be verified in GitHub.
- Generic non-placement module scoring is not implemented as official scoring.
- Current production release version metadata and app changelog version need reconciliation.
