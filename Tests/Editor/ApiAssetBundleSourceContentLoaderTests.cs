using System;
using System.Collections;
using NUnit.Framework;
using Deucarian.API.Models;
using Deucarian.ObjectLoading;
using UnityEngine;

namespace Deucarian.ObjectLoading.APIIntegration.Tests
{
    public sealed class ApiAssetBundleSourceContentLoaderTests
    {
        [Test]
        public void DirectUrl_UsesApiAssetBundleResponsePath()
        {
            RecordingApiClient client = new RecordingApiClient
            {
                NextAssetBundleResult = ApiResult<AssetBundle>.Failure(
                    new ApiError
                    {
                        Message = "Denied",
                        HttpStatusCode = 403,
                        RequestUrl = "https://example.com/object.bundle"
                    },
                    Deucarian.API.HttpMethod.GET)
            };
            ApiAssetBundleSourceContentLoader loader = new ApiAssetBundleSourceContentLoader(
                client,
                new RecordingSourceContentLoader());
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.BearerToken = "token";
            ObjectContentLoadResult result = null;

            Run(loader.LoadAsync(ObjectSource.DirectUrl(request.Url), request, value => result = value));

            Assert.AreEqual(typeof(AssetBundle), client.LastResponseType);
            Assert.AreEqual(1, client.AssetBundleRequestCount);
            Assert.AreEqual(0, client.ByteArrayRequestCount);
            Assert.AreEqual(ApiResponseFormat.AssetBundle, client.LastRequest.ResponseFormat);
            Assert.AreEqual("token", client.LastRequest.BearerTokenOverride);
            Assert.NotNull(result);
            Assert.False(result.Succeeded);
            Assert.AreEqual(ObjectLoadErrorCode.DownloadFailed, result.Error.Code);
            Assert.AreEqual(403, result.Error.HttpStatusCode);
        }

        [Test]
        public void DirectUrl_ForwardsTransferProgressToObjectLoadRequest()
        {
            RecordingApiClient client = new RecordingApiClient
            {
                NextAssetBundleResult = ApiResult<AssetBundle>.Failure(
                    new ApiError { Message = "No bundle" },
                    Deucarian.API.HttpMethod.GET)
            };
            client.OnSend = request =>
                    request.TransferProgress?.Invoke(ApiTransferProgress.Create(0.5f, 1f, 1234, 0, false));
            ApiAssetBundleSourceContentLoader loader = new ApiAssetBundleSourceContentLoader(
                client,
                new RecordingSourceContentLoader());
            ObjectLoadRequest loadRequest = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            ObjectLoadProgress latestProgress = null;
            loadRequest.Progress = progress => latestProgress = progress;

            Run(loader.LoadAsync(ObjectSource.DirectUrl(loadRequest.Url), loadRequest, _ => { }));

            Assert.NotNull(latestProgress);
            Assert.AreEqual(ObjectLoadPhase.Failed, latestProgress.Phase);
            Assert.NotNull(latestProgress.Telemetry);
            Assert.AreEqual(ApiAssetBundleSourceContentLoader.LoadStrategy, latestProgress.Telemetry.LoadStrategy);
            Assert.AreEqual(1234, latestProgress.Telemetry.BytesReceived);
            Assert.AreEqual(
                ApiAuthenticationRequirement.Disabled,
                client.LastRequest.Authentication);
            Assert.IsNull(client.LastRequest.BearerTokenOverride);
        }

        [Test]
        public void DirectUrl_ForwardsProviderAuthenticationWithoutTokenOverride()
        {
            RecordingApiClient client = new RecordingApiClient
            {
                NextAssetBundleResult = ApiResult<AssetBundle>.Failure(
                    new ApiError { Message = "No bundle" },
                    Deucarian.API.HttpMethod.GET)
            };
            ApiAssetBundleSourceContentLoader loader =
                new ApiAssetBundleSourceContentLoader(
                    client,
                    new RecordingSourceContentLoader(),
                    ApiAuthenticationRequirement.Required);
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl(
                "https://example.com/object.bundle");

            Run(loader.LoadAsync(
                ObjectSource.DirectUrl(request.Url),
                request,
                _ => { }));

            Assert.AreEqual(
                ApiAuthenticationRequirement.Required,
                client.LastRequest.Authentication);
            Assert.IsNull(client.LastRequest.BearerTokenOverride);
        }

        [Test]
        public void LocalFileAndRawBytes_DelegateToFallbackLoader()
        {
            RecordingApiClient client = new RecordingApiClient();
            RecordingSourceContentLoader fallback = new RecordingSourceContentLoader();
            ApiAssetBundleSourceContentLoader loader = new ApiAssetBundleSourceContentLoader(client, fallback);

            Run(loader.LoadAsync(ObjectSource.LocalFile("C:/tmp/model.bundle"), new ObjectLoadRequest(), _ => { }));
            Run(loader.LoadAsync(ObjectSource.RawBytes(new byte[] { 1, 2, 3 }), new ObjectLoadRequest(), _ => { }));

            Assert.AreEqual(2, fallback.CallCount);
            Assert.AreEqual(ObjectSourceType.RawBytes, fallback.LastSource.Type);
            Assert.AreEqual(0, client.AssetBundleRequestCount);
            Assert.AreEqual(0, client.ByteArrayRequestCount);
        }

        [Test]
        public void PipelineFactory_ComposesApiAssetBundleContentLoader()
        {
            ObjectLoadingPipeline pipeline = ApiObjectLoadingPipelineFactory.Create(new RecordingApiClient());

            ObjectLoadingDiagnosticSnapshot snapshot = pipeline.CreateDiagnosticSnapshot();

            Assert.NotNull(snapshot.ActiveComponents);
            Assert.AreEqual("DirectUrlSourceResolver", snapshot.ActiveComponents.SourceResolver);
            Assert.AreEqual("ApiAssetBundleSourceContentLoader", snapshot.ActiveComponents.SourceContentLoader);
            Assert.IsNull(snapshot.ActiveComponents.Downloader);
            Assert.AreEqual("AssetBundleObjectInstantiator", snapshot.ActiveComponents.Instantiator);
        }

        [Test]
        public void PipelineFactory_ForwardsProviderAuthenticationWithoutTokenOverride()
        {
            RecordingApiClient client = new RecordingApiClient
            {
                NextAssetBundleResult = ApiResult<AssetBundle>.Failure(
                    new ApiError { Message = "No bundle" },
                    Deucarian.API.HttpMethod.GET)
            };
            ObjectLoadingPipeline pipeline =
                ApiObjectLoadingPipelineFactory.Create(
                    client,
                    ApiAuthenticationRequirement.Required);
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl(
                "https://example.com/object.bundle");

            Run(pipeline.LoadAsync(request, _ => { }));

            Assert.NotNull(client.LastRequest);
            Assert.AreEqual(
                ApiAuthenticationRequirement.Required,
                client.LastRequest.Authentication);
            Assert.IsNull(client.LastRequest.BearerTokenOverride);
        }

        private static void Run(IEnumerator enumerator)
        {
            while (enumerator.MoveNext())
            {
                if (enumerator.Current is IEnumerator nested)
                {
                    Run(nested);
                }
            }
        }

        private sealed class RecordingSourceContentLoader : IObjectSourceContentLoader
        {
            public int CallCount { get; private set; }
            public ObjectSource LastSource { get; private set; }

            public IEnumerator LoadAsync(ObjectSource source,
                                         ObjectLoadRequest request,
                                         Action<ObjectContentLoadResult> onCompleted)
            {
                CallCount++;
                LastSource = source;
                onCompleted?.Invoke(ObjectContentLoadResult.Failure(ObjectLoadError.Create(
                    ObjectLoadErrorCode.ContentLoadFailed,
                    "Fallback invoked.")));
                yield break;
            }
        }
    }
}
