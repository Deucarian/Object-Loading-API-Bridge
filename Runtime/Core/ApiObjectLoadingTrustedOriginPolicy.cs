using System;
using System.Collections.Generic;
using Deucarian.API.Models;

namespace Deucarian.ObjectLoading.APIIntegration
{
    /// <summary>
    /// Resolves authenticated object URLs only when their destination is tied to
    /// the configured API base or an explicitly trusted exact HTTP(S) origin.
    /// </summary>
    public sealed class ApiObjectLoadingTrustedOriginPolicy
    {
        private readonly Uri apiBaseUri;
        private readonly Uri relativeResolutionBaseUri;
        private readonly IReadOnlyList<Uri> additionalTrustedOrigins;

        public ApiObjectLoadingTrustedOriginPolicy(string apiBaseUrl)
            : this(apiBaseUrl, null)
        {
        }

        public ApiObjectLoadingTrustedOriginPolicy(
            string apiBaseUrl,
            IEnumerable<string> exactTrustedOrigins)
        {
            if (!string.IsNullOrWhiteSpace(apiBaseUrl))
            {
                if (!TryGetApiBaseUri(apiBaseUrl, out Uri parsedApiBaseUri))
                {
                    throw new ArgumentException(
                        "The API base URL must be an absolute HTTP(S) URL without user information, a query, or a fragment.",
                        nameof(apiBaseUrl));
                }

                apiBaseUri = parsedApiBaseUri;
                relativeResolutionBaseUri = new Uri(
                    apiBaseUri.AbsoluteUri.TrimEnd('/') + "/",
                    UriKind.Absolute);
            }

            var trustedOrigins = new List<Uri>();
            if (exactTrustedOrigins != null)
            {
                foreach (string origin in exactTrustedOrigins)
                {
                    if (!TryGetExactOrigin(origin, out Uri trustedOrigin))
                    {
                        throw new ArgumentException(
                            "Every trusted origin must be an exact HTTP(S) origin without user information, a path, a query, or a fragment.",
                            nameof(exactTrustedOrigins));
                    }

                    if (!ContainsOrigin(trustedOrigins, trustedOrigin))
                    {
                        trustedOrigins.Add(trustedOrigin);
                    }
                }
            }

            additionalTrustedOrigins = trustedOrigins;
        }

        /// <summary>
        /// Resolves a source to a canonical absolute URL. Trusted destinations
        /// use optional provider authentication; an absolute untrusted HTTP(S)
        /// destination remains an explicitly anonymous public request.
        /// </summary>
        public bool TryResolveProviderOptionalRequest(
            string sourceUrl,
            out ApiObjectLoadingRequestResolution request,
            out string issue)
        {
            request = null;
            if (!TryResolveCandidate(
                    sourceUrl,
                    out Uri resolvedUri,
                    out bool isTrustedOrigin,
                    out issue))
            {
                return false;
            }

            request = new ApiObjectLoadingRequestResolution(
                resolvedUri.AbsoluteUri,
                isTrustedOrigin
                    ? ApiAuthenticationRequirement.Optional
                    : ApiAuthenticationRequirement.Disabled);
            return true;
        }

        /// <summary>
        /// Resolves a provider-optional request or throws when the source is not
        /// a valid HTTP(S), API-relative, or public absolute model URL.
        /// </summary>
        public ApiObjectLoadingRequestResolution
            ResolveProviderOptionalRequest(string sourceUrl)
        {
            if (TryResolveProviderOptionalRequest(
                    sourceUrl,
                    out ApiObjectLoadingRequestResolution request,
                    out string issue))
            {
                return request;
            }

            throw new InvalidOperationException(issue);
        }

        /// <summary>
        /// Resolves a source to a canonical absolute URL and Required provider
        /// authentication, or returns false without producing a request. This
        /// method never converts an untrusted destination to an anonymous one.
        /// </summary>
        public bool TryResolveRequiredRequest(
            string sourceUrl,
            out ApiObjectLoadingRequestResolution request,
            out string issue)
        {
            request = null;
            if (!TryResolveCandidate(
                    sourceUrl,
                    out Uri resolvedUri,
                    out bool isTrustedOrigin,
                    out issue))
            {
                return false;
            }

            if (!isTrustedOrigin)
            {
                issue = "The object URL origin is not trusted for required authentication.";
                return false;
            }

            request = new ApiObjectLoadingRequestResolution(
                resolvedUri.AbsoluteUri,
                ApiAuthenticationRequirement.Required);
            return true;
        }

        /// <summary>
        /// Resolves a required-authentication request or throws when the source
        /// is invalid or its exact origin is not trusted. It never downgrades the
        /// request to optional or anonymous authentication.
        /// </summary>
        public ApiObjectLoadingRequestResolution ResolveRequiredRequest(
            string sourceUrl)
        {
            if (TryResolveRequiredRequest(
                    sourceUrl,
                    out ApiObjectLoadingRequestResolution request,
                    out string issue))
            {
                return request;
            }

            throw new InvalidOperationException(issue);
        }

        private bool TryResolveCandidate(
            string sourceUrl,
            out Uri resolvedUri,
            out bool isTrustedOrigin,
            out string issue)
        {
            resolvedUri = null;
            isTrustedOrigin = false;

            if (string.IsNullOrWhiteSpace(sourceUrl))
            {
                issue = "An object source URL is required.";
                return false;
            }

            string candidate = sourceUrl.Trim();
            if (candidate.IndexOf('\\') >= 0 ||
                !Uri.TryCreate(
                    candidate,
                    UriKind.RelativeOrAbsolute,
                    out Uri candidateUri))
            {
                issue = "The object source URL is invalid.";
                return false;
            }

            if (!candidateUri.IsAbsoluteUri)
            {
                if (IsNetworkPathReference(candidate))
                {
                    issue = "Network-path object URLs are not trusted relative URLs.";
                    return false;
                }

                if (relativeResolutionBaseUri == null)
                {
                    issue = "A relative object URL requires a trusted API base URL.";
                    return false;
                }

                string relativeEndpoint = candidate.TrimStart('/');
                if (!Uri.TryCreate(
                        relativeResolutionBaseUri,
                        relativeEndpoint,
                        out resolvedUri) ||
                    !HasSameOrigin(resolvedUri, apiBaseUri))
                {
                    issue = "The relative object URL could not be resolved against the trusted API base URL.";
                    return false;
                }

                isTrustedOrigin = true;
            }
            else
            {
                resolvedUri = candidateUri;
            }

            if (!IsHttp(resolvedUri) ||
                string.IsNullOrWhiteSpace(resolvedUri.IdnHost))
            {
                issue = "Object URLs must use HTTP or HTTPS.";
                return false;
            }

            if (!string.IsNullOrEmpty(resolvedUri.UserInfo))
            {
                issue = "Object URLs must not contain user information.";
                return false;
            }

            isTrustedOrigin = isTrustedOrigin || IsTrustedOrigin(resolvedUri);
            issue = string.Empty;
            return true;
        }

        private bool IsTrustedOrigin(Uri sourceUri)
        {
            if (apiBaseUri != null && HasSameOrigin(sourceUri, apiBaseUri))
            {
                return true;
            }

            foreach (Uri trustedOrigin in additionalTrustedOrigins)
            {
                if (HasSameOrigin(sourceUri, trustedOrigin))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryGetApiBaseUri(string value, out Uri uri)
        {
            if (!TryGetAbsoluteHttpUri(value, out uri))
            {
                return false;
            }

            return string.IsNullOrEmpty(uri.UserInfo) &&
                   string.IsNullOrEmpty(uri.Query) &&
                   string.IsNullOrEmpty(uri.Fragment);
        }

        private static bool TryGetExactOrigin(string value, out Uri uri)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !TryGetAbsoluteHttpUri(value.Trim(), out uri))
            {
                return false;
            }

            return uri.AbsolutePath == "/" &&
                   string.IsNullOrEmpty(uri.UserInfo) &&
                   string.IsNullOrEmpty(uri.Query) &&
                   string.IsNullOrEmpty(uri.Fragment);
        }

        private static bool TryGetAbsoluteHttpUri(string value, out Uri uri)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                   IsHttp(uri) &&
                   !string.IsNullOrWhiteSpace(uri.IdnHost);
        }

        private static bool IsHttp(Uri uri)
        {
            return uri != null &&
                   (string.Equals(
                        uri.Scheme,
                        Uri.UriSchemeHttp,
                        StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(
                        uri.Scheme,
                        Uri.UriSchemeHttps,
                        StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasSameOrigin(Uri left, Uri right)
        {
            return left != null &&
                   right != null &&
                   string.Equals(
                       left.Scheme,
                       right.Scheme,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       left.IdnHost,
                       right.IdnHost,
                       StringComparison.OrdinalIgnoreCase) &&
                   left.Port == right.Port;
        }

        private static bool ContainsOrigin(
            IEnumerable<Uri> origins,
            Uri candidate)
        {
            foreach (Uri origin in origins)
            {
                if (HasSameOrigin(origin, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsNetworkPathReference(string candidate)
        {
            return candidate.Length >= 2 &&
                   candidate[0] == '/' &&
                   candidate[1] == '/';
        }
    }

    /// <summary>
    /// A canonical object URL paired with the only authentication decision
    /// produced by the policy path that resolved it.
    /// </summary>
    public sealed class ApiObjectLoadingRequestResolution
    {
        internal ApiObjectLoadingRequestResolution(
            string resolvedUrl,
            ApiAuthenticationRequirement authentication)
        {
            ResolvedUrl = resolvedUrl;
            Authentication = authentication;
        }

        public string ResolvedUrl { get; }

        public ApiAuthenticationRequirement Authentication { get; }
    }
}
