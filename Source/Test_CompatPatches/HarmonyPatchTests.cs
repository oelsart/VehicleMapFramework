using System.Reflection;
using System.Runtime.Loader;
using HarmonyLib;
using ModAssemblyLoader;

namespace Test_CompatPatches;

[TestFixture]
public class HarmonyPatchTests
{
    private AssemblyLoader loader;

    private Type[] types;
    
    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        AssemblyLoadContext.Default.Resolving += (_, assemblyName) =>
        {
            var fileName = $"{assemblyName.Name}.dll";
            var path = Path.Combine(Configurations.RimWorldAssemblyFolder, fileName);
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        loader = new AssemblyLoader(
            Configurations.Version,
            Configurations.SteamWorkshopRoot,
            Configurations.LocalModsRoot);
        loader.LoadModFolder(TestPlanLoader.WorkshopIds["VehicleFramework"]);
        var assemblies = loader.LoadModFolder("VehicleMapFramework");
        types = assemblies
            .SelectMany(AccessTools.GetTypesFromAssembly)
            .Where(type => type.FullName?.Contains("Patch") ?? false).ToArray();
    }
    
    [Test]
    [TestCaseSource(typeof(TestPlanLoader), nameof(TestPlanLoader.GetTestPlans))]
    public void ExecuteTestPlan(TestPlan plan)
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(plan.Mods, Is.Not.Empty);
            Assert.That(plan.Categories, Is.Not.Empty);
        }

        foreach (var mod in plan.Mods)
        {
            List<Assembly> assemblies2 = null;
            using (Assert.EnterMultipleScope())
            {
                Assert.DoesNotThrow(() => assemblies2 = loader.LoadModFolder(TestPlanLoader.WorkshopIds[mod]));
                Assert.That(assemblies2, Is.Not.Empty);
            }
        }
        
        var harmony = new Harmony($"VehicleMapFramework.CompatPatchesTest: {plan.Name}");
        var lastType = default(Type);
        try
        {
            foreach (var category in plan.Categories)
            {
                PatchCategory(category);
            }
        }
        catch (Exception ex)
        {
            Assert.Fail($"{ex}, lastType: {lastType}");
        }
        TestContext.Out.WriteLine($"Successfully applied {harmony.GetPatchedMethods().Count()} patches.");
        return;
    
        void PatchCategory(string category)
        {
            types.Where(type =>
                {
                    lastType = type;
                    var attributes = type.GetCustomAttributesData();
                    return
                        attributes.Any(attr => attr.AttributeType == typeof(HarmonyPatch)) &&
                        attributes.Any(attr => attr.AttributeType == typeof(HarmonyPatchCategory) &&
                                               attr.ConstructorArguments.Any(c => (string)c.Value == category));
                }).Do(type =>
                {
                    harmony.CreateClassProcessor(type).Patch();
                });
        }
    }
}