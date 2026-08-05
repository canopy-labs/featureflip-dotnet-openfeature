# Changelog

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
