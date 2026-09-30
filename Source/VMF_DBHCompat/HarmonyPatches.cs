using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using DubsBadHygiene;
using HarmonyLib;
using Verse;
using static VehicleMapFramework.MethodInfoCache;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_DBH
{

  public static readonly CompProperties_Pipe dummy = new();

  static Patches_DBH()
  {
    VMF_Harmony.PatchCategory(PatchCategories.DubsBadHygiene);
    if (DubsBadHygiene.Settings.LiteMode)
    {
      DefDatabase<ThingDef>.GetNamed("VMF_PipeConnector"!).comps.RemoveAll(c => c is CompProperties_PipeConnectorDBH);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.DubsBadHygiene)]
[HarmonyPatch(typeof(CompPipe), nameof(CompPipe.Props), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_CompResource_Props
{
  public static void Postfix(CompPipe __instance, ref CompProperties_Pipe __result)
  {
    if (__instance is CompPipeConnectorDBH connector)
    {
      Patches_DBH.dummy.mode = connector.mode;
      Patches_DBH.dummy.stuffed = connector.Props.stuffed;
      Patches_DBH.dummy.vertPipe = connector.Props.vertPipe;
      __result = Patches_DBH.dummy;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.DubsBadHygiene)]
[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_PlaceWorker_SewageArea_DrawGhost_Predicate
{
  private static MethodBase TargetMethod()
  {
    return AccessTools.FindIncludingInnerTypes<MethodBase>(typeof(PlaceWorker_SewageArea),
      t => t.GetDeclaredMethods().FirstOrDefault(m => m.Name.Contains("<DrawGhost>")));
  }

  public static bool Prefix(IntVec3 x, Map ___visibleMap)
  {
    return x.InBounds(___visibleMap);
  }
}

[HarmonyPatchCategory(PatchCategories.DubsBadHygiene)]
[HarmonyPatch(typeof(MapComponent_Hygiene), nameof(MapComponent_Hygiene.CanHaveSewage))]
[PatchLevel(Level.Safe)]
public static class Patch_MapComponent_Hygiene_CanHaveSewage
{
  public static bool Prefix(IntVec3 c, Map ___map)
  {
    return c.InBounds(___map);
  }
}

[HarmonyPatchCategory(PatchCategories.DubsBadHygiene)]
[HarmonyPatch(typeof(MapComponent_Hygiene), nameof(MapComponent_Hygiene.MapComponentUpdate))]
[PatchLevel(Level.Cautious)]
public static class Patch_MapComponent_Hygiene_MapComponentUpdate
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_CellRect_ClipInsideMap, CachedMethodInfo.m_ClipInsideVehicleMap);
  }
}

[HarmonyPatchCategory(PatchCategories.DubsBadHygiene)]
[HarmonyPatch(typeof(Graphic_LinkedPipe), nameof(Graphic_LinkedPipe.ShouldLinkWith))]
[PatchLevel(Level.Safe)]
public static class Patch_Graphic_LinkedPipeDBH_ShouldLinkWith
{
  public static void Prefix(IntVec3 c, Thing parent)
  {
    Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
  }
}

[HarmonyPatchCategory(PatchCategories.DubsBadHygiene)]
[HarmonyPatch("DubsBadHygiene.Building_StallDoor", "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_StallDoor_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .NonFocusedMapVehicleForThing(out var vehicle)
      .AddAltitudeFor(vehicle)
      .InstructionEnumeration();
  }
}

[HarmonyPatchCategory(PatchCategories.DubsBadHygiene)]
[HarmonyPatch(typeof(Building_bath), nameof(Building_bath.DrawAt))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_bath_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Building_bath), nameof(Building_bath.WaterOffset))))
      .Advance()
      .NonFocusedMapVehicleForThing(out var vehicle)
      .CreateLabel(out var label)
      .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldc_R4, VehicleMapUtility.YCompress),
        new CodeInstruction(OpCodes.Div))
      .MatchStartForward(CodeMatch.Calls(AccessTools.Method(typeof(Building_bath), nameof(Building_bath.QuatFromRot))))
      .Advance()
      .MultiplyExtraAngleQuat(vehicle)
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}

[HarmonyPatchCategory(PatchCategories.DubsBadHygiene)]
[HarmonyPatch(typeof(Building_washbucket), nameof(Building_washbucket.DrawAt))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Building_washbucket_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.LoadsField(AccessTools.Field(typeof(Building_washbucket), "quat")))
      .Advance()
      .NonFocusedMapVehicleForThing(out var vehicle)
      .MultiplyExtraAngleQuat(vehicle)
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_BaseRotationVehicleDraw);
  }
}