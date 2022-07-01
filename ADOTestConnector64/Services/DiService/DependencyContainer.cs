using AzureDevOpsTestConnector.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using RestSharp;

namespace AzureDevOpsTestConnector.Services.DiService
{
    public static class DependencyContainer
    {
        public static readonly ServiceProvider ServiceProvider = new ServiceCollection()
                
                .AddTransient<IApiCaller, ApiCaller>()
                .AddTransient<IRestClient, RestClient>()
                .AddTransient<IRestRequest, RestRequest>()
                .AddTransient<IBase64Encoder, Base64Encoder>()
                .AddTransient<IAzureWorkItemCreator, AzureWorkItemCreator>()
                .BuildServiceProvider();
    }
}
