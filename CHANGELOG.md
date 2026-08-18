# Changelog

## 0.2.7 - 2026-08-18

- Added explicit API-provider authentication overloads for byte downloads,
  AssetBundle content loading, request mapping, and pipeline composition.
- Kept legacy overloads unauthenticated by default while allowing the API
  client to resolve a current provider token at send time without copying it
  into Object Loading requests or debug snapshots.
- Kept explicit bearer tokens authoritative and documented the caller-owned
  origin validation required before enabling provider authentication.

## 0.2.6 - 2026-07-17

- Kept the imported API loader sample within its declared dependency boundary by exposing status text without a direct Logging assembly reference.
- Reused Object Loading's canonical request helpers, completed the integration sample contract, and updated exact API and Object Loading dependencies.

## 0.2.5 - 2026-06-22

- Updated exact API and Object Loading dependencies for the accepted stable release line.

## 0.2.4 - 2026-06-22

- Merged main branch history into develop for stable promotion ancestry.
- Updated the exact `com.deucarian.api` dependency to `1.1.1`.

## 0.2.3 - 2026-06-22

- Updated the exact `com.deucarian.object-loading` dependency to `1.2.0`.

## 0.2.2 - 2026-06-22

- Routed the API AssetBundle Loader sample through Object Loading's package-owned log categories instead of direct Unity Debug calls.
- Kept the integration package dependency set unchanged by using the existing `ObjectLoadingLog` facade from `com.deucarian.object-loading`.

## 0.2.1 - 2026-06-22

- Updated package repository metadata to `Deucarian/ObjectLoading-API-Integration`.
- Documented the current package version and aligned the API dependency reference with `package.json`.

## 0.2.0 - 2026-06-19

- Added `ApiAssetBundleSourceContentLoader` for API-backed `UnityWebRequestAssetBundle` loading without materializing AssetBundle bytes.
- Added `ApiObjectLoadingPipelineFactory` for recommended Object Loading composition with an injected `IApiClient`.
- Mapped Object Loading headers, bearer token overrides, timeout, CRC, cache metadata, progress, and telemetry into the API AssetBundle path.
- Kept `ApiObjectDownloader` as a legacy byte-array workflow and updated the sample to the optimized AssetBundle path.

## 0.1.2 - 2026-06-17

- Renamed the package identity from `com.deucarian.object-loading.api-bridge` to `com.deucarian.object-loading.api-integration`.
- Renamed APIBridge assemblies, namespaces, tests, and samples to APIIntegration.
- Migration: remove the old bridge package ID from Unity manifests and add `com.deucarian.object-loading.api-integration`.

## 0.1.1

- Updated the Object Loading dependency to `0.4.1`.
- Kept Newtonsoft Json aligned at `3.2.2`.
- Added package license metadata.
- Updated README structure with overview, installation, usage, samples, tests, and license sections.

## 0.1.0

- Added initial API Helper integration package metadata.
- Added `ApiHelperObjectDownloader`.
- Added request/result/error mapping helpers.
- Added EditMode tests for auth/header forwarding, byte result mapping, error mapping, and debug redaction.
- Added sample composition script.
