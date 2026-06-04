# Privacy & Analytics

How TotalCall handles user data, browser storage, authentication, private Cloud Save, public athlete data, exports, and visit statistics.

Last updated: 2026-06-04.

---

## Current data flow

- Signed-out users store prediction drafts only in their browser.
- Signed-in users store prediction drafts locally and synchronize a private snapshot to Supabase.
- Prediction drafts are not public and are not used for scoring or leaderboards.
- Athlete history and analytics are public sports data and are separate from user accounts and private predictions.

---

## Browser localStorage

TotalCall uses `localStorage` so drafts, preferences, and sessions survive page reloads.

**What is stored locally:**

- Prediction drafts per competition
- UI preferences: theme, language, and selected athlete-data source
- Changelog "seen" state
- Supabase Auth session data while signed in: access token, refresh token, expiry, user ID, and email
- Short-lived PKCE login state and the last Magic Link send timestamp

**Effects of browser actions:**

- Signing out clears the persisted Auth session but leaves local prediction drafts and preferences.
- Clearing browser data removes local drafts, preferences, and the local Auth session.
- Clearing browser data does not delete the Supabase account or private cloud drafts.
- A signed-in user can restore an available cloud draft on another device.

`localStorage` is browser storage and is accessible to JavaScript running on the TotalCall origin. Avoid adding untrusted scripts and treat cross-site scripting prevention as security-critical.

---

## Authentication and account data

TotalCall uses [Supabase Auth](https://supabase.com/docs/guides/auth/) with passwordless email Magic Links.

**Account-related data:**

- Supabase Auth stores the email address and a stable user ID required for Magic Link sign-in.
- TotalCall creates a private `profiles` row keyed by that user ID. The optional display name is currently not collected by the app.
- Authenticated requests use an access token tied to the user.

TotalCall does not collect or store a password.

Signing out is not account deletion. The current app does not yet provide self-service account deletion.

---

## Private Cloud Save

Cloud Save uses the Supabase `prediction_submissions` table.

**Stored for each signed-in user and competition:**

- User ID and competition ID
- Status (`draft` in the current frontend)
- Complete `PredictionSet` JSONB snapshot, including picks and prediction metadata
- App version and prediction schema version
- Created and updated timestamps
- Optional submitted timestamp reserved for future submitted-state support

There is one cloud row per user and competition. The local draft remains the immediate cache; cloud synchronization is additional storage, not a replacement for localStorage.

When both local and cloud snapshots exist, TotalCall keeps the snapshot with the newer `SavedAt` value and writes it to the other storage location.

### Cloud Save access control

- Row Level Security is enabled for `profiles` and `prediction_submissions`.
- Authenticated users can select, insert, and update only rows that belong to their own user ID.
- The unauthenticated `anon` role has no access to these tables.
- There is no public-read policy for prediction submissions.
- No delete permission or delete policy is exposed to users in Cloud Save v1.
- Cloud drafts are not exposed through leaderboards, scoring, public profiles, or sharing links.

See the migration: `supabase/migrations/20260604120000_add_cloud_prediction_saves.sql`.

### Deletion and retention limitations

- Signing out does not delete local drafts or cloud drafts.
- Clearing browser data does not delete server-side account or cloud data.
- Cloud Save v1 has no self-service delete button and no automatic expiry policy.
- Deleting a Supabase Auth user server-side cascades to that user's profile and prediction submissions.

This limitation must be addressed before presenting TotalCall as a production-ready account system.

---

## Public athlete data

Athlete history and analytics are fetched from public Supabase tables and RPCs populated from OpenIPF and OpenPowerlifting data.

- These records describe public competition results and athlete performance.
- They are readable without signing in.
- They are not connected to a user's account, email, local drafts, or private Cloud Save.
- Selected athlete-data-source preferences are stored only in localStorage.

---

## Export and sharing

JSON and CSV exports, copied summaries, and app links are generated in the browser.

- TotalCall does not automatically upload exported files or copied summaries.
- App links do not contain prediction picks.
- If a user manually shares an export or summary, the recipient controls the shared copy.

---

## Analytics

Analytics is injected at build time by GitHub Actions and is not present in local development unless explicitly added.

### How injection works

1. `wwwroot/index.html` contains the `<!-- ANALYTICS_SNIPPET -->` placeholder.
2. The deployment workflow reads the `ANALYTICS_SNIPPET` GitHub Actions secret.
3. After `dotnet publish`, a Perl command replaces the placeholder with the secret value.
4. If the secret is empty, the build continues without an analytics script.
5. The updated `index.html` is copied to `404.html`.

The current disclosed provider is [Cloudflare Web Analytics](https://developers.cloudflare.com/web-analytics/about/), which Cloudflare describes as privacy-first, cookieless, and not collecting or using visitors' personal data.

**What TotalCall does not send through custom analytics events:**

- Email addresses or Auth tokens
- Prediction content or cloud snapshots
- Selected athlete names or options
- localStorage contents
- Exported files or copied summaries

Page-view analytics may include the visited app path. Prediction choices are not encoded in TotalCall URLs.

TotalCall does not use Google Analytics.

---

## Third-party services

| Service | Purpose |
|---|---|
| GitHub Pages | Static application hosting |
| Supabase Auth | Magic Link authentication and user identity |
| Supabase Database | Private Cloud Save and public athlete data |
| Cloudflare Web Analytics | Cookieless page-view statistics when injected in deployment |
| OpenIPF / OpenPowerlifting | Source data for public athlete history and analytics |

As with other hosted web applications, infrastructure providers can process normal request metadata such as IP addresses, timestamps, and user-agent information under their own terms. TotalCall does not write this request metadata into `profiles` or `prediction_submissions`.

---

## UI disclosure

Users can read a plain-language summary by clicking **"Prywatność i dane"** / **"Privacy & data"** in the app footer. It covers:

- Browser storage and Auth session data
- Magic Link account data and private Cloud Save
- Visibility, RLS, and current deletion limitations
- Export and sharing behavior
- Public athlete-data sources
- Visit statistics
- Beta-version limitations

There is currently no cookie banner because the disclosed analytics provider is cookieless. Reassess the UI and legal requirements before introducing cookies, advertising, new analytics events, public predictions, scoring, leaderboards, or additional personal-data processing.
