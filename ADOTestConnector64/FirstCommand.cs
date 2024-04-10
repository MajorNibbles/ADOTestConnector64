using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using System;
using System.Globalization;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using AzureDevOpsTestConnector.DTOs;
using System.Collections.Generic;
using AzureDevOpsTestConnector.Models.DataModels;
using AzureDevOpsTestConnector.Models;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using AzureDevOpsTestConnector.Services.Interfaces;
using AzureDevOpsTestConnector.Services.DiService;
using EnvDTE;
using System.ComponentModel.Design;

namespace ADOTestConnector64
{
    /// <summary>
    /// Command handler
    /// </summary>
    internal sealed class FirstCommand
    {
        /// <summary>
        /// Command ID.
        /// </summary>
        public const int CommandId = 0x0100;

        /// <summary>
        /// Command menu group (command set GUID).
        /// </summary>
        public static readonly Guid CommandSet = new Guid("20852553-7dbf-4bf0-8149-e8a72394b22e");

        /// <summary>
        /// VS Package that provides this command, not null.
        /// </summary>
        private readonly AsyncPackage package;

        private ADOTestConnector64Package _options;
        private AdoUploadData _adoData;
        private ConfigurationData _configData;
        private RuntimeData _runData;
        private List<string> _testFileLines;
        private List<TestCaseData> _tests;

        private static IAzureWorkItemCreator _workItemCreator => (IAzureWorkItemCreator) DependencyContainer.ServiceProvider.GetService(typeof(IAzureWorkItemCreator));

        /// <summary>
        /// Initializes a new instance of the <see cref="FirstCommand"/> class.
        /// Adds our command handlers for menu (commands must exist in the command table file)
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        /// <param name="commandService">Command service to add command to, not null.</param>
        private FirstCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            this.package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(this.Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        /// <summary>
        /// Gets the instance of the command.
        /// </summary>
        public static FirstCommand Instance
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets the service provider from the owner package.
        /// </summary>
        private Microsoft.VisualStudio.Shell.IAsyncServiceProvider ServiceProvider
        {
            get
            {
                return this.package;
            }
        }

        /// <summary>
        /// Initializes the singleton instance of the command.
        /// </summary>
        /// <param name="package">Owner package, not null.</param>
        public static async Task InitializeAsync(AsyncPackage package)
        {
            // Switch to the main thread - the call to AddCommand in FirstCommand's constructor requires
            // the UI thread.
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);

            OleMenuCommandService commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            Instance = new FirstCommand(package, commandService);
        }

        /// <summary>
        /// This function is the callback used to execute the command when the menu item is clicked.
        /// See the constructor to see how the menu item is associated with this function using
        /// OleMenuCommandService service and MenuCommand class.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="e">Event args.</param>
        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _options = this.package as ADOTestConnector64Package;
            _adoData = new AdoUploadData(_options);
            _configData = new ConfigurationData(_options);
            _runData = new RuntimeData();
            _tests = new List<TestCaseData>();
            DTE dte = (DTE)Package.GetGlobalService(typeof(DTE));
            var currentFilePath = dte.ActiveDocument.FullName;

            //check if file is specflow or not
            if (currentFilePath.EndsWith(".feature"))
            {
                _runData.SpecflowFeatureFile = true; //flag to system to use # instead of // in comments
                _configData.TestPlanPattern = new TagPattern(_configData.FeatureTestPlanAttributePattern);
                _configData.TestSuitePattern = new TagPattern(_configData.FeatureTestSuiteAttributePattern);
                _configData.TestCasePattern = new TagPattern(_configData.FeatureTestCaseAttributePattern);
            }
            else
            {
                _configData.TestPlanPattern = new TagPattern(_configData.ClassTestPlanAttributePattern);
                _configData.TestSuitePattern = new TagPattern(_configData.ClassTestSuiteAttributePattern);
                _configData.TestCasePattern = new TagPattern(_configData.ClassTestCaseAttributePattern);
            }


            VerifySavedInputs(currentFilePath);

            //Read CS test file
            _testFileLines = File.ReadAllLines(currentFilePath).ToList();
            GetTestPlanAndTestSuiteIds(currentFilePath, _testFileLines);

            if (VerifyTestPlanId()) return;

            if (VerifyTestSuiteDetails()) return;

            if (_adoData.TestSuiteId == 0 || _adoData.TestSuiteId == -1)
            {
                Interaction.MsgBox("Test Suite not correctly created/entered, no action has been performed");
                return;
            }

            if (_adoData.TestPlanId == 0 || _adoData.TestPlanId == -1)
            {
                Interaction.MsgBox("Test Plan not correctly created/entered, no action has been performed");
                return;
            }

            for (int i = 0; i < _testFileLines.Count; i++)
            {
                var currentLine = _testFileLines.ElementAt(i);
                if (_runData.SpecflowFeatureFile)
                {
                    var testCases = ScanForSpecflowScenarios(currentLine, i);
                    _tests.AddRange(testCases);
                }
                else
                {
                    var testCase = ScanForMsTestAndNunitTests(currentLine, i);
                    if (testCase != null)
                        _tests.Add(testCase);
                }
            }


            //All tests gathered here, send off to Azure to update or create
            foreach (var testCaseData in _tests)
            {
                var wIcTestData = new WorkItemCreatorTestCaseData
                {
                    UpdateTestCaseName = _options.UpdateTestCaseTitle,
                    AzureDevopsBaseUrl = _options.AzureDevopsBaseUrl,
                    currentNameSpace = _adoData.CurrentNameSpace,
                    currentSolutionDllName = _adoData.CurrentSolutionDllName,
                    PatCode = _options.PatCode,
                    ProjectName = _options.ProjectName,
                    ReadableTestCaseName = testCaseData.ReadableTestCaseName,
                    TestCaseMethodName = testCaseData.TestCaseName,
                    TestCaseReference = testCaseData.TestCaseId,
                    testPlanId = _adoData.TestPlanId,
                    TestSteps = testCaseData.TestSteps,
                    testSuiteId = _adoData.TestSuiteId,
                    UpdateTestCaseAssociation = _configData.UpdateTestCaseAssociation
                };

                testCaseData.TestCaseId = _workItemCreator.CreateOrUpdateTestCase(wIcTestData);
            }

            List<int> testCaseSignatureIndexesPopulated = new List<int>();

            foreach (var testCaseData in _tests.OrderByDescending(t => t.TestCaseSignatureIndex))
            {
                if (testCaseSignatureIndexesPopulated.Contains(testCaseData.TestCaseSignatureIndex))
                {
                    continue;
                }
                testCaseSignatureIndexesPopulated.Add(testCaseData.TestCaseSignatureIndex);

                var allIdsForTestCase = _tests.Where(t => t.TestCaseSignatureIndex == testCaseData.TestCaseSignatureIndex).Select(t => t.TestCaseId).Distinct().ToList();
                var testCaseReference = ListToString(allIdsForTestCase.OrderBy(t => t).ToList());

                if (testCaseData.ExistingTestCaseReferenceIndex != 0)
                {
                    var replacementLine = UpdateTestCaseReference(_testFileLines.ElementAt(testCaseData.ExistingTestCaseReferenceIndex), _configData.TestCasePattern, testCaseReference);
                    _testFileLines.RemoveAt(testCaseData.ExistingTestCaseReferenceIndex);
                    _testFileLines.Insert(testCaseData.ExistingTestCaseReferenceIndex, replacementLine);
                }
                else
                {
                    _testFileLines.Insert(testCaseData.TestCaseSignatureIndex, $"{testCaseData.WhiteSpace}{_configData.TestCasePattern.Prefix}{testCaseReference}{_configData.TestCasePattern.Suffix}");
                }
            }

            //Update TestPlan and TestSuite references

            if (_runData.TestSuiteAttributeLine == 0)
            {
                //No existing test plan attribute
                _testFileLines.Insert(_runData.ClassNameLine, $"{_configData.TestSuitePattern.Prefix}{_adoData.TestSuiteId}{_configData.TestSuitePattern.Suffix}");
            }
            else
            {
                //Update into existing Test Plan reference
                var replacementLine = UpdateTestReference(_testFileLines.ElementAt(_runData.TestSuiteAttributeLine),
                    _configData.TestSuitePattern, _adoData.TestSuiteId);
                _testFileLines.RemoveAt(_runData.TestSuiteAttributeLine);
                _testFileLines.Insert(_runData.TestSuiteAttributeLine, replacementLine);
            }

            //Use bool as Test Plan attribute can actually be on line 0 in specflow feature files
            if (!_runData.TestPlanAttributeFound)
            {
                //No existing test plan attribute
                _testFileLines.Insert(_runData.ClassNameLine, $"{_configData.TestPlanPattern.Prefix}{_adoData.TestPlanId}{_configData.TestPlanPattern.Suffix}");
            }
            else
            {
                //Update into existing Test Plan reference
                var replacementLine = UpdateTestReference(_testFileLines.ElementAt(_runData.TestPlanAttributeLine),
                    _configData.TestPlanPattern, _adoData.TestPlanId);
                _testFileLines.RemoveAt(_runData.TestPlanAttributeLine);
                _testFileLines.Insert(_runData.TestPlanAttributeLine, replacementLine);
            }

            //Now update original file:
            File.WriteAllLines(currentFilePath, _testFileLines);

            Interaction.MsgBox("Sync completed");
        }

        private bool VerifyTestSuiteDetails()
        {
            if (_adoData.TestSuiteId == 0)
            {
                var testSuiteInput = Interaction.InputBox(
                    "We need a Test Suite to link the Test Cases too, either provide an ID of an existing Test Suite, or a name of a new one",
                    "Test Plan Id or Name", "", -1, -1);
                if (testSuiteInput == "")
                {
                    Interaction.MsgBox("No Test Suite input detected, no action has been performed");
                    return true;
                }

                var testSuiteReqLinkIds = Interaction.InputBox(
                    $"Would you like to link the new Test Suite to Requirements? If so enter the requirement ID.{Environment.NewLine}If not leave blank. e.g. 123456",
                    "Test Suite Requirement Id", "", -1, -1);

                int.TryParse(testSuiteInput, out var testSuiteIdParse);
                _adoData.TestSuiteId = testSuiteIdParse;

                if (_adoData.TestSuiteId == 0)
                {
                    //create new Test Plan with inputted name
                    _adoData.TestSuiteId = _workItemCreator.CreateNewTestSuite(_options.PatCode, _options.AzureDevopsBaseUrl,
                        _adoData.CurrentProjectName, testSuiteInput, _adoData.TestPlanId, testSuiteReqLinkIds);
                }
            }

            return false;
        }

        private bool VerifyTestPlanId()
        {
            //If no testSuiteId, ask for TestPlanId and TestSuiteName to create
            if (_adoData.TestPlanId == 0)
            {
                var testPlanInput = Interaction.InputBox(
                    "We need a Test Plan to link the Test Cases to, either provide an ID of an existing Test Plan, or a name of a new one",
                    "Test Plan Id or Name", "", -1, -1);
                if (testPlanInput == "")
                {
                    Interaction.MsgBox("No Test Plan input detected, no changes have been made");
                    return true;
                }

                int.TryParse(testPlanInput, out var testPlanIdParse);
                _adoData.TestPlanId = testPlanIdParse;
                if (_adoData.TestPlanId == 0)
                {
                    //create new Test Plan with inputted name
                    _adoData.TestPlanId = _workItemCreator.CreateNewTestPlan(_options.PatCode, _options.AzureDevopsBaseUrl,
                        _adoData.CurrentProjectName, testPlanInput);
                }
            }

            return false;
        }

        private void GetTestPlanAndTestSuiteIds(string currentFilePath, List<string> testFileLines)
        {
            //find class declaration line
            for (int i = 0; i < testFileLines.Count; i++)
            {
                if (testFileLines[i].Contains("Feature:"))
                {
                    //find current namespace in specflow file
                    var featureName = testFileLines[i].Replace("Feature:", "").Trim();
                    featureName = Regex.Replace(featureName, @"(^\w)|(\s\w)", m => m.Value.ToUpper()); //replace first letters with capitals
                    featureName = featureName.Replace(" ", "");
                    featureName += "Feature";

                    //This file is a specflow feature file, look at .cs file to find namespace:
                    try
                    {
                        var featureCsFileLines = File.ReadAllLines(currentFilePath + ".cs").ToList();
                        var lineWithNameSpace = featureCsFileLines.Where(l => l.Contains("namespace ")).FirstOrDefault();
                        _adoData.CurrentNameSpace = lineWithNameSpace.Replace("namespace ", "").Trim() + "." + featureName;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        //ignore
                    }
                    break;
                }

                if (testFileLines[i].Contains(" class "))
                {
                    _runData.ClassNameLine = i;
                    // class found, look above for TestSuiteReference and TestPlanReference
                    for (int j = 1; j <= 10; j++)
                    {
                        if (i - j <= 1)
                        {
                            break;
                        }

                        if (testFileLines[i - j].Contains(_configData.TestPlanPattern.Prefix) && testFileLines[i - j].Contains(_configData.TestPlanPattern.Suffix))
                        {
                            _runData.TestPlanAttributeFound = true;
                            _adoData.TestPlanId = ExtractReferenceId(testFileLines[i - j], _configData.TestPlanPattern);
                            _runData.TestPlanAttributeLine = i - j;
                        }

                        if (testFileLines[i - j].Contains(_configData.TestSuitePattern.Prefix) && testFileLines[i - j].Contains(_configData.TestSuitePattern.Suffix))
                        {
                            _adoData.TestSuiteId = ExtractReferenceId(testFileLines[i - j], _configData.TestSuitePattern);
                            _runData.TestSuiteAttributeLine = i - j;
                        }
                    }

                    //find current namespace in .cs file

                    var classname = "";
                    var classnameSplit = testFileLines[i].Split(' ');
                    for (int k = 0; k < classnameSplit.Length; k++)
                    {
                        if (classnameSplit[k].Contains("class"))
                        {
                            classname = classnameSplit[k + 1].Trim();
                            break;
                        }
                    }


                    var lineWithNameSpace = testFileLines.Where(l => l.Contains("namespace ")).FirstOrDefault();
                    _adoData.CurrentNameSpace = lineWithNameSpace.Replace("namespace ", "").Trim() + "." + classname;

                    break;
                }
            }

            //unable to find class line, maybe specflow feature file, look for class attributes:
            if (_runData.ClassNameLine == 0)
            {
                for (int i = 0; i < testFileLines.Count; i++)
                {
                    if (i > testFileLines.Count)
                    {
                        break;
                    }
                    if (testFileLines[i].Contains(_configData.TestPlanPattern.Prefix) && testFileLines[i].Contains(_configData.TestPlanPattern.Suffix))
                    {
                        _runData.TestPlanAttributeFound = true;
                        _adoData.TestPlanId = ExtractReferenceId(testFileLines[i], _configData.TestPlanPattern);
                        _runData.TestPlanAttributeLine = i;
                    }

                    if (testFileLines[i].Contains(_configData.TestSuitePattern.Prefix) && testFileLines[i].Contains(_configData.TestSuitePattern.Suffix))
                    {
                        _adoData.TestSuiteId = ExtractReferenceId(testFileLines[i], _configData.TestSuitePattern);
                        _runData.TestSuiteAttributeLine = i;
                    }
                }
            }
        }


        //Verifys the ADO Project name and Solution Dll is correct
        private void VerifySavedInputs(string currentFilePath)
        {
            //Double check projectName
            _adoData.CurrentProjectName = Interaction.InputBox("Please enter the Azure Devops Project name you want the Test Case created in",
                "Project Name", _adoData.CurrentProjectName, -1, -1);
            _options.ProjectName = _adoData.CurrentProjectName;

            string solutionProjectName = FindContainingSolutionProject(currentFilePath);

            //Double check solutiondll name
            if (_configData.UpdateTestCaseAssociation)
                _adoData.CurrentSolutionDllName = Interaction.InputBox("Please enter the SolutionDll name you want to associate the tests with in ADO, please leave blank if you wish no association to be made",
                    "Association Dll Name", solutionProjectName, -1, -1);
            _options.AssociationDllName = _adoData.CurrentSolutionDllName;

            //Double check automation namespace
            if (_configData.UpdateTestCaseAssociation)
                _adoData.CurrentNameSpace = Interaction.InputBox("Please validate the Automated Test Name association namespace you want to associate the tests with in ADO, please leave blank if you wish no association to be made",
                    "Association Name Space, NOTE: If using specflow the format of this must be: NameSpace+FeatureName+'Feature'", _adoData.CurrentNameSpace, -1, -1);

            _options.CurrentNameSpace = _adoData.CurrentNameSpace;
        }

        private string HeaderValuePairToString(Dictionary<string, List<string>> headerValuePairs, int index)
        {
            var returnString = "";
            foreach (var headerValuePair in headerValuePairs)
            {
                returnString += $"\"{headerValuePair.Value[index]}\",";
            }

            if (returnString.Length > 0)
            {
                returnString = returnString.Substring(0, returnString.Length - 1);
            }

            return returnString;
        }

        private string ReplaceSpecflowStepWithListItems(string input, Dictionary<string, List<string>> headerValuePairs, int valueIndex)
        {
            for (int i = 0; i < headerValuePairs.Count; i++)
            {
                input = input.Replace($"<{headerValuePairs.ElementAt(i).Key}>",
                    $"{headerValuePairs.ElementAt(i).Value[valueIndex]}");
            }

            return input;
        }

        private string ListToString(List<string> input)
        {
            var outputStr = "";
            foreach (var item in input)
            {
                outputStr += item + ", ";
            }

            if (outputStr.Length >= 2)
            {
                outputStr = outputStr.Substring(0, outputStr.Length - 2);
            }

            return outputStr;
        }

        private string FindContainingSolutionProject(string currentFilePath)
        {
            string dirName = Path.GetDirectoryName(currentFilePath);
            for (int i = 0; i < 5; i++)
            {

                var files = Directory.GetFiles(dirName, "*.csproj");
                if (files.Length > 0)
                {
                    return Path.GetFileNameWithoutExtension(files.First()) + ".dll";
                }

                dirName = Directory.GetParent(dirName).FullName;
            }

            return "";

        }

        private string ExtractWhiteSpace(string fullLine)
        {
            Regex pattern = new Regex("(?<whiteSpace>\\s+)\\S");
            var match = pattern.Match(fullLine);
            var returnable = match.Groups["whiteSpace"].Value == " " ? "" : match.Groups["whiteSpace"].Value;
            return returnable;
        }

        private string ExtractSpecflowScenarioName(string fullLine)
        {
            fullLine = fullLine.Replace("Scenario:", "");
            fullLine = fullLine.Replace("Scenario Outline:", "");
            fullLine = fullLine.Trim();

            // The following 2 lines are to match SpecFlows handling of methods that start with a digit.
            // If a Scenario starts with a number, SpecFlow automatically adds an underscore otherwise the method will not compile.
            char firstCharacter = fullLine.ToCharArray()[0];
            if (Char.IsDigit(firstCharacter)) fullLine = $"_{fullLine}";

            return fullLine;
        }

        private string UpdateTestReference(string fullLine, TagPattern tagPattern, int testReferenceId)
        {
            string pattern = $"{ParseRegexString(tagPattern.Prefix)}[\\d,]*{ParseRegexString(tagPattern.Suffix)}";
            var returnString = Regex.Replace(fullLine, pattern, $"{tagPattern.Prefix}{testReferenceId}{tagPattern.Suffix}");

            return returnString;
        }

        private string UpdateTestCaseReference(string fullLine, TagPattern tagPattern, string testReferenceId)
        {
            string pattern = $"{ParseRegexString(tagPattern.Prefix)}[\\d,]*{ParseRegexString(tagPattern.Suffix)}";
            var returnString = Regex.Replace(fullLine, pattern, $"{tagPattern.Prefix}{testReferenceId}{tagPattern.Suffix}");

            return returnString;
        }

        private int ExtractReferenceId(string fullLine, TagPattern tagPattern)
        {
            Regex pattern = new Regex($"{ParseRegexString(tagPattern.Prefix)}(\\d*){ParseRegexString(tagPattern.Suffix)}");
            var match = pattern.Match(fullLine);

            return int.Parse(match.Groups[match.Groups.Count - 1].Value);
        }

        private string ExtractTestCaseReferenceId(string fullLine, TagPattern tagPattern)
        {
            Regex pattern = new Regex($"{ParseRegexString(tagPattern.Prefix)}([\\d,]*){ParseRegexString(tagPattern.Suffix)}");
            var match = pattern.Match(fullLine);

            return match.Groups[match.Groups.Count - 1].Value;
        }

        private string ParseRegexString(string input)
        {
            input = input.Replace(@"\", @"\\").Replace("^", @"\^");
            input = input.Replace("(", @"\(").Replace(")", @"\)");
            input = input.Replace(@"$", @"\$");

            return input;
        }

        private string UpdateTestCaseReference(string fullLine, string testCaseReferenceId, string testCaseReferenceAttribute)
        {
            string pattern = $"{testCaseReferenceAttribute}\\(.*\\)";
            var returnString = Regex.Replace(fullLine, pattern, $"{testCaseReferenceAttribute}({testCaseReferenceId})");

            return returnString;
        }

        private string TitleCaseToHuman(string input)
        {
            var withSpaces = Regex.Replace(Regex.Replace(input.ToString(), @"(\P{Ll})(\P{Ll}\p{Ll})", "$1 $2"), @"(\p{Ll})(\P{Ll})", "$1 $2");
            return withSpaces.Replace("_ ", " ");
        }

        private bool DoesLineHaveKnownTestTag(string fullLine)
        {
            List<string> knownTestTags = new List<string>
            {
                "Test",
                "TestMethod",
                "DataTestMethod"
            };

            var currentLineNoSpace = fullLine.Replace(" ", "");

            foreach (var tag in knownTestTags)
            {
                if (currentLineNoSpace.Contains($"[{tag}]")
                    || currentLineNoSpace.Contains($"[{tag},")
                    || currentLineNoSpace.Contains($",{tag},")
                    || currentLineNoSpace.Contains($",{tag}]"))
                {
                    return true;
                }
            }

            return false;
        }

        private string ExtractMethodName(string fullLine)
        {
            var methodName = fullLine.Substring(fullLine.IndexOf("public"));
            methodName = methodName.Split(' ').First(s => s.Contains("("));
            methodName = methodName.Substring(0, methodName.IndexOf('('));

            return methodName;
        }

        private TestCaseData ScanForMsTestAndNunitTests(string currentLine, int currentLineInFile)
        {
            if (DoesLineHaveKnownTestTag(currentLine))
            {
                //find Method signature
                for (int j = currentLineInFile; j < currentLineInFile + 10; j++)
                {
                    if (j >= _testFileLines.Count)
                    {
                        break;
                    }
                    currentLine = _testFileLines.ElementAt(j);



                    if (currentLine.Contains("public") && currentLine.Contains("(") && !currentLine.Contains("=") && !currentLine.Contains("//"))
                    {
                        //Find Method Name and any existing methodTestCaseIds
                        var methodName = ExtractMethodName(currentLine);
                        var readableMethodName = TitleCaseToHuman(methodName);
                        var methodTestCaseId = "";
                        var testCaseSignatureIndex = j;
                        var testReferenceIndex = 0;
                        var whiteSpace = ExtractWhiteSpace(currentLine);

                        var previousLineIndex = j - 1;

                        do
                        {
                            currentLine = _testFileLines.ElementAt(previousLineIndex);
                            if (currentLine.Contains(_configData.TestCasePattern.Prefix) && currentLine.Contains(_configData.TestCasePattern.Suffix))
                            {
                                methodTestCaseId = ExtractTestCaseReferenceId(currentLine, _configData.TestCasePattern);
                                testReferenceIndex = previousLineIndex;
                                break;
                            }
                            previousLineIndex -= 1;
                        } while (!currentLine.Contains("}") && !currentLine.Contains("{") && previousLineIndex > 1 && methodTestCaseId == "");

                        return new TestCaseData
                        {
                            TestCaseName = methodName,
                            ReadableTestCaseName = readableMethodName,
                            TestCaseId = methodTestCaseId,
                            TestCaseSignatureIndex = testCaseSignatureIndex,
                            ExistingTestCaseReferenceIndex = testReferenceIndex,
                            WhiteSpace = whiteSpace
                        };
                    }
                }
            }

            return null;
        }

        private List<TestCaseData> ScanForSpecflowScenarios(string currentLine, int currentLineInFile)
        {
            var testCases = new List<TestCaseData>();
            //See if line contains the start of a scenario
            if (currentLine.Contains("Scenario:") || currentLine.Contains("Scenario Outline:") && !currentLine.Contains("#"))
            {

                //flag for scenario outline
                bool scenarioOutline = currentLine.Contains("Scenario Outline:");
                //Find Method Name and any existing methodTestCaseIds
                var methodName = ExtractSpecflowScenarioName(currentLine);
                var whiteSpace = ExtractWhiteSpace(currentLine);
                var readableMethodName = methodName; //specflow scenarios are already readable
                var methodTestCaseId = "";
                var testCaseSignatureIndex = currentLineInFile;
                var testReferenceIndex = 0;

                var previousLineIndex = currentLineInFile - 1;

                //look for existing test case id
                do
                {
                    currentLine = _testFileLines.ElementAt(previousLineIndex);
                    if (currentLine.Contains(_configData.TestCasePattern.Prefix) && currentLine.Contains(_configData.TestCasePattern.Suffix))
                    {
                        methodTestCaseId = ExtractTestCaseReferenceId(currentLine, _configData.TestCasePattern);
                        testReferenceIndex = previousLineIndex;
                        break;
                    }
                    previousLineIndex -= 1;
                } while (!currentLine.ToLower().Contains("given") && !currentLine.ToLower().Contains("when") &&
                !currentLine.ToLower().Contains("then") && previousLineIndex > 1 && methodTestCaseId == "");

                //find specflow steps if enabled
                List<string> specflowSteps = new List<string>();
                if (_configData.UpdateSpecFlowSteps)
                {
                    var currentLineIndex = currentLineInFile + 1;
                    do
                    {
                        currentLine = _testFileLines.ElementAt(currentLineIndex);
                        if (currentLine.Contains("Given") || currentLine.Contains("When") || currentLine.Contains("Then") || currentLine.Contains("And") || currentLine.Contains("But") && !currentLine.Contains("#") && !currentLine.Contains("|"))
                        {
                            specflowSteps.Add(currentLine.Trim());
                        }
                        currentLineIndex += 1;
                    } while (currentLineIndex < _testFileLines.Count
                    && !_testFileLines.ElementAt(currentLineIndex).Contains("Scenario:")
                    && !_testFileLines.ElementAt(currentLineIndex).Contains("Scenario Outline:")
                    && !_testFileLines.ElementAt(currentLineIndex).Contains("Examples:")
                    && !_testFileLines.ElementAt(currentLineIndex).Contains("Background:")
                    && !_testFileLines.ElementAt(currentLineIndex).Contains(_configData.TestCasePattern.Prefix));
                }

                //find specflow example tests if enabled
                if (_configData.SeparateSpecFlowExamples && scenarioOutline)
                {
                    //find examples table header:
                    var currentLineIndex = currentLineInFile;
                    var examplesLine = -1;
                    do
                    {
                        currentLineIndex++;
                        if (_testFileLines.ElementAt(currentLineIndex).ToLower().Contains("examples:"))
                        {
                            for (int h = 1; h < 5; h++)
                            {
                                if (_testFileLines.Count < currentLineIndex + h)
                                {
                                    break;
                                }
                                if (_testFileLines.ElementAt(currentLineIndex + h).Contains("|"))
                                {
                                    //first line of examples table found here
                                    examplesLine = currentLineIndex + h;
                                    break;
                                }
                            }
                        }
                    } while (currentLineIndex < _testFileLines.Count
                    && !_testFileLines.ElementAt(currentLineIndex).Contains("Scenario:")
                    && !_testFileLines.ElementAt(currentLineIndex).Contains("Scenario Outline:") && !_testFileLines.ElementAt(currentLineIndex).Contains("Background:")
                    && !_testFileLines.ElementAt(currentLineIndex).Contains(_configData.TestCasePattern.Prefix)
                    && examplesLine == -1);

                    if (examplesLine == -1)
                    {
                        //unable to find examples line
                        return testCases;
                    }

                    //find header row values
                    Dictionary<string, List<string>> headerValuePairs = new Dictionary<string, List<string>>();
                    var headers = _testFileLines.ElementAt(examplesLine).Split('|');
                    var skipHeaders = 0;
                    foreach (var header in headers)
                    {

                        if (!string.IsNullOrEmpty(header.Trim()))
                        {
                            headerValuePairs.Add(header.Trim(), new List<string>());
                        }
                        else
                        {
                            if (headerValuePairs.Count == 0)
                                skipHeaders++;
                        }
                    }

                    //collect rest of data
                    do
                    {
                        examplesLine++;
                        var data = _testFileLines.ElementAt(examplesLine).Split('|');
                        for (int h = 0 + skipHeaders; h <= headerValuePairs.Count; h++)
                        {
                            headerValuePairs.ElementAt(h - skipHeaders).Value.Add(data[h].Trim());
                        }
                    } while (_testFileLines.Count > examplesLine + 1 && _testFileLines.ElementAt(examplesLine + 1).Contains('|'));

                    var splitTestCaseReferenceIds = methodTestCaseId.Split(',');

                    //update method name and steps with examples data
                    for (int h = 0; h < headerValuePairs.ElementAt(0).Value.Count; h++)
                    {
                        var testCaseIds = splitTestCaseReferenceIds.Length >= h + 1 ? splitTestCaseReferenceIds[h].Trim() : "";
                        var paramString = HeaderValuePairToString(headerValuePairs, h);
                        var enhancedMethodName = methodName + $"({paramString})";
                        var enhancedSteps = new List<string>();
                        foreach (var step in specflowSteps)
                        {
                            enhancedSteps.Add(ReplaceSpecflowStepWithListItems(step, headerValuePairs, h));
                        }

                        //add enhanced tests to test case data

                        testCases.Add(new TestCaseData
                        {
                            TestCaseName = enhancedMethodName,
                            ReadableTestCaseName = readableMethodName,
                            TestCaseId = testCaseIds,
                            TestCaseSignatureIndex = testCaseSignatureIndex,
                            ExistingTestCaseReferenceIndex = testReferenceIndex,
                            WhiteSpace = whiteSpace,
                            TestSteps = enhancedSteps
                        });
                    }
                }
                else
                {
                    testCases.Add(new TestCaseData
                    {
                        TestCaseName = methodName,
                        ReadableTestCaseName = readableMethodName,
                        TestCaseId = methodTestCaseId,
                        TestCaseSignatureIndex = testCaseSignatureIndex,
                        ExistingTestCaseReferenceIndex = testReferenceIndex,
                        WhiteSpace = whiteSpace,
                        TestSteps = specflowSteps
                    });
                }
            }

            return testCases;
        }
    }
}
