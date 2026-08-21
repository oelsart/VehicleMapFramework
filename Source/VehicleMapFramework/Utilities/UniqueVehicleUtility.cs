using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;

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

  internal static readonly FastInvokeHandler GeneratePathData;

  static UniqueVehicleUtility()
  {
    var m_GeneratePathData = AccessTools.Method("Vehicles.PathDataContainer:CreatePathData");
    // TODO with VF Updates: PathData will be managed by PathDataContainer
    if (m_GeneratePathData is not null)
      GeneratePathData = MethodInvoker.GetHandler(m_GeneratePathData);
  }

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
    def.drawProperties = Gen.MemberwiseClone(def.drawProperties);
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

  public static bool AllowGenerate(VehicleDef def)
  {
    if (!def.IsUniqueVehicle) return true;
    var manager = Current.Game.GetComponent<UniqueVehicleManager>();
    if (manager is null) return false;
    return manager.ClaimedCount(def) < UniqueVehicleManager.PlaceholderDefs.TryGetValue(def)?.Count;
  }
}