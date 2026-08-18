# Deucarian Object Loading API Integration

## Overview

`com.deucarian.object-loading.api-integration` is an optional UPM package that adapts API to Deucarian Object Loading.

The core `com.deucarian.object-loading` package has no API dependency. Use this integration only when downloads should go through API request handling.

Migration note: replace old manifest entries for `com.deucarian.object-loading.api-bridge` with `com.deucarian.object-loading.api-integration`. Current installs use the `ObjectLoading-API-Integration.git` repository.

## When To Use This

Use this package when a project already uses Deucarian Object Loading and wants its object-download step to run through Deucarian API request handling, authentication, headers, timeouts, cancellation, byte responses, and API error mapping.

Do not use this package for Session lifecycle, backend object/version resolution, direct Object Loading runtime behavior, package installation, caching policy, Addressables, glTF loading, or material/render-pipeline repair. Resolve the final URL elsewhere, then pass it into `ObjectLoadRequest`.

## What It Does

- Implements `IObjectDownloader` as `ApiObjectDownloader`.
- Converts `ObjectLoadRequest` URL, headers, bearer token, timeout, and cancellation into an API `ApiRequest`.
- Requests `ApiResponseFormat.Bytes`.
- Maps `ApiResult<byte[]>` back to `ObjectDownloadResult`.
- Maps API errors to `ObjectLoadError`.
- Keeps authentication explicit through `ObjectLoadRequest.BearerToken`,
  `Authorization: Bearer ...`, or provider-authentication overloads used with
  an explicitly composed API client.

## What It Does Not Do

- No Session dependency.
- No backend URL resolving.
- No backend DTOs.
- No glTF loading.
- No Addressables integration.
- No caching policy.
- No material remapping.

Backend URL resolving remains outside this integration. Resolve the final URL elsewhere, then pass it into `ObjectLoadRequest`.

## Installation

Install this integration after installing its dependencies:

- `com.deucarian.object-loading`
- `com.deucarian.api`

The package depends on `com.deucarian.object-loading` `1.2.2`, `com.deucarian.api` `1.1.4`, and Unity's Newtonsoft Json package `3.2.2`.

Current package version: `0.2.7`.

`com.deucarian.object-loading` supplies the runtime loading pipeline this package adapts. `com.deucarian.api` supplies the request, response, authentication, AssetBundle transport, and progress models used by the integration.

Stable:

```json
"com.deucarian.object-loading.api-integration": "https://github.com/Deucarian/ObjectLoading-API-Integration.git#main"
```

Development:

```json
"com.deucarian.object-loading.api-integration": "https://github.com/Deucarian/ObjectLoading-API-Integration.git#develop"
```

Use `#main` for stable package consumption and `#develop` when testing active package work.

## Quick Start

1. Install `com.deucarian.object-loading`, `com.deucarian.api`, and this integration package.
2. Let Unity finish resolving packages and compiling assemblies.
3. Import the `API AssetBundle Loader Sample` sample if you want a working reference setup.
4. Compose `ApiObjectDownloader` into an `ObjectLoadingPipeline` where the project needs API-backed byte downloads.

## Usage

```csharp
using Deucarian.API.Models;
using Deucarian.ObjectLoading;
using Deucarian.ObjectLoading.APIIntegration;

ObjectLoadingPipeline pipeline = new ObjectLoadingPipeline(
    new DirectUrlSourceResolver(),
    new ApiObjectDownloader(),
    new AssetBundleContentLoader(),
    new AssetBundleObjectInstantiator(),
    new DefaultObjectDiagnostics());

ObjectLoadRequest request = ObjectLoadRequest.FromUrl(assetBundleUrl);
request.BearerToken = accessToken;
request.AddHeader("X-Custom-Header", "value");
```

For explicit composition and tests, pass your own API client:

```csharp
ApiObjectDownloader downloader = new ApiObjectDownloader(apiClient);
```

To let API resolve the current token from that client's `IApiAuthProvider` at
send time, opt in explicitly:

```csharp
ObjectLoadingPipeline authenticatedPipeline =
    ApiObjectLoadingPipelineFactory.Create(
        apiClient,
        ApiAuthenticationRequirement.Required);
```

The same `providerAuthentication` overload is available on
`ApiObjectDownloader`, `ApiAssetBundleSourceContentLoader`, and the mapper
methods. Existing overloads continue to use
`ApiAuthenticationRequirement.Disabled`.

## Auth And Headers

`ObjectLoadRequest.BearerToken` becomes `ApiRequest.BearerTokenOverride`.

An explicit `Authorization: Bearer ...` header is also parsed into `BearerTokenOverride`. Other headers are forwarded unchanged.

When no explicit bearer token is present, a caller can supply
`ApiAuthenticationRequirement.Required`, `Optional`, or `UseConfigDefault`
through the provider-authentication overloads. The resulting `ApiRequest`
contains no token override; API asks the configured `IApiAuthProvider` for its
current token when the request is sent. An explicit bearer token always makes
the request `Required` and takes precedence over the provider-authentication
argument.

Provider authentication is destination-sensitive. Keep it disabled for
untrusted or arbitrary absolute URLs. Applications must validate that a source
is API-relative, same-origin, or explicitly allowlisted before enabling a
provider token for that load.

`ApiObjectDownloadMapper.CreateDebugSnapshotJson(...)` redacts bearer tokens and sensitive headers.

## Samples

Import the **API Downloader Sample** from Unity's Package Manager to see `ApiObjectDownloader` composed into an `ObjectLoadingPipeline`.

## Tests

Run the package's EditMode tests in Unity. Tests cover provider-authentication
forwarding, legacy disabled defaults, explicit auth/header forwarding,
byte-result mapping, error mapping, and debug redaction.

## Validation

Run the shared package validator from the repository root:

```powershell
python C:/Repositories/Package-Registry/Tools/deucarian_package_validator.py --registry-root C:/Repositories/Package-Registry --repository-root . --config deucarian-package.json
```

Documentation-only updates should still pass:

```powershell
git diff --check
```

## Troubleshooting

- Downloads fail before Object Loading receives bytes: inspect the API `ApiResult<byte[]>` error and request headers first.
- Auth does not apply: set `ObjectLoadRequest.BearerToken`, provide an
  `Authorization: Bearer ...` header, or use a provider-authentication overload
  with an API client configured with the expected auth provider.
- The integration is installed but not used: confirm the `ObjectLoadingPipeline` is composed with `ApiObjectDownloader` instead of the default downloader.
- Backend lookup is missing: resolve project/object/version URLs in API or application code before creating the `ObjectLoadRequest`.

## Architecture / Contributor Notes

- [AGENTS.md](AGENTS.md) contains repository-specific ownership and Codex guidance.
- Deucarian architecture rules live in [Package Registry](https://github.com/Deucarian/Package-Registry/blob/develop/ARCHITECTURE.md).
- Capability ownership is tracked in [CAPABILITY_OWNERSHIP.md](https://github.com/Deucarian/Package-Registry/blob/develop/CAPABILITY_OWNERSHIP.md).

## License

MIT. See [LICENSE.md](LICENSE.md).
