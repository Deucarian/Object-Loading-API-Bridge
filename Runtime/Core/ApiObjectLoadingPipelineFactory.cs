using System;
using Deucarian.API.Core;
using Deucarian.API.Models;
using Deucarian.ObjectLoading;

namespace Deucarian.ObjectLoading.APIIntegration
{
    public static class ApiObjectLoadingPipelineFactory
    {
        public static ObjectLoadingPipeline Create(IApiClient apiClient)
        {
            return Create(
                apiClient,
                ApiAuthenticationRequirement.Disabled);
        }

        public static ObjectLoadingPipeline Create(
            IApiClient apiClient,
            ApiAuthenticationRequirement providerAuthentication)
        {
            return Create(
                apiClient,
                new DirectUrlSourceResolver(),
                new SourceAssetBundleContentLoader(),
                new AssetBundleObjectInstantiator(),
                new DefaultObjectDiagnostics(),
                providerAuthentication);
        }

        public static ObjectLoadingPipeline Create(IApiClient apiClient,
                                                   IObjectSourceResolver sourceResolver,
                                                   IObjectSourceContentLoader fallbackContentLoader,
                                                   IObjectInstantiator instantiator,
                                                   IObjectDiagnostics diagnostics)
        {
            return Create(
                apiClient,
                sourceResolver,
                fallbackContentLoader,
                instantiator,
                diagnostics,
                ApiAuthenticationRequirement.Disabled);
        }

        public static ObjectLoadingPipeline Create(
            IApiClient apiClient,
            IObjectSourceResolver sourceResolver,
            IObjectSourceContentLoader fallbackContentLoader,
            IObjectInstantiator instantiator,
            IObjectDiagnostics diagnostics,
            ApiAuthenticationRequirement providerAuthentication)
        {
            if (apiClient == null)
            {
                throw new ArgumentNullException(nameof(apiClient));
            }

            return new ObjectLoadingPipeline(
                sourceResolver ?? new DirectUrlSourceResolver(),
                new ApiAssetBundleSourceContentLoader(
                    apiClient,
                    fallbackContentLoader ?? new SourceAssetBundleContentLoader(),
                    providerAuthentication),
                instantiator ?? new AssetBundleObjectInstantiator(),
                diagnostics ?? new DefaultObjectDiagnostics());
        }
    }
}
