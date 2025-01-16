using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [HarmonyPatch(typeof(Building_Door), "StuckOpen", MethodType.Getter)]
    public static class Patch_Building_Door_StuckOpen
    {
        public static void Postfix(Building_Door __instance, ref bool __result)
        {
            __result = __result && !(__instance is Building_VehicleRamp);
        }
    }

    [HarmonyPatch(typeof(CompInteractable), nameof(CompInteractable.CanInteract))]
    public static class Patch_CompInteractable_CanInteract
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_PositionHeld, MethodInfoCache.m_PositionHeldOnBaseMap)
                .MethodReplacer(MethodInfoCache.m_ReachabilityUtility_CanReach, MethodInfoCache.m_ReachabilityUtilityOnVehicle_CanReach);
        }
    }

    [HarmonyPatch(typeof(CompPowerPlantSolar), nameof(CompPowerPlantSolar.PostDraw))]
    public static class Patch_CompPowerPlantSolar_PostDraw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing)
                .MethodReplacer(MethodInfoCache.m_Rot4_Rotate, MethodInfoCache.m_Rot8_Rotate);
        }
    }

    [HarmonyPatch(typeof(CompPowerPlantWind), nameof(CompPowerPlantWind.PostDraw))]
    public static class Patch_CompPowerPlantWind_PostDraw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing)
                .MethodReplacer(MethodInfoCache.g_Rot4_FacingCell, MethodInfoCache.g_Rot8_FacingCell)
                .MethodReplacer(MethodInfoCache.g_Rot4_RighthandCell, MethodInfoCache.m_Rot8Utility_RighthandCell)
                .MethodReplacer(MethodInfoCache.m_Rot4_Rotate, MethodInfoCache.m_Rot8_Rotate)
                .MethodReplacer(MethodInfoCache.g_Rot4_AsQuat, MethodInfoCache.m_Rot8_AsQuatRef)
                .MethodReplacer(MethodInfoCache.m_IntVec3_ToVector3, MethodInfoCache.m_Rot8Utility_ToFundVector3);
        }
    }

    [HarmonyPatch(typeof(CompPowerPlantWind), nameof(CompPowerPlantWind.CompTick))]
    public static class Patch_CompPowerPlantWind_CompTick
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }
    }

    [HarmonyPatch(typeof(CompPowerPlantWind), "RecalculateBlockages")]
    public static class Patch_CompPowerPlantWind_RecalculateBlockages
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                CodeInstruction.LoadField(typeof(CompPowerPlantWind), nameof(CompPowerPlantWind.parent)),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_BaseMap_Thing),
                CodeInstruction.Call(typeof(Patch_CompPowerPlantWind_RecalculateBlockages), nameof(Patch_CompPowerPlantWind_RecalculateBlockages.Restrict))
            });

            return codes.MethodReplacer(MethodInfoCache.g_Thing_Position, MethodInfoCache.m_PositionOnBaseMap)
                .MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseRotation)
                .MethodReplacer(MethodInfoCache.g_Thing_Map, MethodInfoCache.m_BaseMap_Thing);
        }

        private static IEnumerable<IntVec3> Restrict(IEnumerable<IntVec3> enumerable, Map map)
        {
            return enumerable.Where(c => c.InBounds(map));
        }
    }

    [HarmonyPatch(typeof(Building_Battery), "DrawAt")]
    public static class Patch_Building_Battery_DrawAt
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing)
                .MethodReplacer(MethodInfoCache.m_Rot4_Rotate, MethodInfoCache.m_Rot8_Rotate);
        }
    }

    [HarmonyPatch(typeof(PlaceWorker_WindTurbine), nameof(PlaceWorker_WindTurbine.DrawGhost))]
    public static class Patch_PlaceWorker_WindTurbine_DrawGhost
    {
        public static void Prefix(ref IntVec3 center, ref Rot4 rot, Thing thing)
        {
            if (Command_FocusVehicleMap.FocusedVehicle != null)
            {
                center = center.OrigToVehicleMap(Command_FocusVehicleMap.FocusedVehicle);
                rot.AsInt += Command_FocusVehicleMap.FocusedVehicle.Rotation.AsInt;
            }
            if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle))
            {
                center = center.OrigToVehicleMap(vehicle);
                rot.AsInt += vehicle.Rotation.AsInt;
            }
        }
    }

    [HarmonyPatch(typeof(CompRefuelable), nameof(CompRefuelable.PostDraw))]
    public static class Patch_CompRefuelable_PostDraw
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            return instructions.MethodReplacer(MethodInfoCache.g_Thing_Rotation, MethodInfoCache.m_BaseFullRotation_Thing)
                .MethodReplacer(MethodInfoCache.m_Rot4_Rotate, MethodInfoCache.m_Rot8_Rotate);
        }
    }

    [HarmonyPatch(typeof(PlaceWorker_FuelingPort), nameof(PlaceWorker_FuelingPort.DrawFuelingPortCell))]
    public static class Patch_PlaceWorker_FuelingPort_DrawFuelingPortCell
    {
        public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = instructions.ToList();
            var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_0);
            codes.InsertRange(pos, new[]
            {
                CodeInstruction.LoadArgument(0),
                new CodeInstruction(OpCodes.Call, MethodInfoCache.m_FocusedDrawPosOffset)
            });
            return codes;
        }
    }

    [HarmonyPatch(typeof(TravelingTransportPods), nameof(TravelingTransportPods.Tick))]
    public static class Patch_TravelingTransportPods_Tick
    {
        public static void Postfix(TravelingTransportPods __instance)
        {
            if (__instance.arrivalAction is TransportPodsArrivalAction_LandInVehicleMap arrivalAction && arrivalAction.mapParent is MapParent_Vehicle mapParent)
            {
                __instance.destinationTile = mapParent.Tile;
            }
        }
    }
}
