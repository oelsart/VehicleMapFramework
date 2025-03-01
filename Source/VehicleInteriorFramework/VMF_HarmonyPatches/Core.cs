using HarmonyLib;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    public class VMF_Harmony
    {
        public static Harmony Instance = new Harmony("com.harmony.oels.vehicleinteriorframework");
    }

    [LoadedEarly]
    [StaticConstructorOnModInit]
    public static class EarlyPatchCore
    {
        static EarlyPatchCore()
        {
            VMF_Harmony.Instance.PatchCategory("VehicleInteriors.EarlyPatches");
        }
    }

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public class StaticConstructorOnStartupPriority : Attribute
    {
        public StaticConstructorOnStartupPriority(int priority)
        {
            this.priority = priority;
        }

        public int priority = -1;
    }

    [StaticConstructorOnStartup]
    public static class StaticConstructorOnStartupPriorityUtility
    {
        static StaticConstructorOnStartupPriorityUtility()
        {
            var types = GenTypes.AllTypesWithAttribute<StaticConstructorOnStartupPriority>();
            types.SortByDescending(t => t.GetCustomAttribute<StaticConstructorOnStartupPriority>().priority);
            foreach (Type type in types)
            {
                try
                {
                    RuntimeHelpers.RunClassConstructor(type.TypeHandle);
                }
                catch (Exception ex)
                {
                    Log.Error(string.Concat(new object[]
                    {
                        "Error in static constructor of ",
                        type,
                        ": ",
                        ex
                    }));
                }
            }
        }
    }

    [StaticConstructorOnStartupPriority(Priority.Normal)]
    public static class Core
    {
        static Core()
        {
            VMF_Harmony.Instance.PatchAllUncategorized(Assembly.GetExecutingAssembly());
        }
    }

    [StaticConstructorOnStartupPriority(Priority.Last)]
    public static class HarmonyPatchReport
    {
        static HarmonyPatchReport()
        {
            Log.Message($"[VehicleMapFramework] {VehicleInteriors.mod.Content.ModMetaData.ModVersion} rev{Assembly.GetExecutingAssembly().GetName().Version.Revision}");
            Log.Message($"[VehicleMapFramework] {VMF_Harmony.Instance.GetPatchedMethods().Count()} patches applied.");
        }

        public static IEnumerable<CodeInstruction> MethodReplacerLog(this IEnumerable<CodeInstruction> instructions, MethodBase from, MethodBase to)
        {
            if (instructions.Any(c => c.OperandIs(from))) Log.Error("Could not find the method to be replaced.");
            if (from == null)
            {
                throw new ArgumentException("Unexpected null argument", "from");
            }
            if (to == null)
            {
                throw new ArgumentException("Unexpected null argument", "to");
            }
            foreach (CodeInstruction codeInstruction in instructions)
            {
                MethodBase left = codeInstruction.operand as MethodBase;
                if (left == from)
                {
                    codeInstruction.opcode = (to.IsConstructor ? OpCodes.Newobj : OpCodes.Call);
                    codeInstruction.operand = to;
                }
                yield return codeInstruction;
            }
        }
    }
}