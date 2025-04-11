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

            var loadTransporters = DefDatabase<WorkGiverDef>.GetNamedSilentFail("LoadTransporters");
            if (loadTransporters != null)
            {
                VMF_DefOf.VMF_LoadBuildableContainer.label = loadTransporters.label;
                VMF_DefOf.VMF_LoadBuildableContainer.verb = loadTransporters.verb;
                VMF_DefOf.VMF_LoadBuildableContainer.gerund = loadTransporters.gerund;
            }

            var refuelVehicleTank = DefDatabase<WorkGiverDef>.GetNamedSilentFail("VMF_RefuelVehicleTank");
            var refuelVehicle = DefDatabase<WorkGiverDef>.GetNamedSilentFail("RefuelVehicle");
            if (refuelVehicleTank != null && refuelVehicle != null)
            {
                refuelVehicleTank.label = refuelVehicle.label;
                refuelVehicleTank.verb = refuelVehicle.verb;
                refuelVehicleTank.gerund = refuelVehicle.gerund;
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

            var VMF_RefuelVehicleTank = VMF_DefOf.VMF_RefuelVehicleTank;
            var RefuelVehicle = JobDefOf_Vehicles.RefuelVehicle;
            if (VMF_RefuelVehicleTank != null && RefuelVehicle != null)
            {
                VMF_RefuelVehicleTank.label = RefuelVehicle.label;
                VMF_RefuelVehicleTank.reportString = RefuelVehicle.reportString;
            }

            //var VMF_RefuelVehicleTankAtomic = VMF_DefOf.VMF_RefuelVehicleTankAtomic;
            //var RefuelVehicleAtomic = JobDefOf_Vehicles.RefuelVehicleAtomic;
            //if (VMF_RefuelVehicleTankAtomic != null && RefuelVehicle != null)
            //{
            //    VMF_RefuelVehicleTankAtomic.label = RefuelVehicleAtomic.label;
            //    VMF_RefuelVehicleTankAtomic.reportString = RefuelVehicleAtomic.reportString;
            //}
        }

        public const string prefix = "VMF_";

        public const string suffix = "AcrossMaps";
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
