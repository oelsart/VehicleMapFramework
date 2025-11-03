using YamlDotNet.Serialization;

namespace Test_CompatPatches;

public static class TestPlanLoader
{
    public static Dictionary<string, string> WorkshopIds { get; private set; } 
    
    private const string WorkshopIdsFileName = "WorkshopIds.yml";
    
    private const string TestPlansFileName = "TestPlans.yml";

    public static IEnumerable<TestCaseData> GetTestPlans()
    {
        var deserializer = new DeserializerBuilder().Build();
        var workshopIdsYaml = File.ReadAllText(Path.Combine(Configurations.TestProjectRoot, WorkshopIdsFileName));
        WorkshopIds = deserializer.Deserialize<Dictionary<string, string>>(workshopIdsYaml);
        var testPlansYaml = File.ReadAllText(Path.Combine(Configurations.TestProjectRoot, TestPlansFileName));
        foreach (var plan in deserializer.Deserialize<List<TestPlan>>(testPlansYaml))
        {
            plan.Mods ??= [plan.Name];
            plan.Categories ??= [$"VMF_Patches_{plan.Name}"];
            var testCateData = new TestCaseData(plan);
            testCateData.SetName(plan.Name);
            yield return testCateData;
        }
    }
}