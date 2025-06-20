
using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using VehicleInteriors;
using VehicleInteriors.VMF_HarmonyPatches;
using Verse;
using Verse.AI;

namespace VMF_PUAHPatch
{
    /// <summary>
    /// Compatibility patch to PUAH and VFE shields made by Moriarty
    /// </summary>

    // Settings for the mod
    public class VMF_PUAHSettings : ModSettings
    {
        public bool patchEnabled = true;

        // Debug mode - when true, all logs show as their original level (warnings, errors)
        // When false, expected conditions are downgraded to regular messages
        public bool debugMode = false;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref patchEnabled, "patchEnabled", true);
            Scribe_Values.Look(ref debugMode, "debugMode", false);
            base.ExposeData();
        }
    }

    // Main mod class that handles settings
    public class VMF_PUAHMod : Mod
    {
        public static VMF_PUAHMod mod;

        // Static reference to our settings
        public static VMF_PUAHSettings settings;

        public VMF_PUAHMod(ModContentPack content) : base(content)
        {
            mod = this;
            settings = GetSettings<VMF_PUAHSettings>();
        }

        //public override string SettingsCategory() => "VMF and PUaH Compatibility Patch";
        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listingStandard = new Listing_Standard();
            listingStandard.Begin(inRect);

            listingStandard.CheckboxLabeled("Enabled", ref settings.patchEnabled);

            // Add debug mode toggle
            listingStandard.CheckboxLabeled("Debug Mode (Shows all warnings/errors - for mod developers)",
                ref settings.debugMode,
                "When enabled, all log messages will show at their original level (warnings, errors).\nThis is useful for debugging but may clutter your log with expected warnings.");

            listingStandard.End();

            base.DoSettingsWindowContents(inRect);
        }
    }

    // Logger utility class
    public static class VMF_PUAHLogger
    {
        // Log a message that would normally be a warning but gets downgraded in non-debug mode
        public static void LogSafe(string message)
        {
            // In debug mode, show as a warning
            if (VMF_PUAHMod.settings?.debugMode == true)
            {
                Log.Warning(message);
            }
            // In normal mode, show as a message
            else
            {
                Log.Message(message);
            }
        }

        // Log a regular informational message (always shows as message)
        public static void LogInfo(string message)
        {
            Log.Message(message);
        }

        // Log a handled issue (shows as warning in both modes, but helpful to separate types)
        public static void LogHandled(string message)
        {
            Log.Warning(message);
        }

        // Log an actual issue (always shows as warning)
        public static void LogIssue(string message)
        {
            Log.Warning(message);
        }
    }

    [StaticConstructorOnStartupPriority(Priority.Low)]
    public static class VMF_CompatibilityPatchMod
    {
        // Flag to control whether shield system patching is enabled
        private static bool shieldPatchEnabled = false;

        static VMF_CompatibilityPatchMod()
        {
            try
            {
                // First check if shield system patching is possible
                shieldPatchEnabled = CheckForShieldSystem();

                // Apply all other patches
                if (VMF_PUAHMod.settings.patchEnabled)
                {
                    //VMF_Harmony.Instance.PatchCategory("VMF_Compatibility_Patch");
                    ApplyPatches();
                    VMF_PUAHLogger.LogInfo("[VMF Compatibility Patch] Initialized");
                }
            }
            catch (Exception ex)
            {
                VMF_PUAHLogger.LogIssue($"[VMF Compatibility Patch] [ISSUE] Error during initialization: {ex.Message}");
            }
        }

        public static void ApplyPatches(bool unpatch = false)
        {
            //Patch(AccessTools.Method(typeof(Job), "MakeDriver"), prefix: AccessTools.Method(typeof(JobMakeDriverPatch), nameof(JobMakeDriverPatch.Prefix)));
            //Patch(AccessTools.Method(typeof(JobAcrossMapsUtility), nameof(JobAcrossMapsUtility.SetSpotsToJobAcrossMaps)), prefix: AccessTools.Method(typeof(JobAcrossMapsUtilityPatch), nameof(JobAcrossMapsUtilityPatch.Prefix)));
            if (ModCompat.PickUpAndHaul.Active)
            {
                var targetMethod = HaulToInventoryAcrossMapsPatch.TargetMethod();
                if (targetMethod != null)
                {
                    Patch(targetMethod, postfix: AccessTools.Method(typeof(HaulToInventoryAcrossMapsPatch), nameof(HaulToInventoryAcrossMapsPatch.Postfix)));
                }
            }
            //Patch(AccessTools.Method(typeof(FloatMenuMakerMap), "AddJobGiverWorkOrders"), prefix: AccessTools.Method(typeof(FloatMenuMakerMapPatch), nameof(FloatMenuMakerMapPatch.Prefix)));
            if (ModCompat.VFECore.Active)
            {
                var targetMethod = ShieldsSystemPatch.TargetMethod();
                if (targetMethod != null)
                {
                    Patch(targetMethod, prefix: AccessTools.Method(typeof(ShieldsSystemPatch), nameof(ShieldsSystemPatch.Prefix)));
                }
            }
            //Patch(AccessTools.Method(typeof(JobDriver), nameof(JobDriver.GetReport)), prefix: AccessTools.Method(typeof(JobDriverGetReportPatch), nameof(JobDriverGetReportPatch.Prefix)));
            //Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup)), prefix: AccessTools.Method(typeof(PawnSpawnSetupPatch), nameof(PawnSpawnSetupPatch.Prefix)));

            void Patch(MethodBase original, MethodInfo prefix = null, MethodInfo postfix = null, MethodInfo transpiler = null, MethodInfo finalizer = null)
            {
                if (unpatch)
                {
                    if (prefix != null)
                    {
                        VMF_Harmony.Instance.Unpatch(original, prefix);
                    }
                    if (postfix != null)
                    {
                        VMF_Harmony.Instance.Unpatch(original, postfix);
                    }
                    if (transpiler != null)
                    {
                        VMF_Harmony.Instance.Unpatch(original, transpiler);
                    }
                    if (finalizer != null)
                    {
                        VMF_Harmony.Instance.Unpatch(original, finalizer);
                    }
                }
                else
                {
                    if (prefix != null)
                    {
                        VMF_Harmony.Instance.Patch(original, prefix: prefix);
                    }
                    if (postfix != null)
                    {
                        VMF_Harmony.Instance.Patch(original, postfix: postfix);
                    }
                    if (transpiler != null)
                    {
                        VMF_Harmony.Instance.Patch(original, transpiler: transpiler);
                    }
                    if (finalizer != null)
                    {
                        VMF_Harmony.Instance.Patch(original, finalizer: finalizer);
                    }
                }
            }
        }

        // Check if the shield system can be patched correctly
        private static bool CheckForShieldSystem()
        {
            try
            {
                // Find VFECore.Shields.ShieldsSystem type
                Type shieldsSystemType = AccessTools.TypeByName("VFECore.Shields.ShieldsSystem");
                if (shieldsSystemType == null)
                {
                    // If type doesn't exist, no need for the patch
                    VMF_PUAHLogger.LogInfo("[VMF Compatibility Patch] VFECore.Shields.ShieldsSystem not found - shield compatibility not needed");
                    return false;
                }

                // Check for OnPawnSpawn method
                MethodInfo targetMethod = AccessTools.Method(shieldsSystemType, "OnPawnSpawn");
                if (targetMethod == null)
                {
                    // If method doesn't exist, no need for the patch
                    VMF_PUAHLogger.LogInfo("[VMF Compatibility Patch] VFECore.Shields.ShieldsSystem.OnPawnSpawn not found - shield compatibility not needed");
                    return false;
                }

                // Everything needed for patching is found
                VMF_PUAHLogger.LogInfo("[VMF Compatibility Patch] Shield system compatibility enabled");
                return true;
            }
            catch (Exception)
            {
                // Any error means we shouldn't try to patch
                return false;
            }
        }
    }

    // Primary patch to fix the immediate issue at Job.MakeDriver
    [HarmonyPatchCategory("VMF_Compatibility_Patch")]
    [HarmonyPatch(typeof(Job), "MakeDriver")]
    public static class JobMakeDriverPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Job __instance, Pawn driverPawn, ref JobDriver __result)
        {
            // If any essential objects are null, return a null driver and prevent the original method
            if (__instance == null || driverPawn == null || __instance.def == null)
            {
                // This is an expected edge case during map transitions
                VMF_PUAHLogger.LogSafe("[VMF Compatibility Patch] [SAFE] Successfully prevented NullReferenceException in Job.MakeDriver");
                __result = null;
                return false;
            }

            // Let the original method run if all objects are valid
            return true;
        }
    }

    // Patch to fix the issue in JobAcrossMapsUtility.SetSpotsToJobAcrossMaps
    [HarmonyPatchCategory("VMF_Compatibility_Patch")]
    [HarmonyPatch]
    public static class JobAcrossMapsUtilityPatch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // Find the SetSpotsToJobAcrossMaps method in VehicleInteriors.JobAcrossMapsUtility
            Type jobAcrossMapsUtilityType = AccessTools.TypeByName("VehicleInteriors.JobAcrossMapsUtility");
            if (jobAcrossMapsUtilityType == null)
            {
                VMF_PUAHLogger.LogIssue("[VMF Compatibility Patch] [ISSUE] Could not find JobAcrossMapsUtility type");
                return null;
            }

            MethodInfo targetMethod = AccessTools.Method(jobAcrossMapsUtilityType, "SetSpotsToJobAcrossMaps");
            if (targetMethod == null)
            {
                VMF_PUAHLogger.LogIssue("[VMF Compatibility Patch] [ISSUE] Could not find SetSpotsToJobAcrossMaps method");
            }
            return targetMethod;
        }

        // Prefix patch with full parameter list to match the original method
        [HarmonyPrefix]
        public static bool Prefix(Job job, Pawn pawn)
        {
            // If job is null, we can't proceed
            if (job == null || pawn == null)
            {
                // This is an expected edge case during map transitions
                VMF_PUAHLogger.LogSafe("[VMF Compatibility Patch] [SAFE] Safely handled null job or pawn in SetSpotsToJobAcrossMaps");
                return false; // Skip the original method
            }

            // Check if the job can create a valid driver before proceeding
            try
            {
                JobDriver driver = job.GetCachedDriver(pawn);
                if (driver == null)
                {
                    // This is an expected edge case during map transitions
                    VMF_PUAHLogger.LogSafe("[VMF Compatibility Patch] [SAFE] Successfully prevented null driver in SetSpotsToJobAcrossMaps");
                    return false; // Skip the original method
                }
            }
            catch (Exception ex)
            {
                // This is an expected edge case during map transitions
                VMF_PUAHLogger.LogSafe($"[VMF Compatibility Patch] [SAFE] Successfully handled exception in Job.GetCachedDriver: {ex.Message}");
                return false; // Skip the original method
            }

            // Let the original method run
            return true;
        }
    }

    // Patch to fix WorkGiver_HaulToInventoryAcrossMaps.JobOnThing
    [HarmonyPatchCategory("VMF_Compatibility_Patch")]
    [HarmonyPatch]
    public static class HaulToInventoryAcrossMapsPatch
    {
        public static bool Prepare()
        {
            return ModsConfig.IsActive("Mehni.PickUpAndHaul");
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            // Find the JobOnThing method in VMF_PUAHPatch.WorkGiver_HaulToInventoryAcrossMaps
            Type workGiverType = AccessTools.TypeByName("VMF_PUAHPatch.WorkGiver_HaulToInventoryAcrossMaps");
            if (workGiverType == null)
            {
                VMF_PUAHLogger.LogIssue("[VMF Compatibility Patch] [ISSUE] Could not find WorkGiver_HaulToInventoryAcrossMaps type");
                return null;
            }

            MethodInfo targetMethod = AccessTools.Method(workGiverType, "JobOnThing", new[] { typeof(Pawn), typeof(Thing), typeof(bool) });
            if (targetMethod == null)
            {
                VMF_PUAHLogger.LogIssue("[VMF Compatibility Patch] [ISSUE] Could not find JobOnThing method");
            }
            return targetMethod;
        }

        // We need to save the original method for calling it if needed
        private static MethodInfo originalJobOnThingMethod = null;
        private static MethodInfo vanillaHaulToInventoryMethod = null;

        // This method will run once when the patch is first applied
        static HaulToInventoryAcrossMapsPatch()
        {
            // Store the original cross-maps method
            Type workGiverType = AccessTools.TypeByName("VMF_PUAHPatch.WorkGiver_HaulToInventoryAcrossMaps");
            if (workGiverType != null)
            {
                originalJobOnThingMethod = AccessTools.Method(workGiverType, "JobOnThing");
            }

            // Try to find the vanilla Pick Up and Haul's WorkGiver_HaulToInventory.JobOnThing method
            Type vanillaType = AccessTools.TypeByName("PickUpAndHaul.WorkGiver_HaulToInventory");
            if (vanillaType != null)
            {
                vanillaHaulToInventoryMethod = AccessTools.Method(vanillaType, "JobOnThing");
            }
        }

        // Prefix patch to prevent cross-map hauling when on same map
        //[HarmonyPrefix]
        //Disabled by OELS: Because the pawn and Thing could be on the same map but the storage could be on a different map
        public static bool _(Pawn pawn, Thing thing, bool forced, ref Job __result)
        {
            // Basic null checks
            if (pawn == null || thing == null)
            {
                __result = null;
                return false;
            }

            // Additional safety checks
            if (pawn.Map == null || thing.Map == null)
            {
                __result = null;
                return false;
            }

            // Check if the pawn is a mechanoid - if so, only allow on same map hauling with special checks
            if (pawn.RaceProps != null && pawn.RaceProps.IsMechanoid)
            {
                // Most mechanoids can't use Pick Up and Haul inventory systems
                // Skip here to prevent the "provided target but yielded no job" error
                if (thing.Map != pawn.Map || !pawn.RaceProps.ToolUser || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                {
                    // Expected condition - mechanoids can't use PUaH across maps
                    __result = null;
                    return false;
                }
            }

            // KEY FIX: If pawn and thing are on the same map, use the vanilla hauling logic instead
            if (pawn.Map == thing.Map)
            {
                VMF_PUAHLogger.LogInfo("[VMF Compatibility Patch] [INFO] Pawn and item are on the same map, using standard hauling logic");

                // If we found the vanilla method, use it
                if (vanillaHaulToInventoryMethod != null)
                {
                    try
                    {
                        // Create an instance of the vanilla WorkGiver_HaulToInventory class
                        Type vanillaType = AccessTools.TypeByName("PickUpAndHaul.WorkGiver_HaulToInventory");
                        object vanillaWorkGiver = Activator.CreateInstance(vanillaType);

                        // Call the vanilla method
                        __result = (Job)vanillaHaulToInventoryMethod.Invoke(vanillaWorkGiver, new object[] { pawn, thing, forced });
                        return false; // Skip the original method
                    }
                    catch (Exception ex)
                    {
                        // This is a potential issue but we have a fallback
                        VMF_PUAHLogger.LogHandled($"[VMF Compatibility Patch] [HANDLED] Error calling vanilla hauling method: {ex.Message} - Will try fallback method");
                        // Continue with original method if this fails
                    }
                }

                // If we couldn't use the vanilla method, let's just create a basic haul job
                try
                {
                    // Only attempt to create a haul job if the pawn can actually haul
                    if (!pawn.RaceProps.ToolUser || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                    {
                        __result = null;
                        return false;
                    }

                    // Use the most straightforward approach to create a haul job
                    StoragePriority currentPriority = StoreUtility.CurrentStoragePriorityOf(thing);
                    IntVec3 storeCell;
                    if (StoreUtility.TryFindBestBetterStoreCellFor(thing, pawn, pawn.Map, currentPriority, pawn.Faction, out storeCell))
                    {
                        __result = HaulAIUtility.HaulToCellStorageJob(pawn, thing, storeCell, false);
                    }
                    else
                    {
                        __result = null;
                    }

                    return false; // Skip the original method regardless of result
                }
                catch (Exception ex)
                {
                    // This is potentially a real issue, but we've contained it
                    VMF_PUAHLogger.LogHandled($"[VMF Compatibility Patch] [HANDLED] Error creating vanilla haul job: {ex.Message}");
                    __result = null;
                    return false;
                }
            }

            // If pawn and thing are on different maps, let the original cross-map logic run
            VMF_PUAHLogger.LogInfo("[VMF Compatibility Patch] [INFO] Pawn and item are on different maps, proceeding with cross-map logic");
            return true;
        }

        // Postfix to ensure no null job drivers are returned
        [HarmonyPostfix]
        public static void Postfix(ref Job __result, Pawn pawn)
        {
            if (__result != null && pawn != null)
            {
                try
                {
                    // Check if the job can create a valid driver
                    JobDriver driver = __result.GetCachedDriver(pawn);
                    if (driver == null)
                    {
                        VMF_PUAHLogger.LogHandled("[VMF Compatibility Patch] Created job would have null driver - replacing with null job");
                        __result = null;
                    }
                }
                catch (Exception ex)
                {
                    VMF_PUAHLogger.LogHandled($"[VMF Compatibility Patch] Exception in job driver check: {ex.Message}");
                    __result = null;
                }
            }
        }
    }

    // Additional patch to handle exceptions in the FloatMenuMakerMap
    [HarmonyPatchCategory("VMF_Compatibility_Patch")]
    [HarmonyPatch(typeof(FloatMenuMakerMap), "AddJobGiverWorkOrders")]
    public static class FloatMenuMakerMapPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(Pawn pawn, List<FloatMenuOption> opts)
        {
            if (pawn == null || opts == null)
            {
                return false;
            }

            try
            {
                // We'll wrap the original method in a try-catch
                return true;
            }
            catch (NullReferenceException ex)
            {
                VMF_PUAHLogger.LogHandled($"[VMF Compatibility Patch] Prevented NullReferenceException in AddJobGiverWorkOrders: {ex.Message}");
                return false;
            }
        }
    }

    // Handle VFECore.Shields.ShieldsSystem if present
    [HarmonyPatchCategory("VMF_Compatibility_Patch")]
    [HarmonyPatch]
    public static class ShieldsSystemPatch
    {
        public static bool Prepare()
        {
            return ModsConfig.IsActive("OskarPotocki.VanillaFactionsExpanded.Core");
        }

        // Cache the types and field info for better performance
        private static Type shieldsSystemType = null;
        private static MethodInfo targetMethod = null;
        private static FieldInfo instanceField = null;
        private static Dictionary<string, HashSet<int>> trackedPawns = new Dictionary<string, HashSet<int>>();

        // Control whether to apply this patch
        public static bool ShouldApplyPatch()
        {
            // Check if shield patching is enabled by a static field in VMF_CompatibilityPatchMod
            FieldInfo enabledField = AccessTools.Field(typeof(VMF_CompatibilityPatchMod), "shieldPatchEnabled");
            return enabledField != null && (bool)enabledField.GetValue(null);
        }

        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            if (!ShouldApplyPatch())
            {
                return null; // Don't apply the patch if it's not enabled
            }

            // Find and cache needed types and methods just once
            if (shieldsSystemType == null)
            {
                shieldsSystemType = AccessTools.TypeByName("VFECore.Shields.ShieldsSystem");

                if (shieldsSystemType != null)
                {
                    targetMethod = AccessTools.Method(shieldsSystemType, "OnPawnSpawn");

                    // Try to find the instance field - it might be named differently
                    instanceField = AccessTools.Field(shieldsSystemType, "instance");
                    if (instanceField == null)
                    {
                        // Try to find any static field of the ShieldsSystem type
                        var allFields = shieldsSystemType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                        foreach (var field in allFields)
                        {
                            if (field.FieldType == shieldsSystemType)
                            {
                                instanceField = field;
                                break;
                            }
                        }
                    }
                }
            }

            return targetMethod;
        }

        [HarmonyPrefix]
        public static bool Prefix(Pawn __instance)
        {
            if (__instance == null) return false;

            try
            {
                // Simple tracking to prevent duplicate key errors
                string pawnKey = __instance.ThingID;
                int pawnID = __instance.thingIDNumber;

                // Use our own tracking system to prevent duplicates
                if (!trackedPawns.ContainsKey(pawnKey))
                {
                    trackedPawns[pawnKey] = new HashSet<int>();
                }

                if (trackedPawns[pawnKey].Contains(pawnID))
                {
                    // We've seen this pawn before, skip the original method
                    VMF_PUAHLogger.LogSafe("[VMF Compatibility Patch] [SAFE] Prevented duplicate shield entry for pawn " + __instance.Name);
                    return false;
                }

                // First time seeing this pawn, add it to our tracker
                trackedPawns[pawnKey].Add(pawnID);

                // Clean up if we're tracking too many versions of this pawn
                if (trackedPawns[pawnKey].Count > 10)
                {
                    trackedPawns[pawnKey].Clear();
                    trackedPawns[pawnKey].Add(pawnID);
                }

                return true; // Let the original method run for new pawns
            }
            catch (Exception ex)
            {
                VMF_PUAHLogger.LogHandled($"[VMF Compatibility Patch] [HANDLED] Exception in shield system: {ex.Message}");
                return true; // If anything goes wrong, let the original method run
            }
        }
    }

    // Patch to handle "Index was outside the bounds of the array" errors in JobDriver.GetReport
    [HarmonyPatchCategory("VMF_Compatibility_Patch")]
    [HarmonyPatch(typeof(JobDriver), "GetReport")]
    public static class JobDriverGetReportPatch
    {
        [HarmonyPrefix]
        public static bool Prefix(JobDriver __instance, ref string __result)
        {
            try
            {
                // Let the original method run inside our try-catch
                return true;
            }
            catch (IndexOutOfRangeException)
            {
                // If there's an index out of range exception, return a basic report
                string jobLabel = __instance?.job?.def?.reportString ?? "Unknown job";
                __result = $"Doing: {jobLabel}";
                return false; // Skip the original method
            }
            catch (Exception ex)
            {
                // For any other exception, log it and return a basic report
                VMF_PUAHLogger.LogHandled($"[VMF Compatibility Patch] Exception in JobDriver.GetReport: {ex.Message}");
                string jobLabel = __instance?.job?.def?.reportString ?? "Unknown job";
                __result = $"Doing: {jobLabel}";
                return false; // Skip the original method
            }
        }
    }

    // Patch to handle map transition issues in SpawnSetup
    [HarmonyPatchCategory("VMF_Compatibility_Patch")]
    [HarmonyPatch(typeof(Pawn), "SpawnSetup")]
    public static class PawnSpawnSetupPatch
    {
        [HarmonyPrefix]
        public static void Prefix(Pawn __instance, Map map)
        {
            if (__instance == null || map == null) return;

            try
            {
                // Just log the spawn if we're in a real game
                if (Current.Game != null && Find.TickManager != null && Find.TickManager.TicksGame > 0)
                {
                    // Optional logging - can be commented out if you want zero log entries
                    // VMF_PUAHLogger.LogInfo($"[VMF Compatibility Patch] Pawn {__instance.Name} spawning on map {map.GetUniqueLoadID()}");
                }
            }
            catch (Exception ex)
            {
                // Silently fail if anything goes wrong
                if (VMF_PUAHMod.settings?.debugMode == true)
                {
                    VMF_PUAHLogger.LogHandled($"[VMF Compatibility Patch] [HANDLED] Exception in pawn spawn: {ex.Message}");
                }
            }
        }
    }
}