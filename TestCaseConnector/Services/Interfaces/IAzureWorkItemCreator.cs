using AzureDevOpsTestConnector.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureDevOpsTestConnector.Services.Interfaces
{
    public interface IAzureWorkItemCreator
    {
        int CreateNewTestPlan(string patToken, string azureDevOpsUrl, string projectName, string testPlanName);

        int CreateNewTestSuite(string patToken, string azureDevOpsUrl, string projectName, string testSuiteName,
            int testPlanId, string testSuiteReqLinkIds);

        string CreateOrUpdateTestCase(WorkItemCreatorTestCaseData wICTestData);
    }
}
