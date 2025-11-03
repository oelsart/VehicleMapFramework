using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using ModAssemblyLoader;

namespace Test_CompatPatches;

[TestFixture]
public class HarmonyPatchTests
{
    private AssemblyLoader loader;

    private Type[] types;

    private Harmony harmony;
    
    private const string HarmonyId = "VehicleMapFramework.HarmonyPatchTests";
    
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

        AccessTools.PropertySetter("VehicleMapFramework.UnitTestDetector:IsTestingContext")
            .Invoke(null, [true]);
        harmony = new Harmony(HarmonyId);
        harmony.Patch(
            AccessTools.Method("Verse.GenTypes:GetTypeInAnyAssembly"),
            AccessTools.Method(typeof(HarmonyPatchTests), nameof(TypeByName)));
    }

    private static bool TypeByName(string typeName, ref Type __result)
    {
        _ = __result;
        __result = AccessTools.TypeByName(typeName);
        return false;
    }

    [OneTimeTearDown]
    public void OneTimeTearDown()
    {
        AccessTools.PropertySetter("VehicleMapFramework.UnitTestDetector:IsTestingContext")
            .Invoke(null, [false]);
        harmony.UnpatchAll(HarmonyId);
    }
    
    [Order(1)]
    [Test]
    [TestCaseSource(typeof(TestPlanLoader), nameof(TestPlanLoader.GetTestPlans))]
    public void LoadAssemblies(TestPlan plan)
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
    }

    [Order(2)]
    [Test]
    public void InitializeModCompatClass()
    {
        var t_ModCompat = AccessTools.TypeByName("VehicleMapFramework.ModCompat");
        RuntimeHelpers.RunClassConstructor(t_ModCompat.TypeHandle);
        var exceptions = (List<Exception>)AccessTools.PropertyGetter(t_ModCompat, "CctorExceptions").Invoke(null, null);
        Assert.That(exceptions, Is.Empty, string.Join("\n\n", exceptions!));
    }

    [Order(3)]
    [Test]
    [TestCaseSource(typeof(TestPlanLoader), nameof(TestPlanLoader.GetTestPlans))]
    public void ExecutePatches(TestPlan plan)
    {
        var harmony = new Harmony($"VehicleMapFramework.CompatPatchesTest: {plan.Name}");
        Assert.DoesNotThrow(() =>
        {
            foreach (var category in plan.Categories)
            {
                PatchCategory(category);
            }
        });
        Assert.Pass($"Successfully applied {harmony.GetPatchedMethods().Count()} patches.");
        return;
    
        void PatchCategory(string category)
        {
            types.Where(type =>
            {
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