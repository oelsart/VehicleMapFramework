using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using HarmonyLib;
using JetBrains.Annotations;
using Mono.Cecil;
using static VehicleMapFramework.Test.TestPlanLoader;

namespace VehicleMapFramework.Test;

[TestFixture]
[Category("Local")]
[Category("Remote")]
public class HarmonyPatchTests
{
  [OneTimeSetUp]
  public void OneTimeSetUp()
  {
    EnsureInitialized();
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
        Assert.DoesNotThrow(() => assemblies2 = Loader.LoadModFolder(WorkshopIds[mod], new AssemblyLoadContext(mod, true)));
        Assert.That(assemblies2, Is.Not.Empty);
      }
      assemblies.AddRange(assemblies2);
    }

    foreach (var assembly in assemblies)
    {
      LoadAndRewriteAssembly(assembly.Location);
    }
    Assert.Pass($"Successfully loaded {assemblies.Count} assemblies.\n{string.Join("\n", assemblies)}");
    return;
    
    static void LoadAndRewriteAssembly(string assemblyPath)
    {
      var targetName = AssemblyName.GetAssemblyName(assemblyPath).Name;
      if (AssemblyLoadContext.Default.Assemblies.Any(a => a.GetName().Name == targetName))
      {
        return;
      }
      
      var resolver = new DefaultAssemblyResolver();
      var baseDir = Configurations.RimWorldAssemblyFolder;
      resolver.AddSearchDirectory(baseDir);
      var assemblyDir = Path.GetDirectoryName(assemblyPath);
      if (!string.IsNullOrEmpty(assemblyDir) && assemblyDir != baseDir)
      {
        resolver.AddSearchDirectory(assemblyDir);
      }
      var readerParameters = new ReaderParameters
      {
        AssemblyResolver = resolver
      };
      
      using var assemblyDef = AssemblyDefinition.ReadAssembly(assemblyPath, readerParameters);
      var isModified = false;

      foreach (var reference in assemblyDef.MainModule.AssemblyReferences)
      {
        if (reference.Name.StartsWith("UnityEngine"))
        {
          reference.Name = "UnityStubs";
          isModified = true;
        }
      }
    
      if (isModified)
      {
        using var ms = new MemoryStream();
        assemblyDef.Write(ms);
        ms.Position = 0;
        AssemblyLoadContext.Default.LoadFromStream(ms);
        Console.WriteLine("Assembly modified.");
        return;
      }

      Assembly.LoadFrom(assemblyPath);
    }
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

    void HandleException([NotNull] Exception ex)
    {
      if (ex is HarmonyException harmonyException)
        ex = harmonyException.InnerException;
      Assert.That(ex is FileNotFoundException { FileName: { } fileName } && fileName.Contains("UnityEngine.CoreModule"),
        Is.True, ex?.ToString());
    }
  }
}
