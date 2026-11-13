global using static VehicleMapFramework.MethodInfoCache;
using System.Reflection.Emit;
using HarmonyLib;
using PipeSystem;
using SmashTools;
using UnityEngine;
using VEF.Apparels;
using VEF.Weapons;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

[StaticConstructorOnStartupPriority(Priority.Low)]
public static class Patches_VEF
{
  public static readonly AccessTools.FieldRef<PipeNetManager, int> pipeNetsCount = AccessTools.FieldRefAccess<PipeNetManager, int>("pipeNetsCount");

  static Patches_VEF()
  {
    if (VMF_Harmony.ManualPatchEnabled)
      VMF_Harmony.ManualPatchApis.Add(new ManualPatches_VEF());
    if (pipeNetsCount != null)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VEFCore);
    }
    else
    {
      ModCompat.LogIncompat("VEF Pipe System");
    }
    if (ModCompat.VFEArchitect)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VFEArchitect);
    }
    if (ModCompat.VFESecurity.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VFESecurity);
    }
    if (ModCompat.VFEFactory.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VFEFactory);
    }
    if (ModCompat.VanillaVehiclesExpanded.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VVE);
    }
    if (ModCompat.VGE)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VGE);
    }
    if (ModCompat.VQEGenerator)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VQEGenerator);
    }
    if (ModCompat.VanillaTemperatureExpanded.Active)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VTE);
    }
    if (ModCompat.VPsyE)
    {
      VMF_Harmony.PatchCategory(PatchCategories.VPsyE);
    }
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompResource), nameof(CompResource.Props), MethodType.Getter)]
[PatchLevel(Level.Safe)]
public static class Patch_CompResource_Props
{
  private static readonly CompProperties_Resource dummy = new();

  public static void Postfix(CompResource __instance, ref CompProperties_Resource __result)
  {
    if (__instance is not CompPipeConnectorVEF connector) return;

    dummy.pipeNet = connector.pipeNet;
    dummy.soundAmbient = __result.soundAmbient;
    __result = dummy;
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(PipeNetManager), nameof(PipeNetManager.UnregisterConnector))]
[PatchLevel(Level.Safe)]
public static class Patch_PipeNetManager_UnregisterConnector
{
  public static void Prefix(PipeNetManager __instance, CompResource comp)
  {
    var pipeNetMap = comp?.PipeNet?.map;
    if (pipeNetMap == null || __instance.map == null || __instance.map == pipeNetMap) return;

    var component = MapComponentCache<PipeNetManager>.GetComponent(pipeNetMap);
    var connectors = comp.PipeNet.connectors.Where(c => c.parent.Map == pipeNetMap);
    var newNet = PipeNetMaker.MakePipeNet(connectors, pipeNetMap, comp.PipeNet.def);
    component.pipeNets.Add(newNet);
    Patches_VEF.pipeNetsCount(MapComponentCache<PipeNetManager>.GetComponent(__instance.map))++;
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(PipeNet), nameof(PipeNet.Merge))]
[PatchLevel(Level.Safe)]
public static class Patch_PipeNet_Merge
{
  public static bool Prefix(ref PipeNet __instance, ref PipeNet otherNet)
  {
    if (__instance.map.IsVehicleMapOf(out _) && otherNet.map != __instance.map)
    {
      otherNet.Merge(__instance);
      return false;
    }
    return true;
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(Graphic_LinkedPipe), nameof(Graphic_LinkedPipe.ShouldLinkWith))]
public static class Patch_Graphic_LinkedPipeVEF_ShouldLinkWith
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(ref IntVec3 c, Thing parent)
  {
    Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, ((Delegate)MapModified).Method);
  }

  private static Map MapModified(Thing thing)
  {
    if (thing.TryGetComp<CompResource>(out var comp) && thing.Map != comp.PipeNet.map)
    {
      return comp.PipeNet.map;
    }
    return thing.Map;
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompResourceStorage), nameof(CompResourceStorage.PostDraw))]
[PatchLevel(Level.Safe)]
public static class Patch_CompResourceStorage_PostDraw
{
  public static void Prefix(CompResourceStorage __instance, ref GenDraw.FillableBarRequest ___request,
    ref FocusVehicleScope? __state)
  {
    if (__instance.Props.drawStorageBar && __instance.parent.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      var barData = __instance.Props.barData;
      ___request.rotation = __instance.parent.BaseRotationVehicleDraw().Rotated(RotationDirection.Clockwise);
      var offset = ___request.rotation.AsInt switch
      {
        Rot4.EastInt => barData.centerPositionOffsetEast,
        Rot4.SouthInt => barData.centerPositionOffsetSouth,
        Rot4.WestInt => barData.centerPositionOffsetWest,
        _ => barData.centerPositionOffsetNorth,
      };
      offset = offset.RotatedBy(vehicle.ExtraAngle);
      ___request.center = __instance.parent.DrawPos + Vector3.up * 0.1f + offset;
      __state = FocusVehicleScope.FocusVehicle(vehicle);
    }
  }
  
  public static void Finalizer(FocusVehicleScope? __state) => __state?.Dispose();
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(ExpandableProjectile), nameof(ExpandableProjectile.StartingPosition), MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_ExpandableProjectile_StartingPosition
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect);
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(Verb_ShootCone), "DrawLines")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_ShootCone_DrawLines
{
  public static List<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions, generator)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.m_IntVec3_ToVector3Shifted))
      .Advance()
      .NonFocusedMapVehicle(out var vehicle,
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Verb), nameof(Verb.caster)))
      .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_ToBaseMapCoord2))
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Rot4_AsQuat))
      .Advance()
      .MultiplyFullAngleQuat(vehicle)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Rot4_AsQuat))
      .Advance()
      .MultiplyFullAngleQuat(vehicle)
      .Instructions();
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(Verb_ShootCone), nameof(Verb_ShootCone.CanHitTarget))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Verb_ShootCone_CanHitTarget
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_LocalTargetInfo_Cell))
      .InsertAndAdvance(
        CodeInstruction.LoadArgument(0),
        CodeInstruction.LoadField(typeof(Verb), nameof(Verb.caster)))
      .Set(OpCodes.Call, ((Delegate)VehicleMapUtility.CellOnAnotherThingMap).Method)
      .Instructions();
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch("VEF.Weapons.VanillaExpandedFramework_Building_TurretGun_IsValidTarget_Patch", "Postfix")]
[PatchLevel(Level.Cautious)]
public static class Patch_VanillaExpandedFramework_Building_TurretGun_IsValidTarget_Patch_Postfix
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return PatchHelper.CreateCodeMatcherFast(instructions)
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Thing_Position))
      .InsertAndAdvance(CodeInstruction.LoadArgument(1))
      .Set(OpCodes.Call, CachedMethodInfo.m_PositionOnAnotherThingMap)
      .Instructions();
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompShieldField), "UpdateShieldCoverage")]
[PatchLevel(Level.Safe)]
public static class Patch_CompShieldField_UpdateShieldCoverage
{
  public static bool Prefix(CompShieldField __instance)
  {
    if (!__instance.HostThing.IsOnVehicleMapOf(out var vehicle) || !vehicle.Spawned)
      return true;

    var positionOnBaseMap = __instance.HostThing.PositionOnBaseMap;
    __instance.coveredCells =
    [
      .. GenRadial
        .RadialCellsAround(positionOnBaseMap, __instance.ShieldRadius, true)
        .Where(x => x.InBounds(vehicle.Map))
    ];
    if (__instance.ShieldRadius < 6f)
    {
      __instance.scanCells = __instance.coveredCells;
    }
    else
    {
      var interiorCells = GenRadial.RadialCellsAround(positionOnBaseMap,
        __instance.ShieldRadius - 5f,
        true);
      __instance.scanCells = new HashSet<IntVec3>(__instance.coveredCells.Where(c => !interiorCells.Contains(c)));
    }
    return false;
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompShieldField), nameof(CompShieldField.GetThingsInAreas))]
[PatchLevel(Level.Cautious)]
public static class Patch_CompShieldField_GetThingsInAreas
{
  private static readonly List<Thing> thingList = [];

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return new CodeMatcher(instructions)
      .MatchStartForward(CodeMatch.Calls(
        AccessTools.Method(typeof(ThingGrid), nameof(ThingGrid.ThingsListAtFast), [typeof(IntVec3)])))
      .Set(OpCodes.Call, ((Delegate)ThingsListAtFastAcrossMaps).Method)
      .MatchStartBackwards(CodeMatch.LoadsField(AccessTools.Field(typeof(Map), nameof(Map.thingGrid))))
      .RemoveInstruction()
      .InstructionEnumeration()
      .MethodReplacer(CachedMethodInfo.g_Thing_MapHeld, CachedMethodInfo.m_MapHeldBaseMap);
  }

  private static List<Thing> ThingsListAtFastAcrossMaps(Map map, IntVec3 c)
  {
    if (map.GetCachedMapComponent<VehicleMapGrid>().VehicleAt(c) is not { } vehicle)
      return map.thingGrid.ThingsListAtFast(c);

    thingList.Clear();
    thingList.AddRange(map.thingGrid.ThingsListAtFast(c));
    thingList.AddRange(vehicle.VehicleMap.thingGrid.ThingsAt(c.ToVehicleMapCoord(vehicle)));
    return thingList;
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompShieldField), nameof(CompShieldField.ListerShieldGensActiveIn))]
[PatchLevel(Level.Safe)]
public static class Patch_CompShieldField_ListerShieldGensActiveIn
{
  public static IEnumerable<CompShieldField> Postfix(IEnumerable<CompShieldField> values, Map map)
  {
    foreach (var comp in values)
    {
      yield return comp;
    }
    foreach (var comp in VehiclePawnWithMapCache.AllVehiclesOn(map)
               .SelectMany(vehicle => CompShieldField.ListerShieldGensActiveIn(vehicle.VehicleMap)))
    {
      yield return comp;
    }
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompShieldField),
  nameof(CompShieldField.AbsorbDamage),
  typeof(float),
  typeof(DamageDef),
  typeof(float))]
[PatchLevel(Level.Cautious)]
public static class Patch_CompShieldField_AbsorbDamage
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing),
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned),
      (CachedMethodInfo.m_OccupiedRect, CachedMethodInfo.m_MovedOccupiedRect));
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompShieldField), "EnergyShieldTick")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompShieldField_EnergyShieldTick
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMapSpawned);
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompShieldField), "Notify_EnergyDepleted")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompShieldField_Notify_EnergyDepleted
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}

[HarmonyPatchCategory(PatchCategories.VEFCore)]
[HarmonyPatch(typeof(CompShieldField), "UpdateCache")]
[PatchLevel(Level.Cautious)]
public static class Patch_CompShieldField_UpdateCache
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Map, CachedMethodInfo.m_BaseMap_Thing);
  }
}
