using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyLib;
using RimWorld;
using SmashTools;
using Vehicles;
using Verse;

// TODO VF Updates 1.7: VehiclePathingSystem.VehiclePathDataはVehicles.PathDataに変更予定
#pragma warning disable CS0618 // 型またはメンバーが旧型式です

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public static class UniqueVehicleUtility
{
  private static readonly Action<Def, Type, HashSet<ushort>> GiveShortHash =
    (Action<Def, Type, HashSet<ushort>>)AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash")
      .CreateDelegate(typeof(Action<Def, Type, HashSet<ushort>>));

  private static readonly Dictionary<Type, HashSet<ushort>> takenHashesPerDeftype =
    AccessTools.StaticFieldRefAccess<Dictionary<Type, HashSet<ushort>>>(typeof(ShortHashGiver),
      "takenHashesPerDeftype");
  
#if DEV
  private static readonly FastInvokeHandler CreatePathData;
  private static readonly Func<VehiclePathingSystem, PathFinderManager> PathFinderManager;
  private static readonly AccessTools.FieldRef<PathDataContainer, VehiclePathingSystem.VehiclePathData[]> pathDatas;
  private static readonly Type PathFinderImpl;
  private static readonly AccessTools.FieldRef<PathFinderManager, IList> pathFinders;
  private static bool Active { get; }

  static UniqueVehicleUtility()
  {
    var m_GeneratePathData = AccessTools.Method("Vehicles.PathDataContainer:CreatePathData");
    if (m_GeneratePathData is not null)
      CreatePathData = MethodInvoker.GetHandler(m_GeneratePathData);
    var g_PathFinderManager = AccessTools.PropertyGetter(typeof(VehiclePathingSystem), "PathFinderManager");
    if (g_PathFinderManager is not null)
      PathFinderManager = AccessTools.MethodDelegate<Func<VehiclePathingSystem, PathFinderManager>>(g_PathFinderManager);
    pathDatas = AccessTools.FieldRefAccess<PathDataContainer, VehiclePathingSystem.VehiclePathData[]>("pathDatas");
    PathFinderImpl = GenTypes.GetTypeInAnyAssembly("Vehicles.PathFinderManager+PathFinderImpl");
    pathFinders = AccessTools.FieldRefAccess<PathFinderManager, IList>("pathFinders");
    if (CreatePathData is null || PathFinderManager is null || pathDatas is null || PathFinderImpl is null ||
        pathFinders is null)
    {
      VMF_Log.Error("Failed to initialize UniqueVehicleUtility.");
      return;
    }

    Active = true;
  }
#endif

  extension(VehicleDef def)
  {
    public bool IsUniqueVehicle => def.HasModExtension<VehicleMapProps_Unique>();
  }

  private static string GetDefName(VehicleDef parentDef, int index) => $"{index.ToString()}_{parentDef.defName}";

  public static VehicleDef GenerateUniqueVehicleDef(VehicleDef parentDef, int index)
  {
    var vehicleDef = DefDatabase<VehicleDef>.GetNamedSilentFail(GetDefName(parentDef, index));
    var hotReload = vehicleDef is not null;
    vehicleDef ??= GenerateInner(parentDef, index);

    VehicleMod.GenerateImpliedDefs(vehicleDef, hotReload);
    DefGenerator.AddImpliedDef(vehicleDef, hotReload);
    if (!hotReload) DefDatabase<ThingDef>.Add(vehicleDef);
    return vehicleDef;
  }

  private static VehicleDef GenerateInner(VehicleDef parentDef, int index)
  {
    if (parentDef.GetModExtension<VehicleMapProps_Unique>() is not { } props)
      return parentDef;

    var def = Gen.MemberwiseClone(parentDef);
    def.defName = GetDefName(parentDef, index);
    def.graphicData = new GraphicDataRGB();
    def.graphicData.CopyFrom(parentDef.graphicData);
    def.properties = Gen.MemberwiseClone(def.properties);
    if (parentDef.components is not null)
    {
      def.components = [];
      foreach (var component in parentDef.components)
      {
        var clone = Gen.MemberwiseClone(component);
        clone.hitbox = Gen.MemberwiseClone(component.hitbox);
        def.components.Add(clone);
      }
    }

    var newProps = Gen.MemberwiseClone(props);
    newProps.baseDef = parentDef;
    def.modExtensions = [.. parentDef.modExtensions];
    def.modExtensions.Remove(props);
    def.modExtensions.Add(newProps);
    def.comps ??= [];
    def.comps.Add(new CompProperties { compClass = typeof(CompVehicleDrawOffset) });
    def.shortHash = 0;
    GiveShortHash(def, typeof(ThingDef), takenHashesPerDeftype[typeof(ThingDef)]);
    return def;
  }

  public static VehicleDef ClaimUniqueVehicleDef(VehicleDef parentDef)
  {
    return Current.Game.GetComponent<UniqueVehicleManager>()?.ClaimUniqueVehicleDef(parentDef) ?? parentDef;
  }

  public static void ReleaseUniqueVehicleDef(VehicleDef def)
  {
    Current.Game.GetComponent<UniqueVehicleManager>()?.ReleaseUniqueVehicleDef(def);
  }

  public static void ReinitializeComponents(VehicleDef def)
  {
    if (def.components is null) return;

    foreach (var component in def.components)
    {
      component.hitbox.Hitbox.Clear();
      component.hitbox.Initialize(def);
    }
  }

  [Conditional("DEV")]
  public static void GeneratePathData(VehicleDef def)
  {
#if DEV
    if (!Active) return;
    
    var calculator =
      Activator.CreateInstance(GenTypes.GetTypeInAnyAssembly("Vehicles.PathGridCalculator", "Vehicles"));
    foreach (var map in Find.Maps)
    {
      if (map.IsVehicleMap) continue;
        
      var component = map.GetCachedMapComponent<VehiclePathingSystem>();
      var pathData = (VehiclePathingSystem.VehiclePathData)CreatePathData.Invoke(component.PathData,
        Params<(object, object, object)>.Get((calculator, def, component.PathFinder)));
      pathDatas(component.PathData)[def.DefIndex] = pathData;
      var pathFinderManager = PathFinderManager(component);
      pathFinders(pathFinderManager)[def.DefIndex] =
        Activator.CreateInstance(PathFinderImpl, Params<(object, object)>.Get((pathFinderManager, def)));
    }
#endif
  }

  public static bool AllowGenerate(VehicleDef def)
  {
    if (!def.IsUniqueVehicle) return true;
    var manager = Current.Game.GetComponent<UniqueVehicleManager>();
    if (manager is null) return false;
    return manager.ClaimedCount(def) < UniqueVehicleManager.PlaceholderDefs.TryGetValue(def)?.Count;
  }
}