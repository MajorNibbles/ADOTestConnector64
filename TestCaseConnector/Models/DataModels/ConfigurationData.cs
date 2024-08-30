using TestWizard;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace AzureDevOpsTestConnector.Models
{
    public class ConfigurationData
    {
        public ConfigurationData(TestWizardPackage options)
        {
            ClassTestPlanAttributePattern = options.ClassTestPlanAttributePattern;
            FeatureTestPlanAttributePattern = options.FeatureTestPlanAttributePattern;
            ClassTestSuiteAttributePattern = options.ClassTestSuiteAttributePattern;
            FeatureTestSuiteAttributePattern = options.FeatureTestSuiteAttributePattern;
            ClassTestCaseAttributePattern = options.ClassTestCaseAttributePattern;
            FeatureTestCaseAttributePattern = options.FeatureTestCaseAttributePattern;
            ClassParentUserStoryAttributePattern = options.ClassParentUserStoryAttributePattern;
            FeatureParentUserStoryAttributePattern = options.FeatureParentUserStoryAttributePattern;

            UpdateSpecFlowSteps = options.UpdateSpecFlowSteps;
            SeparateSpecFlowExamples = options.SeperateSpecFlowExamples;
            UpdateTestCaseAssociation = options.UpdateTestCaseAssociation;
        }

        public string FeatureTestPlanAttributePattern { get; }
        public string ClassTestPlanAttributePattern { get; set; }
        public TagPattern TestPlanPattern { get; set; }

        public string FeatureTestSuiteAttributePattern { get; set; }
        public string ClassTestSuiteAttributePattern { get; set; }
        public TagPattern TestSuitePattern { get; set; }

        public string FeatureTestCaseAttributePattern { get; }
        public string ClassTestCaseAttributePattern { get; set; }
        public TagPattern TestCasePattern { get; set; }

        public string FeatureParentUserStoryAttributePattern { get; }
        public string ClassParentUserStoryAttributePattern { get; set; }
        public TagPattern ParentUserStoryPattern { get; set; }

        public bool UpdateSpecFlowSteps { get; set; }
        public bool SeparateSpecFlowExamples { get; set; }
        public bool UpdateTestCaseAssociation { get; set; }
        
    }

    public class TagPattern
    {
        public TagPattern(string pattern)
        {
            var parts = pattern.Split('~');
            Prefix = parts[0];
            if (parts.Length > 1)
            {
                Suffix = parts[1];
            }
            else
            {
                Suffix = "";
            }
        }

        public string Prefix { get; set; }
        public string Suffix { get; set; }
    }
}
