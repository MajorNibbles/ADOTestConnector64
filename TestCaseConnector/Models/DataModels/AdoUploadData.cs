using TestWizard;

namespace AzureDevOpsTestConnector.Models
{
    public class AdoUploadData
    {
        public AdoUploadData(TestWizardPackage options)
        {
            CurrentProjectName = options.ProjectName;
            CurrentSolutionDllName = options.AssociationDllName;
            TestPlanId = options.TestPlanId;
            CurrentNameSpace = "";
        }

        public string CurrentProjectName { get; internal set; }
        public string CurrentSolutionDllName { get; internal set; }
        public string CurrentNameSpace { get; internal set; }
        public int TestPlanId { get; internal set; }
        public int TestSuiteId { get; internal set; }
        public int ParentUserStoryId { get; set; }
    }
}
