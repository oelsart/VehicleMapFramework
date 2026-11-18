using System.Reflection.Emit;
using HarmonyLib;
using Verse;
using Verse.Sound;

namespace VehicleMapFramework.VMF_HarmonyPatches;

//CellがターゲットのMoteにオフセットをかける
[HarmonyPatch(typeof(MoteAttachLink), nameof(MoteAttachLink.UpdateDrawPos))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MoteAttachLink_UpdateDrawPos
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var codes = instructions.ToList();
    var pos =
      codes.FindIndex(c => c.opcode == OpCodes.Call && c.OperandIs(CachedMethodInfo.m_IntVec3_ToVector3Shifted)) + 1;
    var vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));
    var label = generator.DefineLabel();

    codes[pos].labels.Add(label);
    codes.InsertRange(pos,
    [
      CodeInstruction.LoadArgument(0),
      CodeInstruction.LoadField(typeof(MoteAttachLink), "targetInt", true),
      new CodeInstruction(OpCodes.Call, AccessTools.PropertyGetter(typeof(TargetInfo), nameof(TargetInfo.Map))),
      new CodeInstruction(OpCodes.Ldloca, vehicle),
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsNonFocusedVehicleMapOf),
      new CodeInstruction(OpCodes.Brfalse_S, label),
      new CodeInstruction(OpCodes.Ldloc_S, vehicle),
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2)
    ]);
    return codes;
  }
}

[HarmonyPatch(typeof(SubEffecter_Sprayer), "MakeMote")]
[PatchLevel(Level.Safe)]
public static class Patch_SubEffecter_Sprayer_MakeMote
{
  public static void Prefix(SubEffecter_Sprayer __instance, TargetInfo A, TargetInfo B)
  {
    var locType = __instance.EffectiveSpawnLocType;
    if (locType == MoteSpawnLocType.OnSource && A.HasThing || locType == MoteSpawnLocType.OnTarget && B.HasThing)
      return;
    VehiclePawnWithMapCache.CacheMode = true;
  }

  public static void Finalizer()
  {
    VehiclePawnWithMapCache.CacheMode = false;
  }
}

[HarmonyPatch(typeof(SoundDefHelper), nameof(SoundDefHelper.CorrectContextNow))]
[PatchLevel(Level.Sensitive)]
public static class Patch_SoundDefHelper_CorrectContextNow
{
  public static List<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Find_CurrentMap))
      .Advance()
      .InsertAndAdvance(CachedMethodInfo.m_BaseMap_Map.CallInstruction)
      .InsertAfter(CachedMethodInfo.m_BaseMap_Map.CallInstruction)
      .Instructions();
  }
}

[HarmonyPatch(typeof(SampleOneShotManager), "CameraDistanceSquaredOf")]
[PatchLevel(Level.Cautious)]
public static class Patch_SampleOneShotManager_CameraDistanceSquaredOf
{
  public static List<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_TargetInfo_Cell, (CachedMethodInfo.m_CellOnBaseMap_TargetInfo));
  }
}

[HarmonyPatch(typeof(Sustainer), "UpdateRootObjectPosition")]
[PatchLevel(Level.Cautious)]
public static class Patch_Sustainer_UpdateRootObjectPosition
{
  public static List<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_TargetInfo_Cell, (CachedMethodInfo.m_CellOnBaseMap_TargetInfo));
  }
}

[HarmonyPatch(typeof(Sustainer), "PlaySoundOnMapOrCamera")]
[PatchLevel(Level.Cautious)]
public static class Patch_Sustainer_PlaySoundOnMapOrCamera
{
  public static List<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_TargetInfo_Map, (CachedMethodInfo.m_BaseMap_TargetInfo));
  }
}