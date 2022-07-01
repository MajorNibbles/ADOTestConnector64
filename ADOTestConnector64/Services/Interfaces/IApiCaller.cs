using System.Collections.Generic;
using RestSharp;

namespace AzureDevOpsTestConnector.Services.Interfaces
{
    public interface IApiCaller
    {
        void SetUpClient(string baseUri);
        void SetUpRequestHeaders(Dictionary<string, string> headers);
        void SetUpQueryParameters(Dictionary<string, string> parameters);
        IRestResponse PerformApiGetRequest();
        IRestResponse<T> PerformApiPostRequest<T>() where T : new();
        IRestResponse PerformApiPostRequest();
        void AddRequestBodyText(string bodyText);
        IRestResponse PerformApiPatchRequest();
    }
}
