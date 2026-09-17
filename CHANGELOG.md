# Changelog

## 0.4.0

### Changed

- **Requires `Featureflip.Client` 2.9.0 or later.** The 2.7.0 floor set in 0.3.0 went stale
  exactly the way the 2.6.0 floor before it did: NuGet resolves a `PackageReference` to the
  *lowest* version that satisfies it, so every consumer of 0.3.x kept resolving 2.7.0 while
  four releases of evaluation-contract and delivery fixes reached direct SDK users and
  silently skipped this provider's. Raising the floor hands OpenFeature consumers what
  direct users already have:

  - an unrecognised condition operator now fails closed instead of matching every user. The
    dispatch returned `false`, which a negated condition inverted to `true`, so a config
    naming an operator this SDK did not know could silently target everyone
    (#2262)
  - an unrecognised `FlagType` no longer discards the whole config fetch, so one new
    server-side flag type can no longer blank out every flag the provider serves
    (#2401)
  - an unrecognised operator is tolerated at deserialization rather than failing the payload
    (#2372), and an integer is
    rejected where an enum string belongs instead of being resolved by ordinal into a
    variant the wire format never named
    (#2315)
  - analytics events survive a transient failure of the events endpoint instead of being
    discarded, with the queue bounded at 10,000 events and a backoff so a failing endpoint
    cannot turn into one request per evaluation
    (#2456)
  - `FlushAsync` no longer opens a second drain loop while one is already running
    (#2477), and the first SSE
    reconnect after a healthy stream drops is jittered rather than firing in lockstep with
    every other client (#2508)
  - a routine mid-frame SSE sever is logged at `Debug` rather than `Warning`, so ordinary
    operation behind a CDN stops reading as a recurring alarm
    (#2457)

- **A `Before`/`After` date operand must now be ISO-8601 or a Unix timestamp in seconds.**
  This arrives through the same floor raise, and it is why 0.4.0 is a minor rather than a
  patch. `Featureflip.Client` made the same `DateTimeOffset.TryParse` call the evaluation
  engine makes and so inherited its leniency about the date *format*: `05/15/2023`,
  `Jan 1 2024`, `2024.01.01` and similar resolved there and matched nothing in the go, java,
  python, ruby, php and js SDKs, so the same saved flag config targeted differently
  depending on which SDK read it. Date operands are now case-sensitive as well
  (`2024-01-31t09:30:00z` matches nothing where `2024-01-31T09:30:00Z` matches). If one of
  your rules uses a non-ISO operand, rewrite it as ISO-8601 (`2023-05-15`,
  `2023-05-15T09:30:00Z`, `2023-05-15T09:30:00+05:00`) or as a Unix timestamp in seconds.
  (#2480,
  #2468)

  This provider's own surface is unchanged. As with 0.3.0, a behaviour change arriving
  through a dependency is released as a minor rather than a patch — a patch would give the
  weakest possible signal for the most surprising change.

### Fixed

- The NuGet listing renders release notes. Nothing declared `PackageReleaseNotes`, so the
  package page said nothing about what changed in any version; it now links the matching
  Release on the public mirror
  (#2553).

## 0.3.1

### Fixed

- The package now ships its README, so the NuGet listing has content instead of a bare
  dependency table. `dotnet pack` had been warning `is missing a readme` on every release
  since 0.1.0 — the file existed at the package root the whole time, but nothing declared
  `PackageReadmeFile` or packed it. `Featureflip.Client` gets this from
  `packages/csharp-sdk/src/Directory.Build.props`; this project has no such file, so the
  same two lines are declared inline.

- The listing also names its publisher. `Authors`, `Company`, `RepositoryType` and
  `Copyright` were never set on this project, so NuGet fell back to showing the assembly
  name as the author. The values match `Featureflip.Client`'s.

## 0.3.0

### Changed

- **Requires `Featureflip.Client` 2.7.0 or later.** NuGet resolves a `PackageReference` to
  the *lowest* version that satisfies it, so the previous 2.6.0 floor meant an OpenFeature
  consumer kept resolving 2.6.0 however far the SDK moved on — every evaluation-contract
  fix since then reached direct SDK users and silently skipped this provider's. Raising the
  floor hands OpenFeature consumers what direct users already have:

  - a type-mismatched read returns the caller's default and reports `EvaluationReason.Error`
    instead of the evaluator's success reason
    (#2281)
  - a closed handle serves the caller's default from every accessor and reports
    not-initialized, rather than evaluating against a frozen snapshot that can never update
    (#2309)
  - a null `EvaluationContext` no longer throws a `NullReferenceException` out of an
    evaluation that had already succeeded
    (#2311)

  This provider's own surface is unchanged, but a type-mismatched read that used to return
  the served value now returns your default. That is a behaviour change arriving through a
  dependency, so it is released as a minor rather than a patch — a patch would have given
  the weakest possible signal for the most surprising change.

### Fixed

- `OpenFeature` moves to 2.14.1
  (#2216).

## 0.2.1

### Fixed

- `LICENSE` is now the verbatim Apache-2.0 text. Three phrases in the operative sections
  had been reworded and the appendix dropped, which left automated license scanners unable
  to identify it. The license itself is unchanged; the file now says what it always
  claimed to.

## 0.2.0

### Added

- **`PROVIDER_CONFIGURATION_CHANGED` events.** The provider now subscribes to the SDK's
  `FlagsChanged` event and emits OpenFeature's `ProviderConfigurationChanged` with the
  affected flag keys, so consumers can react to a change rather than poll a value.
  Subscription happens after initialization, so the initial flag load is never reported as
  a configuration change — OpenFeature signals that with `PROVIDER_READY` — and it is torn
  down on `ShutdownAsync` even for a caller-injected client. This closes the last parity
  gap with `@featureflip/openfeature-node` (#2080).

### Changed

- **Requires `Featureflip.Client` 2.6.0 or later**, which introduces the `FlagsChanged`
  event this release builds on.

## 0.1.0

- Initial release: OpenFeature provider for the Featureflip .NET server SDK.
