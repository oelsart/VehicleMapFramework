using System.Text.RegularExpressions;
using System.Xml.Linq;
using DevTools.Testing;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public class Logger_JUnit : ILogWriter
{
  protected const string Tab = "--\t";
  protected const string Space = "  ";
  protected const string PassedLabel = "[Passed]";
  protected const string FailedLabel = "[Failed]";
  protected const string SkippedLabel = "[Skipped]";
  
  private const string TestSuiteName = "testsuite";
  private const string TestCaseName = "testcase";
  private const string NameAttribute = "name";
  private const string ClassNameAttribute = "classname";
  private const string TestsAttribute = "tests";
  private const string SkippedAttribute = "skipped";
  private const string FailureAttribute = "failure";
  private const string AssertionAttribute = "assertions";
  private const string MessageAttribute = "message";
  private readonly XDocument document = new();
  private readonly Stack<XTestCase> testCaseStack = [];
  private readonly XTestSuite testsuite = new(TestSuiteName);
  private XElement failure;
  
  private readonly FileStream fileStream;
  private readonly StreamWriter writer;

  public Logger_JUnit(Logger logger)
  {
    var mode = FileMode.Append;
    if (logger.IsOwner)
    {
      // Creates or clears log file, we can immediately close it
      // since we want to open with StreamWriter with append mode.
      mode = FileMode.Create;
    }
    fileStream = new FileStream(logger.LogConfig.FullPath, mode, FileAccess.Write, FileShare.ReadWrite);
    writer = new StreamWriter(fileStream);
  }
  
  void ILogWriter.PostInit()
  {
    testsuite.SetAttributeValue(NameAttribute, "Local test by DevTools");
    document.Add(testsuite);
  }

  void ILogWriter.WriteLine(string message)
  {
    if (message.NullOrEmpty()) return;

    var labelMatch = Regex.Match(message, @"\[.*?\]"); // [...]を取得
    if (labelMatch.Success && labelMatch.Value is PassedLabel or FailedLabel or SkippedLabel)
    {
      if (!message.StartsWith(Tab))
      {
        EndTestCase();
        EndTestCase();
      }
      var label = labelMatch.Value;
      var str = message.Split(label)[1].TrimStart();
      var name = Regex.Replace(str, @"\(.*?\)", "").Split(Space)[0]; // (...)と以降のメッセージを除去
      string fixture = null;
      if (testCaseStack.TryPeek(out var parent))
        fixture = parent.Attributes(NameAttribute).FirstOrDefault()?.Value;
      EnterTestCase(fixture, name);
      Evaluate(label, str);
    }
    else
    {
      failure?.Value += message;
    }
    return;

    void Evaluate(string label, string str)
    {
      switch (label)
      {
        case SkippedLabel:
          failure = new XElement(SkippedAttribute);
          failure.SetAttributeValue(MessageAttribute, str.Split(Space).ElementAtOrDefault(1));
          testCaseStack.Peek().Add(failure);
          break;

        case FailedLabel:
          failure = new XElement(FailureAttribute);
          failure.SetAttributeValue(MessageAttribute, str.Split(Space).ElementAtOrDefault(1));
          testCaseStack.Peek().Add(failure);
          break;

        case PassedLabel:
          foreach (var testCase in testCaseStack)
          {
            testCase.assertions++;
          }
          testsuite.assertions++;
          break;
      }
    }
  }

  void ILogWriter.Flush()
  {
    writer.Flush();
  }

  void IDisposable.Dispose()
  {
    WriteTestResults();
    // ファイルを空にする
    writer.BaseStream.SetLength(0);
    writer.BaseStream.Position = 0;
    document.Save(writer);
    fileStream.Dispose();
    testsuite.RemoveAll();
    testCaseStack.Clear();
    failure = null;
    return;

    void WriteTestResults()
    {
      while (testCaseStack.Count > 0)
      {
        EndTestCase();
      }
      testsuite.SetAttributeValue(TestsAttribute, testsuite.Elements(TestCaseName).Count());
      testsuite.SetAttributeValue(SkippedAttribute,
        testsuite.Elements(TestCaseName)
          .SelectMany(testcase => testcase.Elements().Where(e => e.Name == SkippedAttribute)).Count());
      testsuite.SetAttributeValue(FailureAttribute,
        testsuite.Elements(TestCaseName)
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
    failure = null;
  }

  private void EndTestCase()
  {
    if (testCaseStack.TryPop(out var testCase))
      testCase.SetAttributeValue(AssertionAttribute, testCase.assertions);

    failure = null;
  }

  private class XTestCase(XName name) : XElement(name)
  {
    public int assertions;
  }

  private class XTestSuite(XName name) : XTestCase(name);
}
