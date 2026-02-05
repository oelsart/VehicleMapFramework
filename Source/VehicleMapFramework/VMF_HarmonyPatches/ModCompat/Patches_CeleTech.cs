using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
internal class Patches_CeleTech
{
    static Patches_CeleTech()
    {
        if (CeleTech)
        {
            VMF_Harmony.PatchCategory(PatchCategories.CeleTechArsenal);
        }
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun", "OrderAttack")]
[PatchLevel(Level.Cautious)]
public static class Patch_Building_CMCTurretGun_OrderAttack
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Building_CMCTurretGun", "TryFindNewTarget")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_CMCTurretGun_TryFindNewTarget
{
    private static readonly List<Building> tmpList = [];

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = instructions.ToList();
        var f_allBuildingsColonist = AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist));
        var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldfld && c.OperandIs(f_allBuildingsColonist)) + 1;
        codes.InsertRange(pos,
        [
            CodeInstruction.LoadArgument(0),
            CodeInstruction.Call(typeof(Patch_ArcanePlant_Turret_TryFindNewTarget), nameof(AddBuildingList))
        ]);
        return codes;
    }

    private static List<Building> AddBuildingList(List<Building> list, Building instance)
    {
        tmpList.Clear();
        tmpList.AddRange(list);
        var maps = instance.Map.BaseMapAndVehicleMaps(false);
        tmpList.AddRange(maps.SelectMany(m => m.listerBuildings.allBuildingsColonist));
        return tmpList;
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_CMCTurretGun_TryFindNewTarget_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("TOT_DLL_test.Building_CMCTurretGun", "TOT_DLL_test"), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<TryFindNewTarget>"));
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CMCTurretTop", "DrawTurret")]
[PatchLevel(Level.Sensitive)]
public static class Patch_CMCTurretTop_DrawTurret
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);
        codes.MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(Altitudes), nameof(Altitudes.AltitudeFor), [typeof(AltitudeLayer)])));
        codes.CreateLabelWithOffsets(1, out var label);
        codes.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
        codes.InsertAfter(
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(GenTypes.GetTypeInAnyAssembly("TOT_DLL_test.CMCTurretTop", "TOT_DLL_test"), "parentTurret"),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull));
        return codes.Instructions();
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CMCTurretTop", "ForceFaceTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_CMCTurretTop_ForceFaceTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.CMCTurretTop", "TurretTopTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_CMCTurretTop_TurretTopTick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_FCradar", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_FCradar_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var codes = new CodeMatcher(instructions, generator);
        codes.MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(Altitudes), nameof(Altitudes.AltitudeFor), [typeof(AltitudeLayer)])));
        codes.CreateLabelWithOffsets(1, out var label);
        codes.DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle);
        codes.InsertAfter(
            CodeInstruction.LoadArgument(0),
            CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)),
            new CodeInstruction(OpCodes.Ldloca_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf),
            new CodeInstruction(OpCodes.Brfalse_S, label),
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_YOffsetFull));
        return codes.Instructions();
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_CMCShield", "Draw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_CMCShield_Draw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_PrismTowerTop", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_PrismTowerTop_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_TraderShuttle", "PostDraw")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Comp_TraderShuttle_PostDraw
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        return Patch_Comp_FCradar_PostDraw.Transpiler(instructions, generator);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Comp_UAV", "CompTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_Comp_UAV_CompTick
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "GetForcedMissTarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_GetForcedMissTarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_LauncherProjectileSwitchFire_GetForcedMissTarget_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "TOT_DLL_test"), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<GetForcedMissTarget>"));
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "TryCastShot")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_TryCastShot
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_Verb_LaunchProjectile_TryCastShot.Transpiler(instructions);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "Retarget")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_Retarget
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Patch_Verb_LaunchProjectile_TryCastShot.Transpiler(instructions);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Patch_Verb_LauncherProjectileSwitchFire_Retarget_Delegate
{
    private static MethodBase TargetMethod()
    {
        return AccessTools.FindIncludingInnerTypes(GenTypes.GetTypeInAnyAssembly("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "TOT_DLL_test"), t =>
        {
            return t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<Retarget>"));
        });
    }

    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
    }
}

[HarmonyPatchCategory(PatchCategories.CeleTechArsenal)]
[HarmonyPatch("TOT_DLL_test.Verb_LauncherProjectileSwitchFire", "CanHitFromCellIgnoringRange")]
[PatchLevel(Level.Cautious)]
public static class Patch_Verb_LauncherProjectileSwitchFire_CanHitFromCellIgnoringRange
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return instructions.MethodReplacer(CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap)
            .MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
    }
}