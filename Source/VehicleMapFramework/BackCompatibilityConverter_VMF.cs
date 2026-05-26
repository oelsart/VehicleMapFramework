using System;
using System.Collections.Generic;
using System.Xml;
using HarmonyLib;
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
    if (defType == typeof(ThingDef) && defName == "VMF_WirelessReceiver")
      return "VMF_WirelessTransmitter";
    return null;
  }

  public override Type GetBackCompatibleType(Type baseType, string providedClassName, XmlNode node)
  {
    if (baseType == typeof(Thing) && providedClassName == "VehicleMapFramework.ExplosionAcrossMaps")
      return typeof(Explosion);
    return null;
  }

  public override void PostExposeData(object obj) { }
}
