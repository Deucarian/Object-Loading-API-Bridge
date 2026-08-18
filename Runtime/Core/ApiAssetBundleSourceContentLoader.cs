using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Deucarian.API.Core;
using Deucarian.API.Models;
using Deucarian.ObjectLoading;
using UnityEngine;

namespace Deucarian.ObjectLoading.APIIntegration
{
    public sealed class ApiAssetBundleSourceContentLoader : IObjectSourceContentLoader
    {
        public const string LoadStrategy = "api-unitywebrequest-assetbundle";

        private readonly IApiClient _apiClient;
        private readonly IObjectSourceContentLoader _fallbackContentLoader;
        private readonly ApiAuthenticationRequirement _providerAuthentication;

        public ApiAssetBundleSourceContentLoader(IApiClient apiClient)
            : this(
                apiClient,
                new SourceAssetBundleContentLoader(),
                ApiAuthenticationRequirement.Disabled)
        {
        }

        public ApiAssetBundleSourceContentLoader(
            IApiClient apiClient,
            ApiAuthenticationRequirement providerAuthentication)
            : this(
                apiClient,
                new SourceAssetBundleContentLoader(),
                providerAuthentication)
        {
        }

        public ApiAssetBundleSourceContentLoader(IApiClient apiClient,
                                                 IObjectSourceContentLoader fallbackContentLoader)
            : this(
                apiClient,
                fallbackContentLoader,
                ApiAuthenticationRequirement.Disabled)
        {
        }

        public ApiAssetBundleSourceContentLoader(
            IApiClient apiClient,
            IObjectSourceContentLoader fallbackContentLoader,
            ApiAuthenticationRequirement providerAuthentication)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _fallbackContentLoader = fallbackContentLoader ?? new SourceAssetBundleContentLoader();
            _providerAuthentication = providerAuthentication;
        }

        public IEnumerator LoadAsync(ObjectSource source,
                                     ObjectLoadRequest request,
                                     Action<ObjectContentLoadResult> onCompleted)
        {
            if (source == null)
            {
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.InvalidRequest,
                    "Object source is missing.")));
                yield break;
            }

            switch (source.Type)
            {
                case ObjectSourceType.DirectUrl:
                    yield return LoadDirectUrl(source, request, onCompleted);
                    yield break;
                case ObjectSourceType.LocalFile:
                case ObjectSourceType.RawBytes:
                    yield return _fallbackContentLoader.LoadAsync(source, request, onCompleted);
                    yield break;
                default:
                    onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.SourceResolutionFailed,
                        "Unsupported object source type: " + source.Type + ".")));
                    yield break;
            }
        }

        private IEnumerator LoadDirectUrl(ObjectSource source,
                                          ObjectLoadRequest request,
                                          Action<ObjectContentLoadResult> onCompleted)
        {
            ObjectLoadError validationError = ApiObjectDownloadMapper.Validate(source);
            if (validationError != null)
            {
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(validationError));
                yield break;
            }

            ObjectLoadTelemetry telemetry = CreateTelemetry(request);
            ApiRequest apiRequest = ApiObjectDownloadMapper.CreateAssetBundleApiRequest(
                source,
                request,
                _providerAuthentication);
            apiRequest.TransferProgress = progress => ReportTransferProgress(request, telemetry, progress);

            request?.ReportProgress(ObjectLoadPhase.Downloading, 0f, "Downloading AssetBundle through API.", 0, 0, telemetry);

            CancellationToken cancellationToken = request != null ? request.CancellationToken : default;
            Stopwatch downloadTimer = Stopwatch.StartNew();
            Task<ApiResult<AssetBundle>> task = _apiClient.SendAsync<AssetBundle>(apiRequest, cancellationToken);
            while (!task.IsCompleted)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    downloadTimer.Stop();
                    telemetry.DownloadTimeMs = downloadTimer.ElapsedMilliseconds;
                    request?.ReportProgress(
                        ObjectLoadPhase.Failed,
                        1f,
                        "API AssetBundle download was canceled.",
                        telemetry.BytesReceived,
                        0,
                        telemetry);
                    onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.Canceled,
                        "API AssetBundle download was canceled.",
                        source.Url)));
                    yield break;
                }

                yield return null;
            }

            downloadTimer.Stop();
            telemetry.DownloadTimeMs = downloadTimer.ElapsedMilliseconds;

            if (task.IsCanceled)
            {
                ReportFailure(request, telemetry, onCompleted, ObjectLoadError.Create(
                    ObjectLoadErrorCode.Canceled,
                    "API AssetBundle download was canceled.",
                    source.Url));
                yield break;
            }

            if (task.IsFaulted)
            {
                Exception exception = task.Exception != null ? task.Exception.GetBaseException() : null;
                ReportFailure(request, telemetry, onCompleted, ObjectLoadError.Create(
                    ObjectLoadErrorCode.DownloadFailed,
                    "API AssetBundle download failed: " + (exception != null ? exception.Message : "Unknown error."),
                    source.Url,
                    null,
                    exception != null ? exception.Message : null));
                yield break;
            }

            ApiResult<AssetBundle> apiResult = task.Result;
            if (apiResult == null || !apiResult.IsSuccess)
            {
                ReportFailure(
                    request,
                    telemetry,
                    onCompleted,
                    ApiObjectDownloadMapper.MapError(apiResult, source.Url));
                yield break;
            }

            AssetBundle bundle = apiResult.Data;
            request?.ReportProgress(
                ObjectLoadPhase.Downloading,
                1f,
                "AssetBundle downloaded through API.",
                telemetry.BytesReceived,
                0,
                telemetry);
            request?.ReportProgress(
                ObjectLoadPhase.LoadingBundle,
                1f,
                "AssetBundle loaded by UnityWebRequestAssetBundle.",
                telemetry.BytesReceived,
                0,
                telemetry);

            CompleteBundleLoad(bundle, apiResult.RequestUrl ?? source.Url, telemetry, request, onCompleted);
        }

        private static void ReportTransferProgress(ObjectLoadRequest request,
                                                   ObjectLoadTelemetry telemetry,
                                                   ApiTransferProgress progress)
        {
            if (telemetry == null || progress == null)
            {
                return;
            }

            telemetry.BytesReceived = progress.DownloadedBytes;
            request?.Progress?.Invoke(ObjectLoadProgress.Create(
                ObjectLoadPhase.Downloading,
                progress.IsDone ? 1f : progress.DownloadProgress,
                "Downloading AssetBundle through API.",
                telemetry.BytesReceived,
                0,
                telemetry));
        }

        private static void CompleteBundleLoad(AssetBundle bundle,
                                               string sourceDescription,
                                               ObjectLoadTelemetry telemetry,
                                               ObjectLoadRequest request,
                                               Action<ObjectContentLoadResult> onCompleted)
        {
            if (bundle == null)
            {
                ReportFailure(request, telemetry, onCompleted, ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "AssetBundle could not be loaded through API. Check that the source serves a Unity AssetBundle for the active platform.",
                    sourceDescription));
                return;
            }

            request?.ReportProgress(ObjectLoadPhase.DiscoveringContent, 0f, "Discovering AssetBundle content.", telemetry.BytesReceived, 0, telemetry);
            string[] assetNames = bundle.GetAllAssetNames() ?? new string[0];
            string[] scenePaths = bundle.GetAllScenePaths() ?? new string[0];
            telemetry.AssetCount = assetNames.Length;
            telemetry.SceneCount = scenePaths.Length;

            request?.ReportProgress(ObjectLoadPhase.DiscoveringContent, 1f, "AssetBundle content is ready.", telemetry.BytesReceived, 0, telemetry);
            onCompleted?.Invoke(ObjectContentLoadResult.Success(
                new AssetBundleContent(bundle, assetNames, scenePaths),
                telemetry));
        }

        private static void ReportFailure(ObjectLoadRequest request,
                                          ObjectLoadTelemetry telemetry,
                                          Action<ObjectContentLoadResult> onCompleted,
                                          ObjectLoadError error)
        {
            request?.ReportProgress(
                ObjectLoadPhase.Failed,
                1f,
                error != null ? error.Message : "API AssetBundle loading failed.",
                telemetry != null ? telemetry.BytesReceived : 0,
                0,
                telemetry);
            onCompleted?.Invoke(ObjectContentLoadResult.Failure(
                error ?? ObjectLoadError.Create(ObjectLoadErrorCode.Unknown, "API AssetBundle loading failed.")));
        }

        private static ObjectLoadTelemetry CreateTelemetry(ObjectLoadRequest request)
        {
            return new ObjectLoadTelemetry
            {
                LoadStrategy = LoadStrategy,
                CacheMode = request != null ? request.CacheMode : ObjectLoadCacheMode.Default,
                CacheKey = request != null ? request.CacheKey : null,
                CacheHash = request != null ? request.CacheHash : null,
                CacheVersion = request != null ? request.CacheVersion : null,
                Crc = request != null ? request.Crc : 0,
                CacheStatus = ApiObjectDownloadMapper.DescribeCacheStatus(request)
            };
        }
    }
}
