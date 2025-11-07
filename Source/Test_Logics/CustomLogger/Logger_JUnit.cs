using System.Xml.Linq;
using DevTools.Testing;

namespace VehicleMapFramework.Test_Logics;

public class Logger_JUnit : Logger
{
    private readonly XDocument document = new();

    private readonly XTestSuite testsuite = new(TestSuiteName);
    
    private readonly Stack<XTestCase> testCaseStack = [];
    
    private State currentState = State.None;
    
    private const string TestSuiteName = "testsuite";
    
    private const string TestCaseName = "testcase";
    
    private const string NameAttribute = "name";
    
    private const string ClassNameAttribute = "classname";
    
    private const string TestsAttribute = "tests";
    
    private const string SkippedAttribute = "skipped";
    
    private const string FailureAttribute = "failure";
    
    private const string AssertionAttribute = "assertions";
    
    private const string MessageAttribute = "message";

    public Logger_JUnit(Config config) : base(config)
    {
        document.Add(testsuite);
        testsuite.Add(new XElement("properties"));
    }

    public void ParseAndAdd(string message)
    {
        const string SettingUp = "Setting up";
        const string Executing = "Executing";
        const string TearingDown = "Tearing down";
        const string BeginGroup = "-- Begin Group";
        const string EndGroup = "-- End Group";
        const string PassedLabel = "[Passed]";
        const string FailedLabel = "[Failed]";
        const string SkippedLabel = "[Skipped]";

        if (message.StartsWith(SettingUp))
            currentState = State.SettingUp;
        else if (message.StartsWith(Executing))
        {
            EndTestCase();
            var name = message[10..].Split("::");
            EnterTestCase(name[0], name[1]);
        }
        else if (message.StartsWith(TearingDown))
            currentState = State.TearingDown;
        else if (message.StartsWith(BeginGroup))
        {
            if (currentState == State.TearingDown)
            {
                EndTestCase();
                currentState = State.None;           
            }
            var classname = default(string);
            var name = message[15..].Replace("(", "").Replace(")", "");
            if (testCaseStack.TryPeek(out var parent))
            {
                classname = parent.Attribute(ClassNameAttribute)?.Value;
                name = $"{parent.Attribute(NameAttribute)?.Value}.{name}";
            }
            EnterTestCase(classname, name);
        }
        else if (message.StartsWith(EndGroup))
            EndTestCase();
        else if (message.StartsWith(PassedLabel))
        {
            foreach (var testCase in testCaseStack)
                testCase.assertions++;
            testsuite.assertions++;
        }
        else if (message.StartsWith(FailedLabel))
        {
            if (testCaseStack.TryPeek(out var testCase))
            {
                var failure = new XElement(FailureAttribute);
                failure.SetAttributeValue(MessageAttribute, message[9..]);
                testCase.Add(failure);
                foreach (var testCase2 in testCaseStack)
                    testCase2.assertions++;
                testsuite.assertions++;
            }
        }
        else if (message.StartsWith(SkippedLabel))
        {
            var skipped = new XElement(SkippedAttribute);
            skipped.SetAttributeValue(MessageAttribute, message[10..]);
            testCaseStack.Peek().Add(skipped);
        }
    }

    public void Save(FileStream stream)
    {
        WriteTestResults();
        document.Save(stream);
        return;

        void WriteTestResults()
        {
            while (testCaseStack.Count > 0)
            {
                EndTestCase();
            }
            testsuite.SetAttributeValue(TestsAttribute, testsuite.Elements(TestCaseName).Count());
            testsuite.SetAttributeValue(SkippedAttribute, testsuite.Elements(TestCaseName)
                .SelectMany(testcase => testcase.Elements().Where(e => e.Name == SkippedAttribute)).Count());
            testsuite.SetAttributeValue(FailureAttribute, testsuite.Elements(TestCaseName)
                .SelectMany(testcase => testcase.Elements().Where(e => e.Name == FailureAttribute)).Count());
            testsuite.SetAttributeValue(AssertionAttribute, testsuite.assertions);
        }
    }
    
    private void EnterTestCase(string classname, string name)
    {
        var testCase = new XTestCase(TestCaseName);
        if (classname != null)
            testCase.SetAttributeValue(ClassNameAttribute, classname);
        testCase.SetAttributeValue(NameAttribute, name);
        testsuite.Add(testCase);
        testCaseStack.Push(testCase);
    }
        
    private void EndTestCase()
    {
        if (testCaseStack.TryPop(out var testCase))
            testCase.SetAttributeValue(AssertionAttribute, testCase.assertions);
    }

    private enum State
    {
        None,
        SettingUp,
        TearingDown,
    }

    private class XTestCase(XName name) : XElement(name)
    {
        public int assertions;
    }

    private class XTestSuite(XName name) : XTestCase(name);
}