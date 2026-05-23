# TotalCall Versioning

This project uses two separate version concepts:

1. `appVersion` - application release/build version.
2. `schemaVersion` - format version of locally saved prediction data.

## Release tags

Create releases with git tags in this format:

- `v0.1.0-beta.1`
- `v0.1.0-beta.2`
- `v0.2.0-beta.1`
- `v1.0.0`

Tag prefix `v` is required.

## How to publish a release

1. Update code and commit.
2. Create tag:
   - `git tag v0.1.0-beta.1`
3. Push commit and tag:
   - `git push origin main --tags`
4. GitHub Actions builds GitHub Pages and sets `AssemblyInformationalVersion` from the tag.

## Non-tag builds

If a build runs without a release tag (for example from `main`), workflow sets dev version:

- `0.1.0-dev+<shortSha>`

Example:

- `0.1.0-dev+1a2b3c4`

`<shortSha>` is build metadata and helps trace exact commit.

## When to bump beta number

Increase `beta.N` when:

- you publish another beta for the same target release line,
- you changed features/fixes and want a new testable build,
- schema format did not change.

Example:

- `v0.1.0-beta.1` -> `v0.1.0-beta.2`

## When to bump minor

Increase minor (`0.1` -> `0.2`) when:

- scope of features clearly grows,
- release line changes,
- you start a new beta cycle for a broader milestone.

Example:

- `v0.1.0-beta.4` -> `v0.2.0-beta.1`

## appVersion vs schemaVersion

`appVersion`:

- comes from `AssemblyInformationalVersion`,
- changes per release/build,
- is stored in localStorage data and exported JSON.

`schemaVersion`:

- describes JSON/localStorage data shape,
- changes only when saved data format changes,
- does not change for every app release.

Current storage schema:

- `StorageSchemaVersion = 1`

## Saved data metadata

Each saved/exported prediction payload includes:

- `appVersion`
- `schemaVersion`
- `competitionId`
- `savedAt`
- `answers`
