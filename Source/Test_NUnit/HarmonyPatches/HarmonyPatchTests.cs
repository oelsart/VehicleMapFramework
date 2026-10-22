using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using static VehicleMapFramework.Test_HarmonyPatches.TestPlanLoader;

namespace VehicleMapFramework.Test_HarmonyPatches;

[TestFixture]
// [Category("Local")]
[Category("Remote")]
public class HarmonyPatchTests
{
  [OneTimeSetUp]
  public void OneTimeSetUp()
  {
    AccessTools.PropertySetter("VehicleMapFramework.UnitTestDetector:IsTestingContext")
      .Invoke(null, [true]);
    harmony = new Harmony(HarmonyId);
    harmony.Patch(
      AccessTools.PropertyGetter("Verse.GenTypes:AllTypes"),
      ((Delegate)AllTypes).Method);
    
    harmony.Patch(
      AccessTools.Method("Verse.GenTypes:GetTypeInAnyAssembly"),
      ((Delegate)TypeByName).Method);
    
    harmony.Patch(
      AccessTools.Method("Verse.GenTypes:AllSubclasses"),
      ((Delegate)AllSubclasses).Method);
    
    harmony.Patch(
      AccessTools.Method("Verse.GenTypes:AllSubclassesNonAbstract"),
      ((Delegate)AllSubclassesNonAbstract).Method);
    
    harmony.Patch(
      AccessTools.Method("VehicleMapFramework.VMF_HarmonyPatches.PatchHelper:MethodReplacer",
        [typeof(IEnumerable<CodeInstruction>), typeof((MethodBase, MethodBase)[])]),
      postfix: ((Delegate)AssertReplaced).Method);
  }

  [OneTimeTearDown]
  public void OneTimeTearDown()
  {
    AccessTools.PropertySetter("VehicleMapFramework.UnitTestDetector:IsTestingContext")
      .Invoke(null, [false]);
    harmony.UnpatchAll(HarmonyId);
  }

  private Harmony harmony;

  private const string HarmonyId = "VehicleMapFramework.HarmonyPatchTests";
  
  private static bool AllTypes(out List<Type> __result)
  {
    __result = [.. AccessTools.AllTypes()];
    return false;
  }

  private static bool TypeByName(string typeName, out Type __result)
  {
    __result = AccessTools.TypeByName(typeName);
    if (__result is null)
      Assert.Fail($"Type {typeName} is not found.");
    return false;
  }

  private static bool AllSubclasses(Type baseType, out List<Type> __result)
  {
    __result = AccessTools.AllTypes().AsParallel().Where(x => x.IsSubclassOf(baseType)).ToList();
    return false;
  }

  private static bool AllSubclassesNonAbstract(Type baseType, out List<Type> __result)
  {
    __result = AccessTools.AllTypes().AsParallel()
      .Where(x => x.IsSubclassOf(baseType) && !x.IsAbstract).ToList();
    return false;
  }

  private static readonly MethodInfo m_GetExecutingAssembly = ((Delegate)Assembly.GetExecutingAssembly).Method;

  private static void AssertReplaced((MethodBase from, MethodBase to)[] pairs, List<CodeInstruction> __result)
  {
    var methods = __result.Select(c => c.operand).OfType<MethodBase>().ToList();
    foreach (var (from, to) in pairs)
    {
      if (from == m_GetExecutingAssembly)
        return;
      Assert.Contains(to, methods);
    }
  }

  private static bool FirstOrDefault(IEnumerable<object> list, Predicate<object> predicate, out object __result)
  {
    __result = list.FirstOrDefault((Func<object, bool>)Func);
    return false;

    bool Func(object obj)
    {
      return predicate(obj);
    }
  }

  [Test]
  [TestCaseSource(typeof(TestPlanLoader), nameof(GetLoadTestPlans))]
  public void LoadAssemblies(TestPlan plan)
  {
    using (Assert.EnterMultipleScope())
    {
      Assert.That(plan.Mods, Is.Not.Empty);
      Assert.That(plan.Categories, Is.Not.Empty);
    }

    List<Assembly> assemblies = [];
    foreach (var mod in plan.Mods)
    {
      List<Assembly> assemblies2 = null;
      using (Assert.EnterMultipleScope())
      {
        Assert.DoesNotThrow(() => assemblies2 = Loader.LoadModFolder(WorkshopIds[mod]));
        Assert.That(assemblies2, Is.Not.Empty);
      }
      assemblies.AddRange(assemblies2);
    }
    Assert.Pass($"Successfully loaded {assemblies.Count} assemblies.\n{string.Join("\n", assemblies)}");
  }

  [Test]
  [DependsOnTest("LoadAssemblies", AllowFailure = true)]
  [TestCaseSource(typeof(TestPlanLoader), nameof(GetModCompatTestPlans))]
  public void InitializeModCompatClass(Type type)
  {
    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    var threadLocal = (ThreadLocal<Exception>)AccessTools.PropertyGetter(ModCompatType, "CctorException").Invoke(null, null);
    var exception = threadLocal!.Value;
    threadLocal.Value = null;
    Assert.That(exception, Is.Null);
  }
  
  [Test]
  [DependsOnTest("InitializeModCompatClass", AllowFailure = true)]
  [TestCaseSource(typeof(TestPlanLoader), nameof(GetPatchTestPlans))]
  public void ExecutePatches(TestPlan plan)
  {
    const string Royalty = "VMF_Patches_Royalty";
    const string Ideology = "VMF_Patches_Ideology";
    const string Biotech = "VMF_Patches_Biotech";
    const string Anomaly = "VMF_Patches_Anomaly";
    const string Odyssey = "VMF_Patches_Odyssey";

    var harmonyLocal = new Harmony($"VehicleMapFramework.CompatPatchesTest: {plan.Name}");
    if (plan.Categories is null)
    {
      PatchAllUncategorized();
      PatchCategory(Royalty);
      PatchCategory(Ideology);
      PatchCategory(Biotech);
      PatchCategory(Anomaly);
      PatchCategory(Odyssey);
    }
    else
    {
      foreach (var category in plan.Categories)
      {
        PatchCategory(category);
      }
    }
    Assert.Pass($"Successfully applied {harmonyLocal.GetPatchedMethods().Count()} patches.");
    return;

    void PatchCategory(string category)
    {
      Types.Where(type =>
      {
        var attributes = type.GetCustomAttributesData();
        return
          attributes.Any(attr => attr.AttributeType == typeof(HarmonyPatch)) &&
          attributes.Any(attr => attr.AttributeType == typeof(HarmonyPatchCategory) &&
                                 attr.ConstructorArguments.Any(c => (string)c.Value == category)) &&
          attributes.All(attr => attr.AttributeType.Name != "VFVersionalPatchAttribute");
      }).Do(type =>
      {
        try
        {
          harmonyLocal.CreateClassProcessor(type).Patch();
        }
        catch (Exception ex)
        {
          HandleException(ex);
        }
      });
    }

    void PatchAllUncategorized()
    {
      Types.Where(type =>
      {
        var attributes = type.GetCustomAttributesData();
        return
          attributes.Any(attr => attr.AttributeType == typeof(HarmonyPatch)) &&
          attributes.All(attr => attr.AttributeType != typeof(HarmonyPatchCategory));
      }).Do(type =>
      {
        try
        {
          harmonyLocal.CreateClassProcessor(type).Patch();
        }
        catch (Exception ex)
        {
          HandleException(ex);
        }
      });
    }

    void HandleException(Exception ex)
    {
      Assert.Fail(ex?.ToString() ?? "");
    }
  }
}
