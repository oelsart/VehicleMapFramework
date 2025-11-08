using System.Diagnostics;
using System.Xml.Linq;

namespace VehicleMapFramework.Test_DevTools;

internal class TestCoRunner
{
    internal const string RiMWorldPath = "E:/Program Files (x86)/Steam/steamapps/common/RimWorld/RimWorldWin64.exe";

    internal const string Arguments = "--pid OELS.VehicleMapFramework.dev --test -batchmode -e --logger junit";

    internal const string ResultPath =
        "E:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/VehicleMapFramework/.git/testresults/Test.log";

    internal static XElement[] testCases;
    
    public static void Run()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = RiMWorldPath,
            Arguments = Arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        var process = Process.Start(startInfo);
        Console.WriteLine($"{process}");
        process?.WaitForExit();
        ReadResult();
    }

    internal static void ReadResult()
    {
        var document = XDocument.Load(ResultPath);
        testCases = document.Descendants("testcase").ToArray();
    }
    
    public static IEnumerable<TestCaseData> TestResults()
    {
        Run();
        ReadResult();
        if (testCases is null)
            yield break;
        foreach (var testCase in testCases)
        {
            var testCaseData = new TestCaseData(testCase);
            testCaseData.SetName($"{testCase.Attribute("name")?.Value}");
            yield return testCaseData;
        }
    }
}