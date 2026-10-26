using System.Diagnostics;
using System.Xml.Linq;

namespace VehicleMapFramework.Test;

internal class TestCoRunner
{
  internal const string RiMWorldPath = "E:/Program Files (x86)/Steam/steamapps/common/RimWorld/RimWorld.exe";

  internal const string Arguments = "--pid OELS.VehicleMapFramework.dev -t -e";

  internal static readonly string[] PackageIds =
  [
    "brrainz.harmony",
    "SmashPhil.VehicleFramework",
    "OELS.VehicleMapFramework.Dev",
    "OELS.MapVehicles",
    "Mehni.PickUpAndHaul"
  ];

  internal const string ResultPath =
    "E:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/VehicleMapFramework/.git/testresults/Test.log";

  internal static XElement[] testCases;

  public static void Run()
  {
    var startInfo = new ProcessStartInfo
    {
      FileName = RiMWorldPath,
      Arguments = $"{Arguments} -mod={string.Join(";", PackageIds)}",
      UseShellExecute = false,
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      CreateNoWindow = true
    };
    var process = Process.Start(startInfo);
    process?.WaitForExit();
    ReadResult();
  }

  internal static void ReadResult()
  {
    var document = XDocument.Load(ResultPath);
    testCases = [.. document.Descendants("testcase")];
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
