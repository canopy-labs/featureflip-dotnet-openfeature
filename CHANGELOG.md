# Changelog

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
