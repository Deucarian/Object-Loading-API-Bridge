using System;
using NUnit.Framework;
using Deucarian.API.Models;
using Deucarian.ObjectLoading;

namespace Deucarian.ObjectLoading.APIIntegration.Tests
{
    public sealed class ApiObjectDownloadMapperTests
    {
        [Test]
        public void CreateApiRequest_ForwardsHeadersAndBearerToken()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.BearerToken = "Bearer secret-token";
            request.TimeoutSeconds = 42;
            request.AddHeader("X-Trace", "abc");

            ApiRequest apiRequest = ApiObjectDownloadMapper.CreateApiRequest(
                ObjectSource.DirectUrl("https://example.com/object.bundle?platform=webgl"),
                request,
                ApiAuthenticationRequirement.Optional);

            Assert.AreEqual("https://example.com/object.bundle?platform=webgl", apiRequest.Endpoint);
            Assert.AreEqual(ApiAuthenticationRequirement.Required, apiRequest.Authentication);
            Assert.AreEqual(ApiResponseFormat.Bytes, apiRequest.ResponseFormat);
            Assert.AreEqual("secret-token", apiRequest.BearerTokenOverride);
            Assert.AreEqual(42, apiRequest.TimeoutSeconds);
            Assert.AreEqual("abc", apiRequest.Headers["X-Trace"]);
            Assert.False(apiRequest.Headers.ContainsKey("Authorization"));
        }

        [Test]
        public void CreateApiRequest_LegacyOverloadLeavesTokenlessRequestDisabled()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl(
                "https://example.com/object.bundle");

            ApiRequest apiRequest = ApiObjectDownloadMapper.CreateApiRequest(
                ObjectSource.DirectUrl(request.Url),
                request);

            Assert.AreEqual(
                ApiAuthenticationRequirement.Disabled,
                apiRequest.Authentication);
            Assert.IsNull(apiRequest.BearerTokenOverride);
        }

        [TestCase(ApiAuthenticationRequirement.UseConfigDefault)]
        [TestCase(ApiAuthenticationRequirement.Required)]
        [TestCase(ApiAuthenticationRequirement.Optional)]
        public void CreateApiRequest_UsesProviderAuthenticationWithoutTokenOverride(
            ApiAuthenticationRequirement providerAuthentication)
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl(
                "https://example.com/object.bundle");

            ApiRequest apiRequest = ApiObjectDownloadMapper.CreateApiRequest(
                ObjectSource.DirectUrl(request.Url),
                request,
                providerAuthentication);

            Assert.AreEqual(
                providerAuthentication,
                apiRequest.Authentication);
            Assert.IsNull(apiRequest.BearerTokenOverride);
            Assert.False(apiRequest.Headers.ContainsKey("Authorization"));
        }

        [Test]
        public void CreateApiRequest_ParsesExplicitAuthorizationHeader()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.AddHeader("Authorization", "Bearer header-token");

            ApiRequest apiRequest = ApiObjectDownloadMapper.CreateApiRequest(
                ObjectSource.DirectUrl(request.Url),
                request);

            Assert.AreEqual(ApiAuthenticationRequirement.Required, apiRequest.Authentication);
            Assert.AreEqual("header-token", apiRequest.BearerTokenOverride);
            Assert.False(apiRequest.Headers.ContainsKey("Authorization"));
        }

        [Test]
        public void CreateAssetBundleApiRequest_MapsAssetBundleTransportOptions()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.BearerToken = "Bearer secret-token";
            request.TimeoutSeconds = 42;
            request.CacheMode = ObjectLoadCacheMode.UseUnityCache;
            request.CacheKey = "model";
            request.CacheHash = "0123456789abcdef0123456789abcdef";
            request.CacheVersion = 7;
            request.Crc = 99;
            request.AddHeader("X-Trace", "abc");

            ApiRequest apiRequest = ApiObjectDownloadMapper.CreateAssetBundleApiRequest(
                ObjectSource.DirectUrl("https://example.com/object.bundle?platform=webgl"),
                request);

            Assert.AreEqual("https://example.com/object.bundle?platform=webgl", apiRequest.Endpoint);
            Assert.AreEqual(ApiResponseFormat.AssetBundle, apiRequest.ResponseFormat);
            Assert.AreEqual(ApiAuthenticationRequirement.Required, apiRequest.Authentication);
            Assert.AreEqual("secret-token", apiRequest.BearerTokenOverride);
            Assert.AreEqual(42, apiRequest.TimeoutSeconds);
            Assert.AreEqual("abc", apiRequest.Headers["X-Trace"]);
            Assert.False(apiRequest.Headers.ContainsKey("Authorization"));
            Assert.NotNull(apiRequest.AssetBundleOptions);
            Assert.AreEqual(ApiAssetBundleCacheMode.UseUnityCache, apiRequest.AssetBundleOptions.CacheMode);
            Assert.AreEqual("model", apiRequest.AssetBundleOptions.CacheKey);
            Assert.AreEqual("0123456789abcdef0123456789abcdef", apiRequest.AssetBundleOptions.CacheHash);
            Assert.AreEqual(7, apiRequest.AssetBundleOptions.CacheVersion);
            Assert.AreEqual(99, apiRequest.AssetBundleOptions.Crc);
            Assert.AreEqual("unity-cache-key-hash", ApiObjectDownloadMapper.DescribeCacheStatus(request));
        }

        [Test]
        public void CreateAssetBundleApiRequest_UsesRequiredProviderAuthenticationWithoutTokenOverride()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl(
                "https://example.com/object.bundle");

            ApiRequest apiRequest =
                ApiObjectDownloadMapper.CreateAssetBundleApiRequest(
                    ObjectSource.DirectUrl(request.Url),
                    request,
                    ApiAuthenticationRequirement.Required);

            Assert.AreEqual(
                ApiAuthenticationRequirement.Required,
                apiRequest.Authentication);
            Assert.IsNull(apiRequest.BearerTokenOverride);
            Assert.False(apiRequest.Headers.ContainsKey("Authorization"));
        }

        [Test]
        public void DescribeCacheStatus_TrimmedHashUsesUnityCachePath()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.CacheMode = ObjectLoadCacheMode.UseUnityCache;
            request.CacheHash = "  0123456789abcdef0123456789abcdef  ";

            Assert.AreEqual(
                "unity-cache-hash",
                ApiObjectDownloadMapper.DescribeCacheStatus(request));
        }

        [Test]
        public void MapApiResult_MapsSuccessfulBytes()
        {
            byte[] bytes = { 1, 2, 3 };
            ApiResult<byte[]> apiResult = ApiResult<byte[]>.Success(
                bytes,
                Deucarian.API.HttpMethod.GET,
                200,
                "https://example.com/object.bundle",
                null);

            ObjectDownloadResult result = ApiObjectDownloadMapper.MapApiResult(apiResult);

            Assert.True(result.Succeeded);
            Assert.AreEqual(200, result.HttpStatusCode);
            Assert.AreSame(bytes, result.Bytes);
        }

        [Test]
        public void MapApiResult_MapsApiError()
        {
            ApiError error = new ApiError
            {
                Message = "Denied",
                HttpStatusCode = 403,
                RequestUrl = "https://example.com/object.bundle",
                Exception = new InvalidOperationException("Nope")
            };
            ApiResult<byte[]> apiResult = ApiResult<byte[]>.Failure(error, Deucarian.API.HttpMethod.GET);

            ObjectDownloadResult result = ApiObjectDownloadMapper.MapApiResult(apiResult);

            Assert.False(result.Succeeded);
            Assert.AreEqual(ObjectLoadErrorCode.DownloadFailed, result.Error.Code);
            Assert.AreEqual("Denied", result.Error.Message);
            Assert.AreEqual(403, result.Error.HttpStatusCode);
            Assert.AreEqual("Nope", result.Error.ExceptionMessage);
        }

        [Test]
        public void DebugSnapshot_RedactsBearerTokenAndSensitiveHeaders()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.BearerToken = "secret-token";
            request.AddHeader("X-Access-Token", "header-secret");
            request.AddHeader("Proxy-Authorization", "proxy-secret");
            request.AddHeader("Api-Key", "api-key-secret");
            request.AddHeader("X-Trace", "visible");

            string json = ApiObjectDownloadMapper.CreateDebugSnapshotJson(
                ObjectSource.DirectUrl(request.Url),
                request);

            Assert.False(json.Contains("secret-token"));
            Assert.False(json.Contains("header-secret"));
            Assert.False(json.Contains("proxy-secret"));
            Assert.False(json.Contains("api-key-secret"));
            Assert.True(json.Contains("[redacted]"));
            Assert.True(json.Contains("visible"));
        }

        [Test]
        public void DebugSnapshot_RecordsProviderAuthenticationWithoutTokenMaterial()
        {
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl(
                "https://example.com/object.bundle");

            string json = ApiObjectDownloadMapper.CreateDebugSnapshotJson(
                ObjectSource.DirectUrl(request.Url),
                request,
                ApiAuthenticationRequirement.Required);

            Assert.True(json.Contains("\"authentication\": \"Required\""));
            Assert.True(json.Contains("\"bearer_token_override\": null"));
            Assert.False(json.Contains("Authorization"));
        }
    }
}
