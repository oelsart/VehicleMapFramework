using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using System.Linq;
using Vehicles;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    [StaticConstructorOnStartup]
    public static class DefMessagesReplace
    {
        static DefMessagesReplace()
        {
            foreach (var workGiverDef in DefDatabase<WorkGiverDef>.AllDefs.Where(d => d.defName.StartsWith(prefix) && d.defName.EndsWith(suffix)))
            {
                var baseDefName = workGiverDef.defName.Replace(prefix, "").Replace(suffix, "");
                var baseDef = DefDatabase<WorkGiverDef>.GetNamedSilentFail(baseDefName);
                if (baseDef != null)
                {
                    workGiverDef.label = baseDef.label;
                    workGiverDef.verb = baseDef.verb;
                    workGiverDef.gerund = baseDef.gerund;
                }
            }

            foreach (var jobDef in DefDatabase<JobDef>.AllDefs.Where(d => d.defName.StartsWith(prefix) && d.defName.EndsWith(suffix)))
            {
                var baseDefName = jobDef.defName.Replace(prefix, "").Replace(suffix, "");
                var baseDef = DefDatabase<JobDef>.GetNamedSilentFail(baseDefName);
                if (baseDef != null)
                {
                    jobDef.label = baseDef.label;
                    jobDef.reportString = baseDef.reportString;
                }
            }
        }

        public const string prefix = "VMF_";

        public const string suffix = "AcrossMaps";
    }

    //WorkGiverDefのgiverClassを差し替え
    [HarmonyPatch(typeof(WorkGiverDef), nameof(WorkGiverDef.Worker), MethodType.Getter)]
    public static class Patch_WorkGiverDef_Worker
    {
        public static void Prefix(WorkGiverDef __instance, WorkGiver ___workerInt)
        {
            if (___workerInt != null) return;

            if (__instance.giverClass == typeof(WorkGiver_DoBill))
            {
                __instance.giverClass = typeof(WorkGiver_DoBillAcrossMaps);
            }
        }
    }

    [StaticConstructorOnStartup]
    public static class AddVehicleMapHolderComp
    {
        static AddVehicleMapHolderComp()
        {
            foreach (var worldObjectDef in DefDatabase<WorldObjectDef>.AllDefs.Where(d => typeof(Caravan).IsAssignableFrom(d.worldObjectClass) || typeof(AerialVehicleInFlight).IsAssignableFrom(d.worldObjectClass)))
            {
                worldObjectDef.comps.Add(new WorldObjectCompProperties_VehicleMapHolderComp());
            }
        }
    }
}
