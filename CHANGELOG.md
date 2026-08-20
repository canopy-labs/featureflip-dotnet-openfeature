# Changelog

## 0.3.0

### Changed

- **Requires `Featureflip.Client` 2.7.0 or later.** NuGet resolves a `PackageReference` to
  the *lowest* version that satisfies it, so the previous 2.6.0 floor meant an OpenFeature
  consumer kept resolving 2.6.0 however far the SDK moved on — every evaluation-contract
  fix since then reached direct SDK users and silently skipped this provider's. Raising the
  floor hands OpenFeature consumers what direct users already have:

  - a type-mismatched read returns the caller's default and reports `EvaluationReason.Error`
    instead of the evaluator's success reason
    ([#2281](https://github.com/canopy-labs/featureflip/issues/2281))
  - a closed handle serves the caller's default from every accessor and reports
    not-initialized, rather than evaluating against a frozen snapshot that can never update
    ([#2309](https://github.com/canopy-labs/featureflip/issues/2309))
  - a null `EvaluationContext` no longer throws a `NullReferenceException` out of an
    evaluation that had already succeeded
    ([#2311](https://github.com/canopy-labs/featureflip/issues/2311))

  This provider's own surface is unchanged, but a type-mismatched read that used to return
  the served value now returns your default. That is a behaviour change arriving through a
  dependency, so it is released as a minor rather than a patch — a patch would have given
  the weakest possible signal for the most surprising change.

### Fixed

- `OpenFeature` moves to 2.14.1
  ([#2216](https://github.com/canopy-labs/featureflip/issues/2216)).

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
