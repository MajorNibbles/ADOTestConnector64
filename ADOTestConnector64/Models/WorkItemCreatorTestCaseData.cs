using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AzureDevOpsTestConnector.DTOs
{
    public class WorkItemCreatorTestCaseData
    {
        public string PatCode { get; set; }
        public string AzureDevopsBaseUrl { get; set; }
        public string ProjectName { get; set; }
        public bool UpdateTestCaseAssociation { get; set; }
        public string currentNameSpace { get; set; }
        public string currentSolutionDllName { get; set; }
        public string ReadableTestCaseName { get; set; }
        public string TestCaseMethodName { get; set; }
        public int testPlanId { get; set; }
        public int testSuiteId { get; set; }
        public string TestCaseReference { get; set; }
        public List<string> TestSteps { get; set; }
        public bool UpdateTestCaseName { get; internal set; }
    }
}
