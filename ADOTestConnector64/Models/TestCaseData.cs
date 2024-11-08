using System.Collections.Generic;

namespace AzureDevOpsTestConnector.DTOs
{
    class TestCaseData
    {
        public string TestCaseName { get; set; }
        public string ReadableTestCaseName { get; set; }
        public int TestCaseSignatureIndex { get; set; }
        public string TestCaseId { get; set; }
        public int ExistingTestCaseReferenceIndex { get; set; }
        public string WhiteSpace { get; set; }
        public List<string> TestSteps { get; set; }
        public string SpecflowParams { get; set; }
    }
}
