using System;
using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class BackCompatibilityConverter_VMF : BackCompatibilityConverter
{
  static BackCompatibilityConverter_VMF()
  {
    ((List<BackCompatibilityConverter>)AccessTools.Field(typeof(BackCompatibility), "conversionChain").GetValue(null))
      .Add(new BackCompatibilityConverter_VMF());
  }

  public override bool AppliesToVersion(int majorVer, int minorVer)
  {
    return true;
  }

  public override string BackCompatibleDefName(Type defType, string defName, bool forDefInjections = false, XmlNode node = null)
  {
    if (defType == typeof(ThingDef))
    {
      if (defName == "VMF_WirelessReceiver")
        return "VMF_WirelessTransmitter";
      if (defName.StartsWith("GravshipVehicle"))
      {
        if (GetClaimedDef(VMF_DefOf.VMF_GravshipVehicleBase, out var vehicleDef) ||
            GetClaimedDef(VMF_DefOf.VMF_GravshipVehicleBaseSpace, out vehicleDef))
          return vehicleDef.defName;
      }
    }
    return null;

    bool GetClaimedDef(VehicleDef parentDef, out VehicleDef vehicleDef)
    {
      if (!UniqueVehicleManager.PlaceholderDefs.TryGetValue(parentDef, out var placeholderDefs))
      {
        vehicleDef = null;
        return false;
      }

      vehicleDef = placeholderDefs.FirstOrDefault(d => d.GetModExtension<VehicleMapProps_Gravship>() is { } props &&
                                                       props.defName == defName);
      return vehicleDef is not null;
    }
  }

  public override Type GetBackCompatibleType(Type baseType, string providedClassName, XmlNode node)
  {
    if (baseType == typeof(Thing) && providedClassName == "VehicleMapFramework.ExplosionAcrossMaps")
      return typeof(Explosion);
    return null;
  }

  public override void PostExposeData(object obj) { }
}
