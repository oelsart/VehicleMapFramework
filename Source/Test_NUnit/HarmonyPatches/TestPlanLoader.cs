using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using ModAssemblyLoader;
using NUnit.Framework.Interfaces;
using YamlDotNet.Serialization;

namespace VehicleMapFramework.Test_CompatPatches;

public static class TestPlanLoader
{
    public static AssemblyLoader Loader { get; }
    
    public static Type[] Types { get; private set; }
    
    public static Type ModCompatType { get; private set; }
    
    public static Dictionary<string, string> WorkshopIds { get; } 
    
    private static readonly List<TestPlan> testPlans;

    static TestPlanLoader()
    {
        // ロードの初期化
        AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
        {
            var fileName = $"{assemblyName.Name}.dll";
            var path = Path.Combine(Configurations.RimWorldAssemblyFolder, fileName);
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Loader = new AssemblyLoader(
            Configurations.Version,
            Configurations.SteamWorkshopRoot,
            Configurations.LocalModsRoot);
        
        // デシリアライズ
        var deserializer = new DeserializerBuilder().Build();
        var workshopIdsYaml = File.ReadAllText(Path.Combine(Configurations.TestProjectRoot, Configurations.WorkshopIdsFileName));
        WorkshopIds = deserializer.Deserialize<Dictionary<string, string>>(workshopIdsYaml);
        var testPlansYaml = File.ReadAllText(Path.Combine(Configurations.TestProjectRoot, Configurations.TestPlansFileName));
        testPlans = deserializer.Deserialize<List<TestPlan>>(testPlansYaml);
        foreach (var plan in testPlans)
        {
            plan.Mods ??= [plan.Name];
            plan.Categories ??= [$"VMF_Patches_{plan.Name}"];
        }
        
        // VMFロードと型キャッシュ
        var assemblies = Configurations.IsRemote
            ? Loader.LoadModFolder(WorkshopIds["VehicleMapFramework"])
            : Loader.LoadModFolder("VehicleMapFramework");
        Types = assemblies
            .SelectMany(AccessTools.GetTypesFromAssembly)
            .Where(type => type.FullName?.Contains("Patch") ?? false).ToArray();
    }
    
    public static IEnumerable<TestCaseData> GetLoadTestPlans()
    {
        foreach (var plan in testPlans)
        {
            var testCaseData = new TestCaseData(plan);
            testCaseData.SetName($"Load: {plan.Name}");
            yield return testCaseData;
        }
    }

    public static IEnumerable<TestCaseData> GetModCompatTestPlans()
    {
        ModCompatType = AccessTools.TypeByName("VehicleMapFramework.ModCompat");
        foreach (var type in ModCompatType.InnerTypes())
        {
            if (type.IsDefined(typeof(CompilerGeneratedAttribute)))
                continue;
            var testCaseData = new TestCaseData(type);
            testCaseData.SetName($"ModCompat: {type.Name}");
            yield return testCaseData;
        }
    }
    
    public static IEnumerable<TestCaseData> GetPatchTestPlans()
    {
        if (!Configurations.IsRemote)
            yield return new TestCaseData(new TestPlan()).SetName("Patch: VehicleMapFramework");
        foreach (var plan in testPlans.Where(plan => !plan.LoadOnly))
            yield return new TestCaseData(plan).SetName($"Patch: {plan.Name}");
    }
}