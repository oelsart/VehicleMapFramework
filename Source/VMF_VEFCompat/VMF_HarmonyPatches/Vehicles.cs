using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SmashTools;
using UnityEngine;
using Vehicles;
using Verse;
using static VehicleMapFramework.ModCompat.VVE;
using Transform = SmashTools.Rendering.Transform;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[HarmonyPatchCategory(PatchCategories.VVE)]
[HarmonyPatch("VanillaVehiclesExpanded.GarageDoor", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_GarageDoor_DrawAt
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        //Graphics.DrawMesh(MeshPool.GridPlane(size), drawPos, base.Rotation.AsQuat, this.def.graphicData.GraphicColoredFor(this).MatAt(base.Rotation, this), 0);
        //this.Graphic.ShadowGraphic?.DrawWorker(drawPos, base.Rotation, this.def, this, 0f);
        //↓
        //Graphics.DrawMesh(MeshPool.GridPlane(size), RotateOffset(drawPos, this), this.BaseFullRotation().AsQuat(), this.def.graphicData.GraphicColoredFor(this).MatAt(this.BaseRotation(), this), 0);
        //this.Graphic.ShadowGraphic?.DrawWorker(drawPos, this.BaseFullRotation(), this.def, this, 0f);
        return new CodeMatcher(instructions, generator)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Rot4_AsQuat))
            .SetOperandAndAdvance(CachedMethodInfo.m_Rot8_AsQuatRef)
            .CreateLabel(out var label)
            .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
            .DeclareLocal(typeof(float), out var rotation)
            .Insert(
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Ldloca_S, vehicle),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
                new CodeInstruction(OpCodes.Brfalse_S, label),
                new CodeInstruction(OpCodes.Ldloc_S, vehicle),
                new CodeInstruction(OpCodes.Callvirt,
                    AccessTools.PropertyGetter(typeof(VehiclePawn), nameof(VehiclePawn.Transform))),
                CodeInstruction.LoadField(typeof(Transform), nameof(Transform.rotation)),
                new CodeInstruction(OpCodes.Stloc_S, rotation),
                new CodeInstruction(OpCodes.Ldloc_S, rotation),
                new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(Vector3), nameof(Vector3.up))),
                CodeInstruction.Call(typeof(Quaternion), nameof(Quaternion.AngleAxis)),
                new CodeInstruction(OpCodes.Call, CachedMethodInfo.o_Quaternion_Multiply))
            .MatchStartBackwards(CodeMatch.Calls(CachedMethodInfo.g_Thing_Rotation))
            .SetOperandAndAdvance(CachedMethodInfo.m_BaseFullRotation_Thing)
            .MatchStartBackwards(new CodeMatch(OpCodes.Ldarg_0))
            .Insert(
                CodeInstruction.LoadArgument(0),
                CodeInstruction.Call(typeof(Patch_GarageDoor_DrawAt), nameof(RotateOffset))
            )
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Rotation))
            .SetOperandAndAdvance(CachedMethodInfo.m_BaseRotation)
            .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Rotation))
            .SetOperandAndAdvance(CachedMethodInfo.m_BaseFullRotation_Thing)
            .MatchStartForward(CodeMatch.LoadsConstant(0f))
            .Set(OpCodes.Ldloc_S, rotation)
            .InstructionEnumeration();
    }

    private static Vector3 RotateOffset(Vector3 point, Building garageDoor)
    {
        return garageDoor.IsOnNonFocusedVehicleMapOf(out var vehicle) ? Ext_Math.RotatePoint(point, garageDoor.DrawPos, -vehicle.FullAngle) : point;
    }
}

[HarmonyPatchCategory(PatchCategories.VVE)]
[HarmonyPatch("VanillaVehiclesExpanded.CompRefuelingPump", "CompTick")]
[PatchLevel(Level.Safe)]
public static class Patch_CompRefuelingPump_CompTick
{
    public static void Postfix(ThingWithComps ___parent, CompRefuelable ___compRefuelable, CompProperties ___props)
    {
        if (!___parent.Spawned) return;
        var fuelTank = ___parent.InteractionCell.GetThingList(___parent.Map).FirstOrDefault(t => t.TryGetComp(out CompFuelTank _));
        if (fuelTank == null || !___compRefuelable.HasFuel || !fuelTank.IsOnVehicleMapOf(out var vehicle)) return;
        var compFueledTravel = vehicle.CompFueledTravel;
        if (compFueledTravel == null || !(compFueledTravel.Fuel < compFueledTravel.FuelCapacity) ||
            compFueledTravel.FuelLeaking) return;
        var amount = Mathf.Min(compFueledTravel.FuelCapacity - compFueledTravel.Fuel, refuelAmountPerTick(___props));
        compFueledTravel.Refuel(amount);
        ___compRefuelable.ConsumeFuel(amount);
    }
}