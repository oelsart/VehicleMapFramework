using System;
using System.Xml;
using Verse;

namespace VehicleMapFramework;

public class BackCompatibilityConverter_VMF : BackCompatibilityConverter
{
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
        return null;
    }

    public override void PostExposeData(object obj)
    {
    }
}
