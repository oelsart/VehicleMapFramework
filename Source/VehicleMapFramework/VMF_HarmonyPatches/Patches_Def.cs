using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.VeryLow)]
public static class DefMessagesReplace
{
    static DefMessagesReplace()
    {
        var refuelVehicleTank = DefDatabase<WorkGiverDef>.GetNamedSilentFail("VMF_RefuelVehicleTank"!);
        var refuelVehicle = DefDatabase<WorkGiverDef>.GetNamedSilentFail("RefuelVehicle"!);
        if (refuelVehicleTank != null && refuelVehicle != null)
        {
            refuelVehicleTank.label = refuelVehicle.label;
            refuelVehicleTank.verb = refuelVehicle.verb;
            refuelVehicleTank.gerund = refuelVehicle.gerund;
        }

        var removeSegment = VMF_DefOf.VMF_RemoveVehicleSegment;
        var deconstruct = DefDatabase<WorkGiverDef>.GetNamedSilentFail("Deconstruct"!);
        if (removeSegment != null && deconstruct != null)
        {
            removeSegment.label = deconstruct.label;
            removeSegment.verb = deconstruct.verb;
            removeSegment.gerund = deconstruct.gerund;
        }

        foreach (var jobDef in DefDatabase<JobDef>.AllDefs
                     .Where(d => d.defName.StartsWith(prefix) && d.defName.EndsWith(suffix)))
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

        VMF_DefOf.VMF_DeconstructVehicleSegment?.reportString = JobDefOf.Deconstruct.reportString;
    }

    public const string prefix = "VMF_";

    public const string suffix = "AcrossMaps";
}

[StaticConstructorOnStartupPriority(Priority.VeryLow)]
public static class CheckEnablePipeConnector
{
    static CheckEnablePipeConnector()
    {
        if (!EnablePipeConnector())
        {
            DefDatabase<ThingDef>.GetNamed("VMF_PipeConnector"!).designationCategory = null;
            DefDatabase<DesignationCategoryDef>.GetNamed("VF_Vehicles"!).ResolveReferences();
        }
    }

    private static bool EnablePipeConnector()
    {
        if (DubsBadHygiene.Active && !DubsBadHygiene.LiteMode) return true;
        if (Rimefeller.Active) return true;

        if (VFECore.Active)
        {
            var allDefs = (IEnumerable<object>)AccessTools.PropertyGetter(typeof(DefDatabase<>)
                .MakeGenericType(VFECore.PipeNetDef), "AllDefs").Invoke(null, null);
            if (allDefs.Count() > 1)
            {
                return true;
            }
        }
        return false;
    }
}
