using System.Linq;
using System.Reflection;
using HarmonyLib;
using SmashTools;
using Vehicles;
using Verse;
using Verse.AI;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class ConditionalPatches
{
    static ConditionalPatches()
    {
        // This class is just a placeholder for conditional patches.
    }

    internal static void DebugError(string methodName)
    {
        VMF_Log.DebugError($"The method {methodName} targeted for patching was not found. This should mean the removal of the stubs targeted for patching.");
    }
}