using HarmonyLib;
using RimWorld;
using System.Linq;
using Verse;

namespace VehicleInteriors.VIF_HarmonyPatches
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

        public const string prefix = "VIF_";

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
}
