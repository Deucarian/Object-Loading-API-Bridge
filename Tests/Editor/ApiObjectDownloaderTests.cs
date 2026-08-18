using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Deucarian.API.Models;
using Deucarian.ObjectLoading;

namespace Deucarian.ObjectLoading.APIIntegration.Tests
{
    public sealed class ApiObjectDownloaderTests
    {
        [Test]
        public void DownloadAsync_UsesApiClientAndMapsResult()
        {
            RecordingApiClient client = new RecordingApiClient
            {
                NextBytesResult = ApiResult<byte[]>.Success(
                    new byte[] { 9, 8, 7 },
                    Deucarian.API.HttpMethod.GET,
                    200,
                    "https://example.com/object.bundle",
                    null)
            };
            ApiObjectDownloader downloader = new ApiObjectDownloader(client);
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl("https://example.com/object.bundle");
            request.BearerToken = "token";
            ObjectDownloadResult result = null;

            Run(downloader.DownloadAsync(
                ObjectSource.DirectUrl(request.Url),
                request,
                value => result = value));

            Assert.NotNull(client.LastRequest);
            Assert.AreEqual("token", client.LastRequest.BearerTokenOverride);
            Assert.NotNull(result);
            Assert.True(result.Succeeded);
            Assert.AreEqual(3, result.Bytes.Length);
        }

        [Test]
        public void DownloadAsync_ForwardsProviderAuthenticationWithoutTokenOverride()
        {
            RecordingApiClient client = new RecordingApiClient
            {
                NextBytesResult = ApiResult<byte[]>.Success(
                    new byte[] { 1 },
                    Deucarian.API.HttpMethod.GET,
                    200,
                    "https://example.com/object.bundle",
                    null)
            };
            ApiObjectDownloader downloader = new ApiObjectDownloader(
                client,
                ApiAuthenticationRequirement.Required);
            ObjectLoadRequest request = ObjectLoadRequest.FromUrl(
                "https://example.com/object.bundle");

            Run(downloader.DownloadAsync(
                ObjectSource.DirectUrl(request.Url),
                request,
                _ => { }));

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
            }
        }
    }
}
