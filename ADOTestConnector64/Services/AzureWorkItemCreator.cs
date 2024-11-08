using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AzureDevOpsTestConnector.DTOs;
using AzureDevOpsTestConnector.Services.Interfaces;

namespace AzureDevOpsTestConnector.Services
{
    public class AzureWorkItemCreator : IAzureWorkItemCreator
    {
        private readonly IBase64Encoder _encoder;
        private readonly IApiCaller _apiCaller;

        public AzureWorkItemCreator(IBase64Encoder encoder, IApiCaller apiCaller)
        {
            _encoder = encoder;
            _apiCaller = apiCaller;
        }

        public string CreateOrUpdateTestCase(WorkItemCreatorTestCaseData wICTestData)
        {

            if (string.IsNullOrEmpty(wICTestData.TestCaseReference))
            {
                wICTestData.TestCaseReference = CreateTestCase(wICTestData);
            }
            else
            {
                wICTestData.TestCaseReference = UpdateTestCase(wICTestData);
            }

            AddTestCaseToTestSuite(wICTestData);
            return wICTestData.TestCaseReference;
        }


        public int CreateNewTestPlan(string patToken, string azureDevOpsUrl, string projectName, string testPlanName)
        {
            var hashedToken = _encoder.EncodeString($"{patToken}");

            _apiCaller.SetUpClient($"{azureDevOpsUrl}/{projectName}/_apis/testplan/plans?api-version=5.1-preview.1");

            _apiCaller.SetUpRequestHeaders(new Dictionary<string, string>
            {
                {"Authorization",$"Basic {hashedToken}" }
            });

            _apiCaller.AddRequestBodyText($"{{\"name\": \"{testPlanName}\"}}");

            var result = _apiCaller.PerformApiPostRequest();

            if (!result.IsSuccessful)
            {
                throw new Exception($"Unable to create new Test Plan, please check your settings{Environment.NewLine}Status Code:{result.StatusCode}(Hint: if 203 check your PAT settings!){Environment.NewLine}Content Message:{result.Content}");
            }

            return int.Parse(ExtractIdFromJsonResponse(result.Content));
        }

        public int CreateNewTestSuite(string patToken, string azureDevOpsUrl, string projectName, string testSuiteName, int testPlanId, string testSuiteReqLinkIds)
        {
            var hashedToken = _encoder.EncodeString($"{patToken}");
            //get root suite id from testPlanId

            _apiCaller.SetUpClient($"{azureDevOpsUrl}/{projectName}/_apis/testplan/Plans/{testPlanId}?api-version=5.1-preview.1");

            _apiCaller.SetUpRequestHeaders(new Dictionary<string, string>
            {
                {"Authorization",$"Basic {hashedToken}" }
            });

            var rootSuiteResult = _apiCaller.PerformApiGetRequest();
            int rootSuiteId = ExtractRootSuiteIdFromJsonResponse(rootSuiteResult.Content);


            _apiCaller.SetUpClient($"{azureDevOpsUrl}/{projectName}/_apis/testplan/Plans/{testPlanId}/suites?api-version=5.1-preview.1");

            _apiCaller.SetUpRequestHeaders(new Dictionary<string, string>
            {
                {"Authorization",$"Basic {hashedToken}" }
            });

            var testSuiteType = string.IsNullOrEmpty(testSuiteReqLinkIds) ? "staticTestSuite" : "requirementTestSuite";
            var requirementJsonEntry = string.IsNullOrEmpty(testSuiteReqLinkIds) ? "" : ($"\"requirementId\": {testSuiteReqLinkIds},");

            _apiCaller.AddRequestBodyText($"{{\"suiteType\": \"{testSuiteType}\",{requirementJsonEntry}\"name\": \"{testSuiteName}\",\"parentSuite\": {{\"id\": {rootSuiteId}}}}}");

            var result = _apiCaller.PerformApiPostRequest();

            if (!result.IsSuccessful)
            {
                throw new Exception($"Unable to create new Test Suite, please check your settings{Environment.NewLine}Status Code:{result.StatusCode}(Hint: if 203 check your PAT settings!){Environment.NewLine}Content Message:{result.Content}");
            }

            return int.Parse(ExtractIdFromJsonResponse(result.Content));
        }


        private string CreateTestCase(WorkItemCreatorTestCaseData wICTestData)
        {
            var hashedToken = _encoder.EncodeString($"{wICTestData.PatCode}");
            wICTestData.ReadableTestCaseName = EscapeJsonCharacters(wICTestData.ReadableTestCaseName);

            _apiCaller.SetUpClient($"{wICTestData.AzureDevopsBaseUrl}/{wICTestData.ProjectName}/_apis/wit/workitems/$Test%20Case?api-version=5.1");

            _apiCaller.SetUpRequestHeaders(new Dictionary<string, string>
            {
                {"Authorization",$"Basic {hashedToken}"},
                {"Content-Type", "application/json-patch+json"}
            });

            var methodName = Regex.Replace(wICTestData.TestCaseMethodName, @"(^\w)|(\s\w)|(\-\w)", m => m.Value.ToUpper()); //replace first letters with capitals

            if (!String.IsNullOrEmpty(wICTestData.SpecflowParams)) methodName = methodName.Replace(" ", "") + wICTestData.SpecflowParams;
            else methodName = methodName.Replace(" ", "");

            string associationBodyText = "";
            if (wICTestData.UpdateTestCaseAssociation)
                associationBodyText = string.IsNullOrEmpty(wICTestData.currentSolutionDllName) ? "" :
                $"{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomatedTestName\",\"value\": \"{EscapeJsonCharacters(wICTestData.currentNameSpace)}.{EscapeJsonCharacters(methodName.Replace("-", "_"))}\"}},{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomatedTestStorage\",\"value\": \"{EscapeJsonCharacters(wICTestData.currentSolutionDllName)}\"}},{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomatedTestId\",\"value\": \"{Guid.NewGuid()}\"}},{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomatedTestType\",\"value\": \"Unit Test\"}},{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomationStatus\",\"value\": \"Automated\"}},";

            var testCaseTitleCorrectLength = wICTestData.ReadableTestCaseName.Length > 128 ? EscapeJsonCharacters(wICTestData.ReadableTestCaseName).Substring(0, 128) : EscapeJsonCharacters(wICTestData.ReadableTestCaseName);
            _apiCaller.AddRequestBodyText($"[{associationBodyText}{{\"op\": \"add\",\"path\": \"/fields/System.Title\",\"from\": null,\"value\": \"{testCaseTitleCorrectLength}\"}}]");

            var result = _apiCaller.PerformApiPostRequest();

            if (!result.IsSuccessful)
            {
                throw new Exception($"Unable to create new Test Case, please check your settings{Environment.NewLine}Status Code:{result.StatusCode}(Hint: if 203 check your PAT settings!){Environment.NewLine}Content Message:{result.Content}");
            }

            wICTestData.TestCaseReference = ExtractIdFromJsonResponse(result.Content);

            //Add test steps

            if (wICTestData.TestSteps != null && wICTestData.TestSteps.Count > 0)
            {
                var requestBody = $"[{{\"op\": \"add\", \"path\": \"/fields/Microsoft.VSTS.TCM.Steps\",\"from\": null,\"value\": \"<steps id=\\\"0\\\" last=\\\"{wICTestData.TestSteps.Count}\\\">";

                for (int i = 0; i < wICTestData.TestSteps.Count; i++)
                {
                    requestBody += $"<step id=\\\"{i + 2}\\\" type=\\\"ActionStep\\\"><parameterizedString isformatted=\\\"true\\\">{EscapeJsonCharacters(wICTestData.TestSteps[i])}</parameterizedString><parameterizedString isformatted=\\\"true\\\"></parameterizedString><description/></step>";
                }

                requestBody += "</steps>\"}]";

                _apiCaller.SetUpClient($"{wICTestData.AzureDevopsBaseUrl}/{wICTestData.ProjectName}/_apis/wit/workitems/{wICTestData.TestCaseReference}?api-version=5.1");

                _apiCaller.SetUpRequestHeaders(new Dictionary<string, string>
                {
                    {"Authorization",$"Basic {hashedToken}" },
                    {"Content-Type", "application/json-patch+json"}
                });

                _apiCaller.AddRequestBodyText(requestBody);

                result = _apiCaller.PerformApiPatchRequest();

                if (!result.IsSuccessful)
                {
                    throw new Exception($"Unable to add steps to Test Case {wICTestData.ReadableTestCaseName}, please check your settings{Environment.NewLine}Status Code:{result.StatusCode}(Hint: if 203 check your PAT settings!){Environment.NewLine}Content Message:{result.Content}");
                }
            }

            return wICTestData.TestCaseReference;
        }

        

        private string UpdateTestCase(WorkItemCreatorTestCaseData wICTestData)
        {
            var hashedToken = _encoder.EncodeString($"{wICTestData.PatCode}");
            var testCaseTitle = EscapeJsonCharacters(wICTestData.ReadableTestCaseName);

            _apiCaller.SetUpClient($"{wICTestData.AzureDevopsBaseUrl}/{wICTestData.ProjectName}/_apis/wit/workitems/{wICTestData.TestCaseReference}?api-version=5.1");

            _apiCaller.SetUpRequestHeaders(new Dictionary<string, string>
            {
                {"Authorization",$"Basic {hashedToken}" },
                {"Content-Type", "application/json-patch+json"}
            });

            var methodName = Regex.Replace(wICTestData.TestCaseMethodName, @"(^\w)|(\s\w)|(\-\w)", m => m.Value.ToUpper()); //replace first letters with capitals

            if (!String.IsNullOrEmpty(wICTestData.SpecflowParams)) methodName = methodName.Replace(" ", "") + wICTestData.SpecflowParams;
            else methodName = methodName.Replace(" ", "");

            List<string> updateOperations = new List<string>();


            string testCaseTitleUpdate = "";
            if (wICTestData.UpdateTestCaseName)
            {
                var testCaseTitleCorrectLength = testCaseTitle.Length > 128 ? EscapeJsonCharacters(testCaseTitle).Substring(0, 128) : EscapeJsonCharacters(testCaseTitle);
                testCaseTitleUpdate = $"{{\"op\": \"add\",\"path\": \"/fields/System.Title\",\"from\": null,\"value\": \"{testCaseTitleCorrectLength}\"}}";
                updateOperations.Add(testCaseTitleUpdate);
            }

            string associationBodyText = "";
            if (wICTestData.UpdateTestCaseAssociation)
            {
                associationBodyText = string.IsNullOrEmpty(wICTestData.currentSolutionDllName) ? "" :
                    $"{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomatedTestName\",\"value\": \"{EscapeJsonCharacters(wICTestData.currentNameSpace)}.{EscapeJsonCharacters(methodName.Replace("-", "_"))}\"}},{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomatedTestStorage\",\"value\": \"{EscapeJsonCharacters(wICTestData.currentSolutionDllName)}\"}},{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomatedTestId\",\"value\": \"{Guid.NewGuid()}\"}},{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomatedTestType\",\"value\": \"Unit Test\"}},{{\"op\": \"add\",\"path\": \"/fields/Microsoft.VSTS.TCM.AutomationStatus\",\"value\": \"Automated\"}}";
                updateOperations.Add(associationBodyText);
            }
            

            

            _apiCaller.AddRequestBodyText($"[{string.Join(",", updateOperations)}]");

            var result = _apiCaller.PerformApiPatchRequest();

            if (!result.IsSuccessful)
            {
                throw new Exception($"Unable to update existing Test Case {testCaseTitle}, please check your settings{Environment.NewLine}Status Code:{result.StatusCode}(Hint: if 203 check your PAT settings!){Environment.NewLine}Content Message:{result.Content}");
            }

            wICTestData.TestCaseReference = ExtractIdFromJsonResponse(result.Content);

            //Update test steps

            if (wICTestData.TestSteps != null && wICTestData.TestSteps.Count > 0)
            {
                var requestBody = $"[{{\"op\": \"add\", \"path\": \"/fields/Microsoft.VSTS.TCM.Steps\",\"from\": null,\"value\": \"<steps id=\\\"0\\\" last=\\\"{wICTestData.TestSteps.Count}\\\">";

                for (int i = 0; i < wICTestData.TestSteps.Count; i++)
                {
                    requestBody += $"<step id=\\\"{i + 2}\\\" type=\\\"ActionStep\\\"><parameterizedString isformatted=\\\"true\\\">{EscapeJsonCharacters(wICTestData.TestSteps[i])}</parameterizedString><parameterizedString isformatted=\\\"true\\\"></parameterizedString><description/></step>";
                }

                requestBody += "</steps>\"}]";

                _apiCaller.SetUpClient($"{wICTestData.AzureDevopsBaseUrl}/{wICTestData.ProjectName}/_apis/wit/workitems/{wICTestData.TestCaseReference}?api-version=5.1");

                _apiCaller.SetUpRequestHeaders(new Dictionary<string, string>
                {
                    {"Authorization",$"Basic {hashedToken}" },
                    {"Content-Type", "application/json-patch+json"}
                });

                _apiCaller.AddRequestBodyText(requestBody);

                result = _apiCaller.PerformApiPatchRequest();

                if (!result.IsSuccessful)
                {
                    throw new Exception($"Unable to add steps to Test Case to {wICTestData.TestCaseReference}, please check your settings{Environment.NewLine}Status Code:{result.StatusCode}(Hint: if 203 check your PAT settings!){Environment.NewLine}Content Message:{result.Content}");
                }
            }

            return wICTestData.TestCaseReference;
        }

        private void AddTestCaseToTestSuite(WorkItemCreatorTestCaseData wICTestData)
        {
            var hashedToken = _encoder.EncodeString($"{wICTestData.PatCode}");

            _apiCaller.SetUpClient($"{wICTestData.AzureDevopsBaseUrl}/{wICTestData.ProjectName}/_apis/test/Plans/{wICTestData.testPlanId}/suites/{wICTestData.testSuiteId}/testcases/{wICTestData.TestCaseReference}?api-version=5.1");

            _apiCaller.SetUpRequestHeaders(new Dictionary<string, string>
            {
                {"Authorization",$"Basic {hashedToken}" }
            });

            var result = _apiCaller.PerformApiPostRequest();

            if (!result.IsSuccessful && !result.Content.Contains("Duplicate"))
            {
                throw new Exception($"Unable to add test cases to the test suite, please check your settings{Environment.NewLine}Status Code:{result.StatusCode}(Hint: if 203 check your PAT settings!){Environment.NewLine}Content Message:{result.Content}");
            }
        }

        private string ExtractIdFromJsonResponse(string input)
        {
            var intStr = input.Substring(6, input.IndexOf(',') - 6);
            return intStr;
        }

        private int ExtractRootSuiteIdFromJsonResponse(string input)
        {
            Regex pattern = new Regex("\"rootSuite\":{\"id\":(?<rootSuiteId>\\d+),");
            var match = pattern.Match(input);

            return int.Parse(match.Groups["rootSuiteId"].Value);
        }

        private string EscapeJsonCharacters(string input)
        {
            var returnable = input.Replace("\\", "\\\\").Replace("\"", "\\\"");
            returnable = returnable.Replace("<", "&amp;lt;").Replace(">", "&amp;gt;");
            returnable = returnable.Replace("&", "&amp;amp");
            return returnable;
        }
    }
}
