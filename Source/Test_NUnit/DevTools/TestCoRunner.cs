using System.Diagnostics;
using System.Xml.Linq;
using Microsoft.VisualBasic;

namespace VehicleMapFramework.Test_DevTools;

internal class TestCoRunner
{
    internal const string RiMWorldPath = "E:/Program Files (x86)/Steam/steamapps/common/RimWorld/RimWorldWin64.exe";

    internal const string Arguments = "-disable-compute-shaders --pid \"OELS.VehicleMapFramework.dev\" -t -e --logger junit";

    internal const string ResultPath =
        "E:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/VehicleMapFramework/.git/testresults/Test.log";
    
    internal static readonly string ConfigPath =
        $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)[..^6]}/LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config";

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
        var modsConfigPath = Path.Combine(ConfigPath, "ModsConfig.xml");
        var backupPath = $"{modsConfigPath}.bak";
        var testConfigPath = Path.Combine(ConfigPath, "_TEST_MODLIST.xml");
        if (!File.Exists(modsConfigPath) || !File.Exists(testConfigPath))
            yield break;
        File.Replace(testConfigPath, modsConfigPath, backupPath);
        
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

        FileSystem.FileCopy(modsConfigPath, testConfigPath);
        File.Replace(backupPath, modsConfigPath, null);
    }
}