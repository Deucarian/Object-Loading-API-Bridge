using System;
using System.Collections;
using System.Threading.Tasks;
using Deucarian.API.Core;
using Deucarian.API.Models;
using Deucarian.ObjectLoading;

namespace Deucarian.ObjectLoading.APIIntegration
{
    [Obsolete("Use ApiAssetBundleSourceContentLoader or ApiObjectLoadingPipelineFactory for API-backed AssetBundle loading. ApiObjectDownloader remains for explicit byte-array workflows.")]
    public sealed class ApiObjectDownloader : IObjectDownloader
    {
        private readonly IApiClient _apiClient;
        private readonly ApiAuthenticationRequirement _providerAuthentication;

        public ApiObjectDownloader()
            : this(
                ApiClientFactory.CreateDefault(),
                ApiAuthenticationRequirement.Disabled)
        {
        }

        public ApiObjectDownloader(IApiClient apiClient)
            : this(
                apiClient,
                ApiAuthenticationRequirement.Disabled)
        {
        }

        public ApiObjectDownloader(
            IApiClient apiClient,
            ApiAuthenticationRequirement providerAuthentication)
        {
            _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
            _providerAuthentication = providerAuthentication;
        }

        public IEnumerator DownloadAsync(ObjectSource source,
                                         ObjectLoadRequest request,
                                         Action<ObjectDownloadResult> onCompleted)
        {
            ObjectLoadError validationError = ApiObjectDownloadMapper.Validate(source);
            if (validationError != null)
            {
                onCompleted?.Invoke(ObjectDownloadResult.Failure(validationError));
                yield break;
            }

            ApiRequest apiRequest = ApiObjectDownloadMapper.CreateApiRequest(
                source,
                request,
                _providerAuthentication);
            Task<ApiResult<byte[]>> task = _apiClient.SendAsync<byte[]>(
                apiRequest,
                request != null ? request.CancellationToken : default);

            while (!task.IsCompleted)
            {
                if (request != null && request.CancellationToken.IsCancellationRequested)
                {
                    onCompleted?.Invoke(ObjectDownloadResult.Failure(ObjectLoadError.Create(
                        ObjectLoadErrorCode.Canceled,
                        "API AssetBundle download was canceled.",
                        source.Url)));
                    yield break;
                }

                yield return null;
            }

            if (task.IsCanceled)
            {
                onCompleted?.Invoke(ObjectDownloadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.Canceled,
                    "API AssetBundle download was canceled.",
                    source.Url)));
                yield break;
            }

            if (task.IsFaulted)
            {
                Exception exception = task.Exception != null ? task.Exception.GetBaseException() : null;
                onCompleted?.Invoke(ObjectDownloadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.DownloadFailed,
                    "API AssetBundle download failed: " + (exception != null ? exception.Message : "Unknown error."),
                    source.Url,
                    null,
                    exception != null ? exception.Message : null)));
                yield break;
            }

            onCompleted?.Invoke(ApiObjectDownloadMapper.MapApiResult(task.Result, source.Url));
        }
    }
}
