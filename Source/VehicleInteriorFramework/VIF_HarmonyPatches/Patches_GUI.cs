using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Collections.Generic;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
{
    [HarmonyPatch(typeof(WorldInterface), nameof(WorldInterface.WorldInterfaceOnGUI))]
    public static class Patch_WorldInterface_WorldInterfaceOnGUI
    {
        public static void Postfix()
        {
            if (VehicleMapUtility.FocusedVehicle != null && !Find.WindowStack.IsOpen<MainTabWindow>() && !WorldRendererUtility.WorldRenderedNow)
            {
                GizmoGridDrawer.DrawGizmoGrid(new List<Gizmo> { new Command_FocusVehicleMap() }, 324f, out var mouseoverGizmo);
            }
        }
    }
}
