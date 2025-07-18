using HarmonyLib;
using RimWorld;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleMapFramework.MethodInfoCache;
using static VehicleMapFramework.ModCompat.VVE;

namespace VehicleMapFramework.VMF_HarmonyPatches
{
    [HarmonyPatchCategory("VMF_Patches_VVE")]
    [HarmonyPatch("VanillaVehiclesExpanded.GarageDoor", "DrawAt")]
    public static class Patch_GarageDoor_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            //Graphics.DrawMesh(MeshPool.GridPlane(size), drawPos, base.Rotation.AsQuat, this.def.graphicData.GraphicColoredFor(this).MatAt(base.Rotation, this), 0);
            //this.Graphic.ShadowGraphic?.DrawWorker(drawPos, base.Rotation, this.def, this, 0f);
            //↓
            //Graphics.DrawMesh(MeshPool.GridPlane(size), RotateOffset(drawPos, this), this.BaseFullRotation().AsQuat(), this.def.graphicData.GraphicColoredFor(this).MatAt(this.BaseRotation(), this), 0);
            //this.Graphic.ShadowGraphic?.DrawWorker(drawPos, this.BaseFullRotation(), this.def, this, 0f);
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Rot4_AsQuat));
            codes[pos].operand = CachedMethodInfo.m_Rot8_AsQuatRef;
            pos = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Thing_Rotation));
            codes[pos].operand = CachedMethodInfo.m_BaseFullRotation_Thing;
            pos = codes.FindLastIndex(pos, c => c.opcode == OpCodes.Ldarg_0);
            codes.InsertRange(pos,
            [
                CodeInstruction.LoadArgument(0),
            CodeInstruction.Call(typeof(Patch_GarageDoor_DrawAt), nameof(RotateOffset))
            ]);
            pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Thing_Rotation));
            codes[pos].operand = CachedMethodInfo.m_BaseRotation;
            pos = codes.FindIndex(pos, c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.g_Thing_Rotation));
            codes[pos].operand = CachedMethodInfo.m_BaseFullRotation_Thing;
            return codes;
        }

        private static Vector3 RotateOffset(Vector3 point, Building garageDoor)
        {
            if (garageDoor.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                return Ext_Math.RotatePoint(point, garageDoor.DrawPos, -vehicle.FullRotation.AsAngle);
            }
            return point;
        }
    }

    [HarmonyPatchCategory("VMF_Patches_VVE")]
    [HarmonyPatch("VanillaVehiclesExpanded.CompRefuelingPump", "CompTick")]
    public static class Patch_CompRefuelingPump_CompTick
    {
        public static void Postfix(ThingWithComps ___parent, CompRefuelable ___compRefuelable, CompProperties ___props)
        {
            if (___parent.Spawned)
            {
                CompFuelTank compFuelTank = default;
                var fuelTank = ___parent.InteractionCell.GetThingList(___parent.Map).FirstOrDefault(t => t.TryGetComp(out compFuelTank));
                if (fuelTank != null && ___compRefuelable.HasFuel)
                {
                    CompFueledTravel compFueledTravel = compFuelTank.Vehicle?.CompFueledTravel;
                    if (compFueledTravel != null && compFueledTravel.Fuel < compFueledTravel.FuelCapacity)
                    {
                        float amount = Mathf.Min(compFueledTravel.FuelCapacity - compFueledTravel.Fuel, refuelAmountPerTick(___props));
                        compFueledTravel.Refuel(amount);
                        ___compRefuelable.ConsumeFuel(amount);
                    }
                }
            }
        }
    }
}
