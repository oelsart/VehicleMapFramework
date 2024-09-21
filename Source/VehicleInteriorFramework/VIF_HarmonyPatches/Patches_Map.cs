using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(Map), nameof(Map.MapUpdate))]
    public static class Patch_Map_MapUpdate
    {
        public static void Postfix(Map __instance)
        {
            if ((__instance.Parent is MapParent_Vehicle parentVehicle) && Find.CurrentMap == parentVehicle.vehicle.Map)
            {
                PlantFallColors.SetFallShaderGlobals(__instance);
                __instance.waterInfo.SetTextures();
                __instance.avoidGrid.DebugDrawOnMap();
                BreachingGridDebug.DebugDrawAllOnMap(__instance);
                __instance.mapDrawer.MapMeshDrawerUpdate_First();
                __instance.powerNetGrid.DrawDebugPowerNetGrid();
                DoorsDebugDrawer.DrawDebug();
                __instance.mapDrawer.DrawMapMesh();
                __instance.dynamicDrawManager.DrawDynamicThings();
                __instance.gameConditionManager.GameConditionManagerDraw(__instance);
                MapEdgeClipDrawer.DrawClippers(__instance);
                __instance.designationManager.DrawDesignations();
                __instance.overlayDrawer.DrawAllOverlays();
                __instance.temporaryThingDrawer.Draw();
                __instance.flecks.FleckManagerDraw();
            }
        }
    }
}
