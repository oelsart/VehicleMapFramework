using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Vehicles;
using Verse;

namespace VehicleInteriors.VMF_HarmonyPatches
{
    public class VMF_Harmony
    {
        public static Harmony Instance = new Harmony("com.harmony.oels.vehiclemapframework");
    }

    [LoadedEarly]
    [StaticConstructorOnModInit]
    public static class EarlyPatchCore
    {
        static EarlyPatchCore()
        {
            //PatchCategoryを手動パッチに変更することでロード時間が短縮された。この件について追加調査を行う必要があるかもしれない。
            //VMF_Harmony.Instance.PatchCategory("VehicleInteriors.EarlyPatches");

            VMF_Harmony.Instance.Patch(AccessTools.PropertyGetter(typeof(ShaderTypeDef), nameof(ShaderTypeDef.Shader)), prefix: AccessTools.Method(typeof(Patch_ShaderTypeDef_Shader), nameof(Patch_ShaderTypeDef_Shader.Prefix)));
            VMF_Harmony.Instance.Patch(AccessTools.Method(typeof(VehicleHarmonyOnMod), nameof(VehicleHarmonyOnMod.ShaderFromAssetBundle)), prefix: AccessTools.Method(typeof(Patch_VehicleHarmonyOnMod_ShaderFromAssetBundle), nameof(Patch_VehicleHarmonyOnMod_ShaderFromAssetBundle.Prefix)));
            VMF_Harmony.Instance.Patch(AccessTools.Method(typeof(GraphicUtility), nameof(GraphicUtility.WrapLinked)), prefix: AccessTools.Method(typeof(Patch_GraphicUtility_WrapLinked), nameof(Patch_GraphicUtility_WrapLinked.Prefix)));
            VMF_Harmony.Instance.Patch(AccessTools.Method(typeof(GraphicData), nameof(GraphicData.CopyFrom)), postfix: AccessTools.Method(typeof(Patch_GraphicData_CopyFrom), nameof(Patch_GraphicData_CopyFrom.Postfix)));
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

    //[StaticConstructorOnStartup]
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
    }
}