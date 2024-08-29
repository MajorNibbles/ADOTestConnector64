﻿using System.Collections.Generic;

namespace AzureDevOpsTestConnector.DTOs
{
    public class WorkItemCreatorTestCaseData
    {
        public string PatCode { get; set; }
        public string AzureDevopsBaseUrl { get; set; }
        public string ProjectName { get; set; }
        public bool UpdateTestCaseAssociation { get; set; }
        public string CurrentNameSpace { get; set; }
        public string CurrentSolutionDllName { get; set; }
        public string ReadableTestCaseName { get; set; }
        public string TestCaseMethodName { get; set; }
        public int TestPlanId { get; set; }
        public int TestSuiteId { get; set; }
        public string TestCaseReference { get; set; }
        public List<string> TestSteps { get; set; }
        public bool UpdateTestCaseName { get; internal set; }
        public int ParentUserStoryId { get; set; }
    }
}
