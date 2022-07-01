using System;
using System.Collections.Generic;
using AzureDevOpsTestConnector.Services.Interfaces;
using RestSharp;

namespace AzureDevOpsTestConnector.Services
{
    public class ApiCaller : IApiCaller
    {
        private IRestClient _client;
        private IRestRequest _request;

        public ApiCaller(IRestClient client, IRestRequest request)
        {
            _client = client;
            _request = request;
        }

        public void SetUpClient(string baseUri)
        {
            _client = new RestClient();
            _request = new RestRequest();

            _client.BaseUrl = new Uri(baseUri);
        }

        public void AddRequestBodyText(string bodyText)
        {
            _request.AddParameter("application/json", bodyText, ParameterType.RequestBody);
        }

        public IRestResponse PerformApiPatchRequest()
        {
            var response = _client.Patch(_request);

            return response;
        }

        public void SetUpRequestHeaders(Dictionary<string, string> headers)
        {
            if (headers == null)
            {
                return;
            }

            foreach (var header in headers)
            {
                _request.AddHeader(header.Key, header.Value);
            }
        }

        public void SetUpQueryParameters(Dictionary<string, string> parameters)
        {
            if (parameters == null)
            {
                return;
            }

            foreach (var parameter in parameters)
            {
                _request.AddParameter(parameter.Key, parameter.Value, ParameterType.QueryString);
            }
        }

        public IRestResponse<T> PerformApiPostRequest<T>() where T : new()
        {
            return _client.Post<T>(_request);
        }

        public IRestResponse PerformApiPostRequest()
        {
            return _client.Post(_request);
        }

        public IRestResponse PerformApiGetRequest()
        {
            var response = _client.Execute(_request);

            return response;
        }
    }
}
