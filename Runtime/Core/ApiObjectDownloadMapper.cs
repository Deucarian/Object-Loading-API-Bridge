using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Deucarian.API;
using Deucarian.API.Models;
using Deucarian.ObjectLoading;
using UnityEngine;

namespace Deucarian.ObjectLoading.APIIntegration
{
    public static class ApiObjectDownloadMapper
    {
        private const string RedactedValue = "[redacted]";

        public static ObjectLoadError Validate(ObjectSource source)
        {
            if (source == null || string.IsNullOrWhiteSpace(source.Url))
            {
                return ObjectLoadError.Create(
                    ObjectLoadErrorCode.InvalidRequest,
                    "Object source URL is missing.");
            }

            return null;
        }

        public static ApiRequest CreateApiRequest(ObjectSource source, ObjectLoadRequest request)
        {
            return CreateApiRequest(
                source,
                request,
                ApiAuthenticationRequirement.Disabled);
        }

        public static ApiRequest CreateApiRequest(
            ObjectSource source,
            ObjectLoadRequest request,
            ApiAuthenticationRequirement providerAuthentication)
        {
            ObjectLoadError validation = Validate(source);
            if (validation != null)
            {
                throw new ArgumentException(validation.Message, nameof(source));
            }

            Dictionary<string, string> headers = request != null
                ? request.CreateHeaders()
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string bearerToken = ExtractBearerToken(headers);
            if (string.IsNullOrWhiteSpace(bearerToken) && request != null)
            {
                bearerToken = ObjectLoadRequest.StripBearerPrefix(request.BearerToken);
            }

            ApiRequest apiRequest = new ApiRequest(
                source.Url,
                HttpMethod.GET,
                ResolveAuthentication(
                    bearerToken,
                    providerAuthentication))
            {
                ResponseFormat = ApiResponseFormat.Bytes,
                BearerTokenOverride = bearerToken,
                TimeoutSeconds = request != null && request.TimeoutSeconds > 0
                    ? (int?)request.TimeoutSeconds
                    : null
            };

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key)
                    || string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                apiRequest.Headers[header.Key.Trim()] = header.Value;
            }

            return apiRequest;
        }

        public static ApiRequest CreateAssetBundleApiRequest(ObjectSource source, ObjectLoadRequest request)
        {
            return CreateAssetBundleApiRequest(
                source,
                request,
                ApiAuthenticationRequirement.Disabled);
        }

        public static ApiRequest CreateAssetBundleApiRequest(
            ObjectSource source,
            ObjectLoadRequest request,
            ApiAuthenticationRequirement providerAuthentication)
        {
            ObjectLoadError validation = Validate(source);
            if (validation != null)
            {
                throw new ArgumentException(validation.Message, nameof(source));
            }

            Dictionary<string, string> headers = request != null
                ? request.CreateHeaders()
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            string bearerToken = ExtractBearerToken(headers);
            if (string.IsNullOrWhiteSpace(bearerToken) && request != null)
            {
                bearerToken = ObjectLoadRequest.StripBearerPrefix(request.BearerToken);
            }

            ApiRequest apiRequest = new ApiRequest(
                source.Url,
                HttpMethod.GET,
                ResolveAuthentication(
                    bearerToken,
                    providerAuthentication))
            {
                ResponseFormat = ApiResponseFormat.AssetBundle,
                AssetBundleOptions = CreateAssetBundleOptions(request),
                BearerTokenOverride = bearerToken,
                TimeoutSeconds = request != null && request.TimeoutSeconds > 0
                    ? (int?)request.TimeoutSeconds
                    : null
            };

            foreach (KeyValuePair<string, string> header in headers)
            {
                if (string.IsNullOrWhiteSpace(header.Key)
                    || string.Equals(header.Key, "Authorization", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                apiRequest.Headers[header.Key.Trim()] = header.Value;
            }

            return apiRequest;
        }

        public static ApiAssetBundleRequestOptions CreateAssetBundleOptions(ObjectLoadRequest request)
        {
            return new ApiAssetBundleRequestOptions
            {
                Crc = request != null ? request.Crc : 0,
                CacheMode = MapCacheMode(request != null ? request.CacheMode : ObjectLoadCacheMode.Default),
                CacheKey = request != null ? request.CacheKey : null,
                CacheHash = request != null ? request.CacheHash : null,
                CacheVersion = request != null ? request.CacheVersion : null
            };
        }

        public static string DescribeCacheStatus(ObjectLoadRequest request)
        {
            ObjectLoadCacheMode cacheMode = request != null ? request.CacheMode : ObjectLoadCacheMode.Default;
            if (cacheMode == ObjectLoadCacheMode.Disabled)
            {
                return "disabled";
            }

#if UNITY_WEBGL && !UNITY_EDITOR
            return "webgl-browser-cache";
#else
            Hash128 hash;
            if (ObjectLoadTransportUtility.TryParseCacheHash(
                    request != null ? request.CacheHash : null,
                    out hash))
            {
                return request != null && !string.IsNullOrWhiteSpace(request.CacheKey)
                    ? "unity-cache-key-hash"
                    : "unity-cache-hash";
            }

            if (request != null && request.CacheVersion.HasValue)
            {
                return "unity-cache-version";
            }

            return cacheMode == ObjectLoadCacheMode.UseUnityCache
                ? "unity-cache-metadata-missing"
                : "not-configured";
#endif
        }

        public static ObjectDownloadResult MapApiResult(ApiResult<byte[]> result, string fallbackUrl = null)
        {
            if (result == null)
            {
                return ObjectDownloadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.DownloadFailed,
                    "API returned no result.",
                    fallbackUrl));
            }

            if (!result.IsSuccess)
            {
                return ObjectDownloadResult.Failure(MapError(result, fallbackUrl));
            }

            if (result.Data == null || result.Data.Length == 0)
            {
                return ObjectDownloadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.EmptyDownload,
                    "API download returned no bytes.",
                    result.RequestUrl ?? fallbackUrl,
                    result.HttpStatusCode));
            }

            return ObjectDownloadResult.Success(
                result.Data,
                result.HttpStatusCode ?? 0,
                new Dictionary<string, string>());
        }

        public static ObjectLoadError MapError(ApiResult<byte[]> result, string fallbackUrl = null)
        {
            return MapError<byte[]>(result, fallbackUrl);
        }

        public static ObjectLoadError MapError<TResponse>(ApiResult<TResponse> result, string fallbackUrl = null)
        {
            ApiError error = result != null ? result.Error : null;
            ObjectLoadErrorCode code = error != null && error.IsCancellation
                ? ObjectLoadErrorCode.Canceled
                : ObjectLoadErrorCode.DownloadFailed;

            string message = error != null && !string.IsNullOrWhiteSpace(error.Message)
                ? error.Message
                : "API download failed.";

            string requestUrl = error != null && !string.IsNullOrWhiteSpace(error.RequestUrl)
                ? error.RequestUrl
                : result != null && !string.IsNullOrWhiteSpace(result.RequestUrl)
                    ? result.RequestUrl
                    : fallbackUrl;

            long? statusCode = error != null && error.HttpStatusCode.HasValue
                ? error.HttpStatusCode
                : result != null
                    ? result.HttpStatusCode
                    : null;

            string exceptionMessage = error != null && error.Exception != null
                ? error.Exception.Message
                : null;

            return ObjectLoadError.Create(code, message, requestUrl, statusCode, exceptionMessage);
        }

        public static string CreateDebugSnapshotJson(ObjectSource source, ObjectLoadRequest request)
        {
            return CreateDebugSnapshotJson(
                source,
                request,
                ApiAuthenticationRequirement.Disabled);
        }

        public static string CreateDebugSnapshotJson(
            ObjectSource source,
            ObjectLoadRequest request,
            ApiAuthenticationRequirement providerAuthentication)
        {
            ApiRequest apiRequest = CreateApiRequest(
                source,
                request,
                providerAuthentication);
            Dictionary<string, string> redactedHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, string> header in apiRequest.Headers)
            {
                redactedHeaders[header.Key] = ObjectLoadTransportUtility.IsSensitiveHeader(header.Key)
                    ? RedactedValue
                    : header.Value;
            }

            ApiObjectDownloadDebugSnapshot snapshot = new ApiObjectDownloadDebugSnapshot
            {
                Endpoint = apiRequest.Endpoint,
                Method = apiRequest.Method.ToString(),
                Authentication = apiRequest.Authentication.ToString(),
                ResponseFormat = apiRequest.ResponseFormat.ToString(),
                AssetBundleOptions = apiRequest.AssetBundleOptions,
                TimeoutSeconds = apiRequest.TimeoutSeconds,
                BearerTokenOverride = string.IsNullOrWhiteSpace(apiRequest.BearerTokenOverride) ? null : RedactedValue,
                Headers = redactedHeaders
            };

            return JsonConvert.SerializeObject(snapshot, Formatting.Indented);
        }

        private static string ExtractBearerToken(Dictionary<string, string> headers)
        {
            if (headers == null)
            {
                return null;
            }

            string authorization;
            if (!headers.TryGetValue("Authorization", out authorization))
            {
                return null;
            }

            return ObjectLoadRequest.StripBearerPrefix(authorization);
        }

        private static ApiAuthenticationRequirement ResolveAuthentication(
            string bearerToken,
            ApiAuthenticationRequirement providerAuthentication)
        {
            return string.IsNullOrWhiteSpace(bearerToken)
                ? providerAuthentication
                : ApiAuthenticationRequirement.Required;
        }

        private static ApiAssetBundleCacheMode MapCacheMode(ObjectLoadCacheMode cacheMode)
        {
            switch (cacheMode)
            {
                case ObjectLoadCacheMode.Disabled:
                    return ApiAssetBundleCacheMode.Disabled;
                case ObjectLoadCacheMode.UseUnityCache:
                    return ApiAssetBundleCacheMode.UseUnityCache;
                case ObjectLoadCacheMode.Default:
                default:
                    return ApiAssetBundleCacheMode.Default;
            }
        }

    }

    public sealed class ApiObjectDownloadDebugSnapshot
    {
        [JsonProperty("endpoint")]
        public string Endpoint { get; set; }

        [JsonProperty("method")]
        public string Method { get; set; }

        [JsonProperty("authentication")]
        public string Authentication { get; set; }

        [JsonProperty("response_format")]
        public string ResponseFormat { get; set; }

        [JsonProperty("asset_bundle_options")]
        public ApiAssetBundleRequestOptions AssetBundleOptions { get; set; }

        [JsonProperty("timeout_seconds")]
        public int? TimeoutSeconds { get; set; }

        [JsonProperty("bearer_token_override")]
        public string BearerTokenOverride { get; set; }

        [JsonProperty("headers")]
        public Dictionary<string, string> Headers { get; set; }
    }
}
