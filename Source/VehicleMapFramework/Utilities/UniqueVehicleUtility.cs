using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public static class UniqueVehicleUtility
{
    private static readonly Action<Def, Type, HashSet<ushort>> GiveShortHash = (Action<Def, Type, HashSet<ushort>>)AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash").CreateDelegate(typeof(Action<Def, Type, HashSet<ushort>>));

    private static readonly Dictionary<Type, HashSet<ushort>> takenHashesPerDeftype = AccessTools.StaticFieldRefAccess<Dictionary<Type, HashSet<ushort>>>(typeof(ShortHashGiver), "takenHashesPerDeftype");

    public static VehicleDef GenerateUniqueVehicleDef(VehicleMapProps_Unique props)
    {
        VMF_Log.DebugMessage($"Generate VehicleDef: {props.defName}");
        var vehicleDef = GenerateInner(props);
        VehicleMod.GenerateImpliedDefs(vehicleDef, false);
        DefGenerator.AddImpliedDef(vehicleDef);
        DefDatabase<ThingDef>.Add(vehicleDef);
        return vehicleDef;
    }

    public static VehicleDef GenerateUniqueVehicleDef(VehiclePawn vehicle)
    {
        var baseProps = vehicle.def.GetModExtension<VehicleMapProps_Unique>();
        if (baseProps is null)
        {
            return null;
        }
        var props = new VehicleMapProps_Unique();
        foreach (var field in typeof(VehicleMapProps_Unique).GetFields())
        {
            if (!field.IsLiteral) field.SetValue(props, field.GetValue(baseProps));
        }
        props.defName = vehicle.def.defName + vehicle.ThingID + "_";
        props.baseDef = vehicle.VehicleDef;
        return GenerateUniqueVehicleDef(props);
    }

    private static VehicleDef GenerateInner(VehicleMapProps_Unique props)
    {
        var def = new VehicleDef();
        foreach (var field in typeof(VehicleDef).GetFields())
        {
            if (!field.IsLiteral) field.SetValue(def, field.GetValue(props.baseDef));
        }

        def.defName = props.defName;
        def.graphicData = new GraphicDataRGB();
        def.graphicData.CopyFrom(props.baseDef.graphicData);
        def.modExtensions = [.. props.baseDef.modExtensions.Where(e => e is not VehicleMapProps_Unique).AddItem(props)];
        def.shortHash = 0;
        GiveShortHash(def, typeof(ThingDef), takenHashesPerDeftype[typeof(ThingDef)]);
        return def;
    }
}
