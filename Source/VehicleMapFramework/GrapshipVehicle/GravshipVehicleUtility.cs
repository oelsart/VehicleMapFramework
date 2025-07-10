using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Vehicles;
using Verse;

namespace VehicleMapFramework
{
    public static class GravshipVehicleUtility
    {
        private readonly static Action<Def, Type, HashSet<ushort>> GiveShortHash = (Action<Def, Type, HashSet<ushort>>)AccessTools.Method(typeof(ShortHashGiver), "GiveShortHash").CreateDelegate(typeof(Action<Def, Type, HashSet<ushort>>));

        private readonly static Dictionary<Type, HashSet<ushort>> takenHashesPerDeftype = AccessTools.StaticFieldRefAccess<Dictionary<Type, HashSet<ushort>>>(typeof(ShortHashGiver), "takenHashesPerDeftype");

        public static VehicleDef GenerateGravshipVehicleDef(VehicleMapProps_Gravship props)
        {
            //if (!ModsConfig.OdysseyActive) return null;

            var vehicleDef = GenerateInner(props);
            if (VehicleMod.GenerateImpliedDefs(vehicleDef, false))
            {
                vehicleDef.generated = true;
                vehicleDef.ResolveDefNameHash();
                ModContentPack modContentPack = vehicleDef.modContentPack;
                if (modContentPack != null)
                {
                    modContentPack.AddDef(vehicleDef, "ImpliedDefs");
                }
                vehicleDef.PostLoad();
            }
            return vehicleDef;
        }

        private static VehicleDef GenerateInner(VehicleMapProps_Gravship props)
        {
            var def = new VehicleDef();
            var baseDef = VMF_DefOf.VMF_GravshipVehicleBase;
            foreach (var field in typeof(VehicleDef).GetFields())
            {
                if (!field.IsLiteral) field.SetValue(def, field.GetValue(baseDef));
            }

            def.defName = props.DefName;
            def.label = "Gravship".Translate();
            def.size = props.size;
            def.graphicData = new GraphicDataRGB();
            def.graphicData.CopyFrom(baseDef.graphicData);
            def.graphicData.drawSize = props.size.ToVector2();
            def.shortHash = 4999;
            //GiveShortHash(def, typeof(VehicleDef), takenHashesPerDeftype[typeof(VehicleDef)]);
            def.modContentPack = VehicleMapFramework.mod.Content;
            def.modExtensions = [props];
            return def;
        }
    }
}
