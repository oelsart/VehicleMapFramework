using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public class Patches_AutoApparelPickup
{
  static Patches_AutoApparelPickup()
  {
    if (AutoApparelPickup)
    {
      var f_ignoredJobs = AccessTools.Field("AutoApparelPickup.HarmonyPatches:ignoredJobs");
      if (f_ignoredJobs?.GetValue(null) is HashSet<JobDef> hashSet)
      {
        hashSet.Add(VMF_DefOf.VMF_GotoDestMap);
        hashSet.Add(VMF_DefOf.VMF_GotoAcrossMaps);
        hashSet.Add(VMF_DefOf.VMF_BoardAcrossMaps);
      }
    }
  }
}
