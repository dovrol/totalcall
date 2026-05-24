# Privacy & Analytics

How TotalCall handles user data, browser storage, and visit statistics.

---

## localStorage

TotalCall saves user picks locally in the browser using `localStorage`. This is a core feature of the app — without it, picks would be lost on every page refresh.

**What is stored:**
- User picks (prediction selections) per competition
- UI preferences (theme, language)
- Changelog "seen" state (which releases the user has already dismissed)

**What is NOT stored server-side:**
- Picks are never sent to a server.
- There is no account, no cloud sync, no database.
- Clearing browser data or switching devices will erase picks.

**Consequence for users:** Export picks to JSON before clearing browser data or switching devices.

---

## Analytics

Analytics is injected at build time by GitHub Actions. It is not present in local development.

### How injection works

1. `wwwroot/index.html` contains a placeholder comment:
   ```html
   <!-- ANALYTICS_SNIPPET -->
   ```
2. The GitHub Actions workflow reads the secret `ANALYTICS_SNIPPET`.
3. After `dotnet publish`, a Python step replaces the placeholder with the secret value.
4. If the secret is empty or not set, the placeholder is silently removed and the build continues without analytics.
5. The updated `index.html` is also copied to `404.html` to keep both files in sync.

### Secret format

The `ANALYTICS_SNIPPET` secret should contain the full provider script tag. Example for Cloudflare Web Analytics:

```
<script defer src="https://static.cloudflareinsights.com/beacon.min.js" data-cf-beacon='{"token":"YOUR_TOKEN_HERE"}'></script>
```

The secret is never logged or printed in workflow output.

### Switching analytics providers

Because the snippet is fully contained in the secret, changing providers requires only updating the `ANALYTICS_SNIPPET` secret — no code changes needed, as long as the new provider also uses a single script tag for page-view tracking.

If the new provider requires custom event calls or SDK initialisation beyond a script tag, additional code changes will be needed at that point.

### What is NOT tracked

The analytics snippet only records page views via the external provider beacon. TotalCall itself does not send any additional data. The following is never sent to analytics:

- Pick content (which athletes or options a user selected)
- Athlete names
- localStorage contents
- Any personally identifiable information
- Full prediction payloads

---

## Current provider

**Cloudflare Web Analytics** — cookieless, no personal data collection beyond anonymised page views. See [Cloudflare Web Analytics privacy](https://developers.cloudflare.com/analytics/web-analytics/) for details.

TotalCall does **not** use Google Analytics.

---

## Future changes — update this document if any of the following happen

| Change | Action required |
|---|---|
| Add Supabase or any server-side storage | Update the localStorage section to document what is now sent to the server |
| Add user accounts / login | Add a section describing what is stored per user and for how long |
| Add custom analytics events | Verify no pick content, athlete names, or personal data is included in event payloads; update this document |
| Add public leaderboards | Document what data becomes public and how it is stored |
| Switch analytics provider | Update the "Current provider" section and replace the `ANALYTICS_SNIPPET` secret |

---

## UI disclosure

Users can read a plain-language summary of data handling by clicking **"Prywatność i dane"** ("Privacy & data") in the app footer. The disclosure covers:

- Local storage usage and consequences
- Export capability
- Visit statistics (provider named explicitly)
- Beta status warning

There is no cookie banner. No consent gate blocks app use. The disclosure is informational only, because:

- localStorage is necessary for the app's core function (picks would not persist otherwise)
- Analytics is cookieless and does not process personal data
- No consent is legally required for these uses under the current scope

If a future change introduces cookies or personal data processing, this decision must be revisited.
