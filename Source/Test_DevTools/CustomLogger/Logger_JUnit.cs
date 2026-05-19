using System.Text.RegularExpressions;
using System.Xml.Linq;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public class Logger_JUnit : CustomLoggerBase
{
    private readonly XDocument document = new();

    private readonly XTestSuite testsuite = new(TestSuiteName);
    
    private readonly Stack<XTestCase> testCaseStack = [];
    
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
        testsuite.SetAttributeValue(NameAttribute, "Local test by DevTools");
        document.Add(testsuite);
    }

    public override void WriteCustom(StreamWriter writer, string message)
    {
        if (message.NullOrEmpty()) return;
        if (!message.StartsWith(Indent))
        {
            EndTestCase();
            var split = message.Split(SpaceFour);
            var label = split[0];
            var str = split[1];
            var name = Regex.Replace(str, @"\([^)]*\)", ""); // テストの成否と括弧内のArgsを除去
            EnterTestCase(null, name);
            Evaluate(label, str);
        }
        else
        {
            var split = message.Split(SpaceFour);
            var label = split[0][Indent.Length..];
            var str = split[1];
            var name = Regex.Replace(str, @"\([^)]*\)", "");
            string fixture = null;
            if (testCaseStack.TryPeek(out var parent))
                fixture = parent.Name.ToString();
            EnterTestCase(fixture, name);
            Evaluate(label, str);
            EndTestCase();
        }
        return;

        void Evaluate(string label, string str)
        {
            switch (label)
            {
                case SkippedLabel:
                    var skipped = new XElement(SkippedAttribute);
                    skipped.SetAttributeValue(MessageAttribute, str.Split(SpaceTwo).ElementAtOrDefault(1));
                    testCaseStack.Peek().Add(skipped);
                    break;
                
                case FailureAttribute:
                    var failure = new XElement(FailureAttribute);
                    failure.SetAttributeValue(MessageAttribute, str.Split(SpaceTwo).ElementAtOrDefault(1));
                    testCaseStack.Peek().Add(failure);
                    break;
                    
                case PassedLabel:
                    foreach (var testCase in testCaseStack)
                        testCase.assertions++;
                    testsuite.assertions++;
                    break;
            }
        }
    }

    public override void DisposeCustom(StreamWriter writer)
    {
        WriteTestResults();
        document.Save(writer);
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

    private class XTestCase(XName name) : XElement(name)
    {
        public int assertions;
    }

    private class XTestSuite(XName name) : XTestCase(name);
}