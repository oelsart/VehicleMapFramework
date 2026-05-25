using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security;
using HarmonyLib;

namespace VehicleMapFramework.Test_CompatPatches;

[TestFixture]
[Category("Local")]
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
      AccessTools.Method("Verse.GenTypes:GetTypeInAnyAssembly"),
      AccessTools.Method(typeof(HarmonyPatchTests), nameof(TypeByName)));

    harmony.Patch(
      AccessTools.Method("Verse.GenTypes:AllSubclasses"),
      AccessTools.Method(typeof(HarmonyPatchTests), nameof(AllSubclasses)));

    harmony.Patch(
      AccessTools.Method("Verse.GenTypes:AllSubclassesNonAbstract"),
      AccessTools.Method(typeof(HarmonyPatchTests), nameof(AllSubclassesNonAbstract)));

    harmony.Patch(
      AccessTools.Method(typeof(Transpilers), nameof(Transpilers.MethodReplacer)),
      postfix: AccessTools.Method(typeof(HarmonyPatchTests), nameof(AssertReplaced)));

    harmony.Patch(
      AccessTools.Method("Verse.GenCollection:FirstOrDefault").MakeGenericMethod(typeof(object)),
      AccessTools.Method(typeof(HarmonyPatchTests), nameof(FirstOrDefault)));
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

  private static bool TypeByName(string typeName, out Type __result)
  {
    __result = AccessTools.TypeByName(typeName);
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

  private static readonly MethodInfo m_GetExecutingAssembly =
    AccessTools.Method(typeof(Assembly), nameof(Assembly.GetExecutingAssembly), []);

  private static void AssertReplaced(MethodBase from, MethodBase to, IEnumerable<CodeInstruction> __result)
  {
    if (from == m_GetExecutingAssembly)
      return;
    Assert.That(__result.Any(c => c.operand as MethodBase == to));
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
  [Order(1)]
  [TestCaseSource(typeof(TestPlanLoader), nameof(TestPlanLoader.GetLoadTestPlans))]
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
        Assert.DoesNotThrow(() => assemblies2 = TestPlanLoader.Loader.LoadModFolder(TestPlanLoader.WorkshopIds[mod]));
        Assert.That(assemblies2, Is.Not.Empty);
      }
    }
  }

  [Test]
  [Order(2)]
  [TestCaseSource(typeof(TestPlanLoader), nameof(TestPlanLoader.GetModCompatTestPlans))]
  public void InitializeModCompatClass(Type type)
  {
    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    var threadLocal = (ThreadLocal<Exception>)AccessTools.PropertyGetter(TestPlanLoader.ModCompatType, "CctorException").Invoke(null, null);
    var exception = threadLocal!.Value;
    threadLocal.Value = null;
    Assert.That(exception, Is.Null);
  }

  [Test]
  [Order(3)]
  [TestCaseSource(typeof(TestPlanLoader), nameof(TestPlanLoader.GetPatchTestPlans))]
  public void ExecutePatches(TestPlan plan)
  {
    const string Royalty = "VMF_Patches_Royalty";
    const string Biotech = "VMF_Patches_Biotech";
    const string Anomaly = "VMF_Patches_Anomaly";
    const string Odyssey = "VMF_Patches_Odyssey";

    var harmonyLocal = new Harmony($"VehicleMapFramework.CompatPatchesTest: {plan.Name}");
    if (plan.Categories is null)
    {
      PatchAllUncategorized();
      PatchCategory(Royalty);
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
      TestPlanLoader.Types.Where(type =>
      {
        var attributes = type.GetCustomAttributesData();
        return
          attributes.Any(attr => attr.AttributeType == typeof(HarmonyPatch)) &&
          attributes.Any(attr => attr.AttributeType == typeof(HarmonyPatchCategory) &&
                                 attr.ConstructorArguments.Any(c => (string)c.Value == category));
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
      TestPlanLoader.Types.Where(type =>
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
      switch (ex)
      {
        // デバッガーがアタッチされている時はReadMethodBody時ECallメソッドのSecurityExceptionが出ない
        // そのためSecurityExceptionのスキップをスキップする
        case not null when Debugger.IsAttached:
        case HarmonyException { InnerException: not SecurityException }:
        case not SecurityException and not HarmonyException:
          Assert.Fail(ex?.ToString() ?? "");
          break;
      }
    }
  }
}
