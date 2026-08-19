using System;
using Deucarian.API.Models;
using NUnit.Framework;

namespace Deucarian.ObjectLoading.APIIntegration.Tests
{
    public sealed class ApiObjectLoadingTrustedOriginPolicyTests
    {
        [TestCase("models/current.bundle")]
        [TestCase("/models/current.bundle")]
        public void RelativeUrlResolvesAgainstExactTrustedBase(
            string sourceUrl)
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/api/v2");

            ApiObjectLoadingRequestResolution request =
                policy.ResolveRequiredRequest(sourceUrl);

            Assert.That(
                request.ResolvedUrl,
                Is.EqualTo(
                    "https://api.example.com/api/v2/models/current.bundle"));
            Assert.That(
                request.Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Required));
        }

        [TestCase("https://api.example.com/models/current.bundle")]
        [TestCase("https://api.example.com:443/models/current.bundle")]
        public void SameOriginUsesRequiredAuthentication(string sourceUrl)
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/api/v2");

            ApiObjectLoadingRequestResolution request =
                policy.ResolveRequiredRequest(sourceUrl);

            Assert.That(
                request.Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Required));
        }

        [Test]
        public void ExplicitExactOriginAllowsRequiredAuthentication()
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/api/v2",
                new[] { "https://cdn.example.com:443" });

            ApiObjectLoadingRequestResolution request =
                policy.ResolveRequiredRequest(
                    "https://cdn.example.com/model.bundle");

            Assert.That(
                request.Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Required));
        }

        [TestCase("https://cdn.other.example/model.bundle")]
        [TestCase("http://api.example.com/model.bundle")]
        [TestCase("https://api.example.com:444/model.bundle")]
        [TestCase("https://api.example.com.evil.test/model.bundle")]
        public void UntrustedOriginIsRejectedWithoutAnonymousDowngrade(
            string sourceUrl)
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/api/v2");

            bool resolved = policy.TryResolveRequiredRequest(
                sourceUrl,
                out ApiObjectLoadingRequestResolution request,
                out string issue);

            Assert.That(resolved, Is.False);
            Assert.That(request, Is.Null);
            Assert.That(issue, Does.Contain("not trusted"));
            Assert.Throws<InvalidOperationException>(
                () => policy.ResolveRequiredRequest(sourceUrl));
        }

        [TestCase("//cdn.other.example/model.bundle")]
        [TestCase("https://user:secret@api.example.com/model.bundle")]
        [TestCase("file:///tmp/model.bundle")]
        [TestCase("https:\\api.example.com\\model.bundle")]
        public void AmbiguousOrCredentialBearingUrlIsRejected(string sourceUrl)
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/api/v2");

            Assert.That(
                policy.TryResolveRequiredRequest(
                    sourceUrl,
                    out ApiObjectLoadingRequestResolution request,
                    out _),
                Is.False);
            Assert.That(request, Is.Null);
        }

        [Test]
        public void RelativeUrlWithoutBaseIsRejected()
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                null,
                new[] { "https://cdn.example.com" });

            Assert.That(
                policy.TryResolveRequiredRequest(
                    "models/current.bundle",
                    out ApiObjectLoadingRequestResolution request,
                    out string issue),
                Is.False);
            Assert.That(request, Is.Null);
            Assert.That(issue, Does.Contain("trusted API base"));
        }

        [Test]
        public void ProviderOptionalRequestUsesOptionalAuthForTrustedOrigin()
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/api/v2");

            ApiObjectLoadingRequestResolution request =
                policy.ResolveProviderOptionalRequest(
                    "models/current.bundle");

            Assert.That(
                request.ResolvedUrl,
                Is.EqualTo(
                    "https://api.example.com/api/v2/models/current.bundle"));
            Assert.That(
                request.Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Optional));
        }

        [TestCase("https://public.example/model.bundle")]
        [TestCase("https://public.example:443/model.bundle")]
        public void ProviderOptionalRequestKeepsPublicCrossOriginAnonymous(
            string sourceUrl)
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(
                "https://api.example.com/api/v2");

            ApiObjectLoadingRequestResolution request =
                policy.ResolveProviderOptionalRequest(sourceUrl);

            Assert.That(
                request.Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Disabled));
        }

        [Test]
        public void ProviderOptionalRequestAllowsPublicAbsoluteUrlWithoutBase()
        {
            var policy = new ApiObjectLoadingTrustedOriginPolicy(null);

            ApiObjectLoadingRequestResolution request =
                policy.ResolveProviderOptionalRequest(
                    "https://public.example/model.bundle");

            Assert.That(
                request.ResolvedUrl,
                Is.EqualTo("https://public.example/model.bundle"));
            Assert.That(
                request.Authentication,
                Is.EqualTo(ApiAuthenticationRequirement.Disabled));
        }

        [TestCase("ftp://api.example.com")]
        [TestCase("https://user:secret@api.example.com")]
        [TestCase("https://api.example.com/api?environment=dev")]
        public void InvalidApiBaseIsRejected(string apiBaseUrl)
        {
            Assert.Throws<ArgumentException>(
                () => new ApiObjectLoadingTrustedOriginPolicy(apiBaseUrl));
        }

        [TestCase("https://cdn.example.com/private")]
        [TestCase("https://user:secret@cdn.example.com")]
        [TestCase("https://cdn.example.com?tenant=dev")]
        [TestCase("*.example.com")]
        [TestCase("")]
        public void InvalidExactTrustedOriginIsRejected(string origin)
        {
            Assert.Throws<ArgumentException>(
                () => new ApiObjectLoadingTrustedOriginPolicy(
                    "https://api.example.com/api/v2",
                    new[] { origin }));
        }
    }
}
