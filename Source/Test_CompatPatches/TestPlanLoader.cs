using YamlDotNet.Serialization;

namespace Test_CompatPatches;

public static class TestPlanLoader
{
    public static readonly Dictionary<string, string> WorkshopIds;

    public static readonly List<TestPlan> TestPlans;
    
    private const string WorkshopIdsFileName = "WorkshopIds.yml";
    
    private const string TestPlansFileName = "TestPlans.yml";

    static TestPlanLoader()
    {
        var deserializer = new DeserializerBuilder().Build();
        var workshopIdsYaml = File.ReadAllText(Path.Combine(Configurations.TestProjectRoot, WorkshopIdsFileName));
        WorkshopIds = deserializer.Deserialize<Dictionary<string, string>>(workshopIdsYaml);
        var testPlansYaml = File.ReadAllText(Path.Combine(Configurations.TestProjectRoot, TestPlansFileName));
        TestPlans = deserializer.Deserialize<List<TestPlan>>(testPlansYaml);
        foreach (var plan in TestPlans)
        {
            plan.WorkshopIds = WorkshopIds;
        }
    }

    public static IEnumerable<TestPlan> GetTestPlans()
    {
        return TestPlans;
    }
}