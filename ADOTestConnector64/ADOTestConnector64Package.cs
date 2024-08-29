using Microsoft.VisualStudio.Shell;
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using Task = System.Threading.Tasks.Task;

namespace ADOTestConnector64
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the
    /// IVsPackage interface and uses the registration attributes defined in the framework to
    /// register itself and its components with the shell. These attributes tell the pkgdef creation
    /// utility what data to put into .pkgdef file.
    /// </para>
    /// <para>
    /// To get loaded into VS, the package must be referred by &lt;Asset Type="Microsoft.VisualStudio.VsPackage" ...&gt; in .vsixmanifest file.
    /// </para>
    /// </remarks>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(ADOTestConnector64Package.PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(OptionPageGrid),
        "AzureDevops Test Connector", "Settings", 0, 0, true)]
    public sealed class ADOTestConnector64Package : AsyncPackage
    {
        /// <summary>
        /// ADOTestConnector64Package GUID string.
        /// </summary>
        public const string PackageGuidString = "9f223846-a452-42ca-afd9-85890e2c4b4f";

        #region Package Members

        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token to monitor for initialization cancellation, which can occur when VS is shutting down.</param>
        /// <param name="progress">A provider for progress updates.</param>
        /// <returns>A task representing the async work of package initialization, or an already completed task if there is none. Do not return null from this method.</returns>
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            // When initialized asynchronously, the current thread may be a background thread at this point.
            // Do any initialization that requires the UI thread after switching to the UI thread.
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);
            await FirstCommand.InitializeAsync(this);
        }

        #endregion

        public string AzureDevopsBaseUrl
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.AzureDevopsBaseUrl;
            }
        }

        public string PatCode
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return $":{page.PatToken}";
            }
        }

        public string FeatureTestPlanAttributePattern
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.FeatureTestPlanAttributePattern;
            }
        }

        public string ClassTestPlanAttributePattern
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.ClassTestPlanAttributePattern;
            }
        }

        public string FeatureTestSuiteAttributePattern
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.FeatureTestSuiteAttributePattern;
            }
        }

        public string ClassTestSuiteAttributePattern
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.ClassTestSuiteAttributePattern;
            }
        }

        public string FeatureTestCaseAttributePattern
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.FeatureTestCaseAttributePattern;
            }
        }

        public string ClassTestCaseAttributePattern
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.ClassTestCaseAttributePattern;
            }
        }

        public bool UpdateSpecFlowSteps
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.UpdateSpecFlowSteps;
            }
        }

        public bool SeperateSpecFlowExamples
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.SeparateSpecFlowExamples;
            }
        }

        public bool UpdateTestCaseAssociation
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.UpdateTestCaseAssociation;
            }
        }

        public bool UpdateTestCaseTitle
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.UpdateTestCaseTitle;
            }
        }

        public string ProjectName
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.ProjectName;
            }
            set
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                page.ProjectName = value;
                page.SaveSettingsToStorage();
            }
        }

        public int TestPlanId
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.TestPlanId;
            }
            set
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                page.TestPlanId = value;
                page.SaveSettingsToStorage();
            }
        }

        public string AssociationDllName
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.AssociationDllname;
            }
            set
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                page.AssociationDllname = value;
                page.SaveSettingsToStorage();
            }
        }

        public string CurrentNameSpace
        {
            get
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                return page.CurrentNameSpace;
            }
            set
            {
                OptionPageGrid page = (OptionPageGrid)GetDialogPage(typeof(OptionPageGrid));
                page.CurrentNameSpace = value;
                page.SaveSettingsToStorage();
            }
        }

    }

    public class OptionPageGrid : DialogPage
    {

        [Category("Integration Settings")]
        [DisplayName("AzureDevops instance URL")]
        [Description(
            "The base url of the AzureDevops instance you want to connect to. e.g. https://foo.visualstudio.com")]
        public string AzureDevopsBaseUrl { get; set; } = "https://foo.visualstudio.com";

        [Category("Integration Settings")]
        [DisplayName("Current Project name")]
        [Description(
            "The name of the project you want to add tests to, can be changed on the fly during Test Case creation")]
        public string ProjectName { get; set; } = "ProjectFoo";

        [Category("Integration Settings")]
        [DisplayName("Test Plan ID")]
        [Description(
            "The ID of the Test Plan you are using")]
        public int TestPlanId { get; set; } = -1;

        [Category("Integration Settings")]
        [DisplayName("PAT Code")]
        [Description("An authorised PAT (unencrypted) to access Azure Devops")]
        public string PatToken { get; set; } = "LargePatTokenString";

        [Category("Labelling Settings")]
        [DisplayName("Class Test Plan Attribute Pattern")]
        [Description("A Class level attribute to define an Azure Devops Test Plan Id to link the tests to. The Ado ID will replace the '~' character e.g. TestPlanReference(~)")]
        public string ClassTestPlanAttributePattern { get; set; } = "//TestPlanReference(~)";

        [Category("Labelling Settings")]
        [DisplayName("Feature Test Plan Attribute Pattern")]
        [Description("(Specflow) A Feature level attribute to define an Azure Devops Test Plan Id to link the tests to. The Ado ID will replace the '~' character e.g. TestPlanReference(~)")]
        public string FeatureTestPlanAttributePattern { get; set; } = "#TestPlanReference(~)";

        [Category("Labelling Settings")]
        [DisplayName("Class Test Suite Attribute Pattern")]
        [Description("A Class level attribute to define an Azure Devops Test Suite Id to link the tests to. The Ado ID will replace the '~' character e.g. //TestSuiteReference(~)")]
        public string ClassTestSuiteAttributePattern { get; set; } = "//TestSuiteReference(~)";

        [Category("Labelling Settings")]
        [DisplayName("Feature Test Suite Attribute Pattern")]
        [Description("(Specflow) A Feature level attribute to define an Azure Devops Test Suite Id to link the tests to. The Ado ID will replace the '~' character e.g. #TestSuiteReference(~)")]
        public string FeatureTestSuiteAttributePattern { get; set; } = "#TestSuiteReference(~)";

        [Category("Labelling Settings")]
        [DisplayName("Class Test Case Attribute Pattern")]
        [Description("A Method level attribute to define an Azure Devops Test Case Id to link the tests to. The Ado ID will replace the '~' character e.g. //TestCaseId(~)")]
        public string ClassTestCaseAttributePattern { get; set; } = "//TestCaseReference(~)";

        [Category("Labelling Settings")]
        [DisplayName("Feature Test Case Attribute Pattern")]
        [Description("(Specflow) A Method level attribute to define an Azure Devops Test Case Id to link the tests to. The Ado ID will replace the '~' character e.g. #TestCaseId(~)")]
        public string FeatureTestCaseAttributePattern { get; set; } = "#TestCaseReference(~)";

        [Category("Labelling Settings")]
        [DisplayName("Update Specflow steps in Test Cases?")]
        [Description("If true we will remove all existing Test Steps and replace them with the latest Test Steps found in your Feature file (SpecFlow Feature files only)")]
        public bool UpdateSpecFlowSteps { get; set; } = false;

        [Category("Labelling Settings")]
        [DisplayName("Separate Specflow Examples?")]
        [Description("If true we will create or update a test case in ADO for each example found. The Ids will be stored (accessible as an arg) in the test case attribute above the scenario, in strict order of the examples.")]
        public bool SeparateSpecFlowExamples { get; set; } = false;

        [Category("Labelling Settings")]
        [DisplayName("Update test case Automation Association?")]
        [Description("If true we will set the association information in the created/updated test case.")]
        public bool UpdateTestCaseAssociation { get; set; } = false;

        [Category("Labelling Settings")]
        [DisplayName("Update test case Title?")]
        [Description("If true we will set the Test Case Title to that found in the VS file.")]
        public bool UpdateTestCaseTitle { get; set; } = false;

        [Category("Storage")]
        [DisplayName("Current Association Dll Name")]
        [Description("A dll name used when associating tests (set during sync)")]
        public string AssociationDllname { get; set; } = "someProject.dll";

        [Category("Storage")]
        [DisplayName("Current Association Dll Name")]
        [Description("A namespace used when associating tests (set during sync)")]
        public string CurrentNameSpace { get; set; } = "bigLongNameSpace";
    }
}
