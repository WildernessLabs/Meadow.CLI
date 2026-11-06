# ADR 0002: CI on every PR and releases built from tags

## Status

Accepted (2026-11-05).

## Context

- Releases 2.0.65 through 3.0.2 were built and pushed to NuGet by hand. The last git tag is `v2.0.17.0` (2024). The commits behind 3.0.1 and 3.0.2 are only recorded in the nuspec `repository` element (`45c1e1f` and `02f2024`, both on `meadow-3.0`).
- `dotnet-ci.yml` ran on `develop` only, with the .NET 8 SDK. `meadow-3.0` targets net10.0, so the line that ships had no CI.
- `dotnet.yml` built `main` with the version pinned at 2.0.17.0 and only uploaded an artifact. It did not publish.
- The build checked out `Meadow.Contracts`, `Meadow.Logging` and `Meadow.Units` beside the CLI at their `develop` branches. A local build used whatever those checkouts were at. Neither could be reproduced. The only file actually compiled in was `CircularBuffer.cs`, unused since e5c268f.
- The version was set by hand in three places: `PackageVersion` in the csproj and two constants in `Properties/AssemblyInfo.cs`.
- One person publishes the CLI.

## Decision

1. `meadow-3.0` is the trunk for now. `develop` keeps CI so it is not left broken, but releases come from `meadow-3.0`.
2. The CLI builds from its own checkout. The `CircularBuffer.cs` link and the three reference projects in the solution are removed.
3. `<Version>` in `Source/Meadow.CLI/Meadow.CLI.csproj` is the only version. The SDK generates the assembly attributes, and the informational version carries `+<commit>`, so `meadow --version` identifies the exact build.
4. `ci.yml` runs on every PR and push to `meadow-3.0` and `develop`: build, unit tests, and the `.nupkg` as an artifact.
5. `release.yml` runs when a `v*` tag is pushed. It fails if the tag does not equal `v<Version>`, then builds, runs unit tests, publishes to NuGet, and creates a GitHub Release with generated notes and the `.nupkg` attached. A version with a `-` suffix is marked as a prerelease.
6. NuGet publishing uses Trusted Publishing (`NuGet/login`, GitHub OIDC). No long-lived key is stored in the repo. The NuGet account name is the repo variable `NUGET_USER`.
7. Release notes are grouped by PR label (`.github/release.yml`). The PR template asks for the `Meadow_Issues` number and a hardware test for changes to `Meadow.HCom` or device commands.

To release: bump `<Version>` in a PR, merge it, then tag that commit `vX.Y.Z` and push the tag.

## Alternatives

- **Pin the sibling repos to commits.** Not needed once nothing is compiled from them.
- **Derive the version from the tag (MinVer or Nerdbank.GitVersioning).** Removes the version bump PR, but local builds then get a computed version and the tool adds a dependency. With one publisher, an explicit bump plus a tag check is simpler.
- **Keep the stored `NUGET_API_KEY` secret.** It was last set on 2023-04-02. NuGet API keys last at most a year, so it cannot be relied on, and Trusted Publishing needs no stored key at all.
- **Publish on merge to the trunk.** Every merge would be a release. Tags keep releases deliberate.

## Consequences

- A release needs no local build. What is on NuGet is the tagged commit.
- `meadow --version` output changes from `3.0.0-beta5` style to `3.0.2+<sha>`. `VersionChecker` already strips `+...` before comparing.
- Building in Visual Studio no longer loads the three sibling projects in the `_ref` solution folder.
- Before the first tagged release: add a Trusted Publishing policy on nuget.org for `WildernessLabs.Meadow.CLI` (owner Wilderness Labs or the publishing account, repository `WildernessLabs/Meadow.CLI`, workflow `release.yml`), and set the repo variable `NUGET_USER`.
- Hardware testing stays manual.

## Implementation Status

- Done on `ci/standalone-build-release`: standalone build, csproj version, `ci.yml`, `release.yml`, `.github/release.yml`, PR template. The old `dotnet-ci.yml` and `dotnet.yml` are removed.
- Verified locally: a clean clone with no sibling repos builds in Release, the 82 unit tests pass, it packs `WildernessLabs.Meadow.CLI.3.0.2.nupkg`, and `meadow --version` prints `3.0.2+<sha>`.
- Not verified: the workflows on GitHub, and the Trusted Publishing login, until the policy exists and a tag is pushed.
- To do: tag `v3.0.1` at `45c1e1f` and `v3.0.2` at `02f2024` so the published versions have tags.
