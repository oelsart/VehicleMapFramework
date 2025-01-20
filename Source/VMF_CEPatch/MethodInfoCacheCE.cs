using CombatExtended;
using HarmonyLib;
using System.Reflection;

namespace VMF_CEPatch
{
    public static class MethodInfoCacheCE
    {
        public static MethodInfo m_TryFindCEShootLineFromTo = AccessTools.Method(typeof(Verb_LaunchProjectileCE), nameof(Verb_LaunchProjectileCE.TryFindCEShootLineFromTo));

        public static MethodInfo m_TryFindCEShootLineFromToOnVehicle = AccessTools.Method(typeof(VerbOnVehicleCEUtility), nameof(VerbOnVehicleCEUtility.TryFindCEShootLineFromToOnVehicle));
    }
}
