using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using SmashTools;
using UnityEngine;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

//Graphic_Linked系統のリンクは、先にcを回転させておく。base.ShouldLinkWithを使っているところはスタブしておいたオリジナルのメソッドを使用
[HarmonyPatch(typeof(Graphic_Linked), nameof(Graphic_Linked.ShouldLinkWith))]
public static class Patch_Graphic_Linked_ShouldLinkWith
{
  [PatchLevel(Level.Mandatory)]
  [HarmonyReversePatch]
  [HarmonyPriority(Priority.Normal)]
  //なんでReversePatchしてるのにオリジナルのメソッドをコピーしてるのかって？Performance AnalyzerがReversePatchに対応してないからだよ！
  public static bool ShouldLinkWith(Graphic_Linked instance, IntVec3 c, Thing parent)
  {
    if (!parent.Spawned)
    {
      return false;
    }

    if (!c.InBounds(parent.Map))
    {
      return (parent.def.graphicData.linkFlags & LinkFlags.MapEdge) > LinkFlags.None;
    }

    return (parent.Map.linkGrid.LinkFlagsAt(c) & parent.def.graphicData.linkFlags) > LinkFlags.None;
  }

  [PatchLevel(Level.Safe)]
  [HarmonyPriority(Priority.Low)]
  public static void Prefix(ref IntVec3 c, Thing parent)
  {
    if (VehicleSectionLayerManager.RotForPrint != Rot4.North)
    {
      var offset = c - parent.Position;
      var rotated = offset.RotatedBy(VehicleSectionLayerManager.RotForPrintCounter);
      c = rotated + parent.Position;
    }
  }
}

[HarmonyPatch(typeof(Graphic_LinkedAsymmetric), nameof(Graphic_LinkedAsymmetric.ShouldLinkWith))]
public static class Patch_Graphic_LinkedAsymmetric_ShouldLinkWith
{
  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
    instructions.MethodReplacer(CachedMethodInfo.m_ShouldLinkWith, CachedMethodInfo.m_ShouldLinkWithOrig);

  [PatchLevel(Level.Safe)]
  public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
}

[HarmonyPatch(typeof(Graphic_LinkedTransmitter), nameof(Graphic_LinkedTransmitter.ShouldLinkWith))]
public static class Patch_Graphic_LinkedTransmitter_ShouldLinkWith
{
  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) =>
    instructions.MethodReplacer(CachedMethodInfo.m_ShouldLinkWith, CachedMethodInfo.m_ShouldLinkWithOrig);

  [PatchLevel(Level.Safe)]
  public static void Prefix(ref IntVec3 c, Thing parent) => Patch_Graphic_Linked_ShouldLinkWith.Prefix(ref c, parent);
}

[HarmonyPatch]
[PatchLevel(Level.Sensitive)]
public static class Patch_Thing_Print
{
  private static IEnumerable<MethodBase> TargetMethods()
  {
    return typeof(Thing).AllSubclasses().Append(typeof(Thing)).Where(t => t != typeof(MinifiedThing))
      .Select(t => AccessTools.DeclaredMethod(t, nameof(Thing.Print)))
      .Where(m =>
      {
        if (m is null) return false;
        return PatchHelper.ReadMethodBodyWrapper(m).Any(i =>
          OpCodes.Ldc_R4.Equals(i.Key) && 0f.Equals(i.Value));
      });
  }

  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    codes.MatchStartForward(CodeMatch.LoadsConstant(0f))
      .Repeat(matcher =>
        matcher.InsertAndAdvance(CodeInstruction.LoadArgument(0))
          .SetInstruction(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation)));
    return codes.Instructions();
  }
}

[HarmonyPatch(typeof(MinifiedThing), nameof(MinifiedThing.Print))]
[PatchLevel(Level.Sensitive)]
public static class Patch_MinifiedThing_Print
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = new CodeMatcher(instructions);
    var m_PrintPlane = ((Delegate)Printer_Plane.PrintPlane).Method;
    codes.MatchStartForward(CodeMatch.Calls(m_PrintPlane));
    codes.MatchStartBackwards(new CodeMatch(c => c.opcode == OpCodes.Ldloc_1));
    codes.InsertAfterAndAdvance(CodeInstruction.LoadArgument(0));
    codes.Advance();
    codes.SetInstruction(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));

    codes.End();
    codes.MatchStartBackwards(CodeMatch.Calls(m_PrintPlane));
    codes.MatchStartBackwards(new CodeMatch(c =>
      c.opcode == OpCodes.Ldloc_S && ((LocalBuilder)c.operand).LocalType == typeof(Material)));
    codes.InsertAfterAndAdvance(CodeInstruction.LoadArgument(0));
    codes.Advance();
    codes.SetInstruction(new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_PrintExtraRotation));
    return codes.Instructions();
  }
}

[HarmonyPatch(typeof(Graphic), nameof(Graphic.Print))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Graphic_Print
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    var codes = instructions.ToList();
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Stloc_3) - 1;

    codes.InsertRange(pos,
    [
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotateForPrintNegate),
      CodeInstruction.LoadArgument(2),
      ((Delegate)EdgeSpacerOffset).Method.CallInstruction
    ]);

    return codes.MethodReplacer(CachedMethodInfo.g_Thing_Rotation, CachedMethodInfo.m_RotationForPrint);
  }

  //はしごとかのマップ端オフセットを足す
  private static Vector3 EdgeSpacerOffset(Vector3 vector, Thing thing)
  {
    VehicleMapProps mapProps;
    if (thing.def.HasComp(typeof(CompVehicleEnterSpot)) && thing.IsOnVehicleMapOf(out var vehicle) &&
        (mapProps = vehicle.VehicleDef.GetModExtension<VehicleMapProps>()) != null)
    {
      var opposite = thing.Rotation.Opposite;
      return vector + (opposite.AsVector2.ToVector3() *
                       mapProps.EdgeSpaceValue(VehicleSectionLayerManager.RotForPrint, opposite));
    }

    return vector;
  }
}

//コーナーフィラーの位置の回転を打ち消す
//マップ端のフィラー位置調整機能も切る　この機能何？
[HarmonyPatch(typeof(Graphic_LinkedCornerFiller), nameof(Graphic_LinkedCornerFiller.Print))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Graphic_LinkedCornerFiller_Print
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    var codes = instructions.ToList();
    var f_Altitudes_AltIncVect = AccessTools.Field(typeof(Altitudes), nameof(Altitudes.AltIncVect));
    var pos = codes.FindIndex(c => c.opcode == OpCodes.Ldsfld && c.OperandIs(f_Altitudes_AltIncVect)) - 1;

    codes.Insert(pos, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotateForPrintNegate));

    var c_Vector3 = AccessTools.Constructor(typeof(Vector3), [typeof(float), typeof(float), typeof(float)]);
    var pos2 = codes.FindIndex(pos, c => c.opcode == OpCodes.Newobj && c.OperandIs(c_Vector3)) + 1;
    codes.Insert(pos2, new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_RotateForPrintNegate));

    var pos3 = codes.FindIndex(pos2, c => c.opcode == OpCodes.Brtrue);
    var label = codes[pos3].operand;
    var l_vehicle = generator.DeclareLocal(typeof(VehiclePawnWithMap));

    codes.InsertRange(pos3 + 1,
    [
      CodeInstruction.LoadArgument(2),
      new CodeInstruction(OpCodes.Ldloca, l_vehicle),
      new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsOnVehicleMapOf),
      new CodeInstruction(OpCodes.Brtrue, label)
    ]);

    return codes;
  }
}

//Graphic_LinkedCornerOverlaySingleを使うためのWrap。linkDrawerTypeは適当に被らなそうな数字にしました。
[HarmonyPatchCategory(EarlyPatchCore.Category)]
[HarmonyPatch(typeof(GraphicUtility), nameof(GraphicUtility.WrapLinked))]
[PatchLevel(Level.Mandatory)]
public static class Patch_GraphicUtility_WrapLinked
{
  public static bool Prefix(Graphic subGraphic, LinkDrawerType linkDrawerType, ref Graphic_Linked __result)
  {
    if ((byte)linkDrawerType == 56)
    {
      __result = new Graphic_LinkedCornerOverlaySingle(subGraphic);
      return false;
    }

    return true;
  }
}

//カメラの制限範囲を書き換える
[HarmonyPatch(typeof(CameraDriver), nameof(CameraDriver.Update))]
[PatchLevel(Level.Sensitive)]
public static class Patch_CameraDriver_Update
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    const float limit = 200f;
    return new CodeMatcher(instructions, generator)
      .MatchStartForward(CodeMatch.LoadsConstant(-2f))
      .DeclareLocal(typeof(VehiclePawnWithMap), out var vehicle)
      .DeclareLocal(typeof(bool), out var isVehicleMap)
      .CreateLabel(out var label)
      .Insert(
        CodeInstruction.LoadField(typeof(VehicleMapFramework), nameof(VehicleMapFramework.settings)),
        CodeInstruction.LoadField(typeof(VehicleMapSettings), nameof(VehicleMapSettings.drawPlanet)),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.g_Find_CurrentMap),
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_IsVehicleMapOf),
        new CodeInstruction(OpCodes.Stloc_S, isVehicleMap))
      .Repeat(c =>
      {
        c.CreateLabel(out var label2)
          .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldloc_S, vehicle),
            new CodeInstruction(OpCodes.Brfalse_S, label2),
            new CodeInstruction(OpCodes.Pop),
            new CodeInstruction(OpCodes.Ldc_R4, limit))
          .Advance();
      }).InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(PawnRenderer), "GetBodyPos")]
[PatchLevel(Level.Safe)]
public static class Patch_PawnRenderer_GetBodyPos
{
  public static void Postfix(PawnPosture posture, Pawn ___pawn, ref Vector3 __result)
  {
    var corpse = ___pawn.Corpse;
    if (corpse != null && corpse.IsOnNonFocusedVehicleMapOf(out _))
    {
      corpse.TryGetDrawPos(ref __result);
    }
    else if (___pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      if (___pawn.CurrentBed() != null)
      {
        __result = __result.ToBaseMapCoord(vehicle).WithYOffset(-0.9615385f / VehicleMapUtility.YCompress);
      }
      else if (posture != PawnPosture.Standing)
      {
        __result = __result.YOffsetFull(vehicle);
      }
    }
  }
}

[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.BodyAngle))]
[PatchLevel(Level.Safe)]
public static class Patch_PawnRenderer_BodyAngle
{
  public static void Postfix(Pawn ___pawn, ref float __result)
  {
    if (___pawn.IsOnNonFocusedVehicleMapOf(out var vehicle))
    {
      __result = Ext_Math.RotateAngle(__result, vehicle.FullAngle);
    }
  }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawAimPie))]
public static class Patch_GenDraw_DrawAimPie
{
  [PatchLevel(Level.Safe)]
  public static void Prefix(Thing shooter, ref LocalTargetInfo target)
  {
    if (!target.HasThing && shooter.TryGetTargetMap(out var map))
    {
      target = target.Cell.ToBaseMapCoord(map);
    }
  }

  [PatchLevel(Level.Cautious)]
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(
      (CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap),
      (CachedMethodInfo.g_LocalTargetInfo_Cell, CachedMethodInfo.m_CellOnBaseMap));
  }
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.ProcessPostTickVisuals))]
[PatchLevel(Level.Cautious)]
public static class Patch_Pawn_ProcessPostTickVisuals
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.g_Thing_Position, CachedMethodInfo.m_PositionOnBaseMap);
  }
}

[HarmonyPatch(typeof(Graphic), nameof(Graphic.Draw))]
[PatchLevel(Level.Safe)]
public static class Patch_Graphic_Draw
{
  public static void Prefix(ref Vector3 loc, ref Rot4 rot, Thing thing, ref float extraRotation, Graphic __instance)
  {
    if (thing.IsOnNonFocusedVehicleMapOf(out var vehicle) && thing.def.drawerType == DrawerType.RealtimeOnly &&
        thing.def.category != ThingCategory.Item)
    {
      var def = thing.def.IsBlueprint ? thing.def.entityDefToBuild as ThingDef ?? thing.def : thing.def;

      var rot2 = rot;
      var baseRotInt = vehicle.FullRotation.RotForVehicleDraw().AsInt;

      bool SameMaterialByRot()
      {
        var graphic = def.graphic;
        if (graphic is Graphic_Collection) return true;
        var rotation = new Rot4(rot2.AsInt + baseRotInt);
        return graphic != null && graphic.MatAt(rot2, thing) == graphic.MatAt(rotation, thing) &&
               graphic.DrawOffset(rot2) == graphic.DrawOffset(rotation);
      }

      if (thing is not Building_Bookcase || thing.Graphic == __instance)
      {
        if (def.size.x != def.size.z || thing is Building_SupportedDoor ||
            ((((def.graphicData?.drawRotated ?? false) && (!def.graphicData?.Linked ?? true)) || def.rotatable) &&
             !SameMaterialByRot()))
        {
          rot.AsInt += baseRotInt;
        }
      }

      if (def.ShouldRotatedOnVehicle())
      {
        var angle = vehicle.Angle - vehicle.Transform.rotation;
        extraRotation -= angle;
        var offset = thing.Graphic.DrawOffset(rot);
        if (__instance is Graphic_Flicker && thing.Graphic is not Graphic_Single &&
            thing.TryGetComp<CompFireOverlay>(out var comp))
        {
          offset += comp.Props.DrawOffsetForRot(rot);
        }

        var offset2 = offset.RotatedBy(-angle);
        loc += new Vector3(offset2.x - offset.x, 0f, offset2.z - offset.z);
      }
    }
    else if (thing is { Spawned: false } && thing.SpawnedParentOrMe.IsOnNonFocusedVehicleMapOf(out vehicle))
    {
      extraRotation += vehicle.FullAngle;
    }
  }
}

[HarmonyPatch(typeof(Graphic), nameof(Graphic.DrawFromDef))]
[PatchLevel(Level.Safe)]
public static class Patch_Graphic_DrawFromDef
{
  public static void Prefix(ref Vector3 loc, ref Rot4 rot, ThingDef thingDef, ref float extraRotation,
    Graphic __instance)
  {
    if (VehicleMapUtility.FocusedOnVehicleMap(out var vehicle) && thingDef != null)
    {
      var def = thingDef.IsBlueprint ? thingDef.entityDefToBuild as ThingDef ?? thingDef : thingDef;
      var compProperties = def.GetCompProperties<CompProperties_FireOverlay>();
      var flag = __instance is Graphic_Flicker && compProperties != null;

      if (flag)
      {
        loc -= (def.graphicData?.DrawOffsetForRot(rot) ?? Vector3.zero) + compProperties.DrawOffsetForRot(rot);
      }

      var angle = vehicle.Angle - vehicle.Transform.rotation;
      var rot2 = rot;
      var baseRotInt = vehicle.FullRotation.RotForVehicleDraw().AsInt;

      bool SameMaterialByRot()
      {
        if (__instance is Graphic_Collection) return true;
        var rotation = new Rot4(rot2.AsInt + baseRotInt);
        return __instance.MatAt(rot2) == __instance.MatAt(rotation) &&
               __instance.DrawOffset(rot2) == __instance.DrawOffset(rotation);
      }

      if (def.size.x != def.size.z ||
          ((((__instance.data?.drawRotated ?? false) && (!__instance.data?.Linked ?? true)) || def.rotatable) &&
           !SameMaterialByRot()))
      {
        rot.AsInt += baseRotInt;
      }

      var flag2 = def.ShouldRotatedOnVehicle();
      if (flag2)
      {
        extraRotation -= angle;
      }

      var offset = __instance.data?.DrawOffsetForRot(rot) ?? Vector3.zero;
      if (flag)
      {
        var offset2 = compProperties.DrawOffsetForRot(rot);
        loc += (offset + offset2).RotatedBy(flag2 ? -angle : 0f);
      }
      else
      {
        var offset2 = offset.RotatedBy(flag2 ? -angle : 0f);
        loc += new Vector3(offset2.x - offset.x, 0f, offset2.z - offset.z);
      }

      //はしごとかのマップ端オフセット
      VehicleMapProps mapProps;
      if (thingDef.HasComp(typeof(CompVehicleEnterSpot)) &&
          (mapProps = vehicle.VehicleDef.GetModExtension<VehicleMapProps>()) != null)
      {
        var baseRot = new Rot8(rot2).Rotated(vehicle.FullRotation);
        loc += baseRot.Opposite.AsVector2.ToVector3() * mapProps.EdgeSpaceValue(vehicle.FullRotation, rot2.Opposite);
      }
    }
  }
}

[HarmonyPatch(typeof(Graphic_Shadow), nameof(Graphic_Shadow.DrawWorker))]
[PatchLevel(Level.Sensitive)]
public static class Patch_Graphic_Shadow_DrawWorker
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions,
    ILGenerator generator)
  {
    return new CodeMatcher(instructions, generator)
      .AddAltitudeFor(out var vehicle,
        getInstance: [CodeInstruction.LoadArgument(4)])
      .MatchStartForward(CodeMatch.Calls(CachedMethodInfo.g_Rot4_AsQuat))
      .SetOperandAndAdvance(CachedMethodInfo.m_Rot8_AsQuatRef)
      .CreateLabel(out var label)
      .Insert(
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Brfalse_S, label),
        new CodeInstruction(OpCodes.Ldloc_S, vehicle),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.m_FullAngleQuat),
        new CodeInstruction(OpCodes.Call, CachedMethodInfo.o_Quaternion_Multiply))
      .InstructionEnumeration();
  }
}

[HarmonyPatch(typeof(Frame), "DrawAt")]
[PatchLevel(Level.Sensitive)]
public static class Patch_Frame_DrawAt
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    foreach (var instruction in instructions)
    {
      if (instruction.OperandIs(CachedMethodInfo.m_Matrix4x4_SetTRS))
      {
        yield return CodeInstruction.LoadArgument(0);
        instruction.operand = CachedMethodInfo.m_SetTRSOnVehicle;
      }

      yield return instruction;
    }
  }
}

[HarmonyPatch(typeof(GenDraw), nameof(GenDraw.DrawFillableBar))]
[PatchLevel(Level.Safe)]
public static class Patch_GenDraw_DrawFillableBar
{
  public static bool Prefix(GenDraw.FillableBarRequest r)
  {
    VehiclePawnWithMap vehicle = null;
    if (r.rotation.AsInt >= 4 || Find.CurrentMap.IsNonFocusedVehicleMapOf(out vehicle))
    {
      var extraRotation = vehicle?.Transform.rotation ?? 0f;
      var rot = new Rot8(r.rotation.AsInt);
      var fullAngle = rot.Opposite.AsAngle + extraRotation;
      var vector = r.preRotationOffset.RotatedBy(fullAngle);
      r.center += new Vector3(vector.x, 0f, vector.y);
      Vector3 s = new(r.size.x + r.margin, 1f, r.size.y + r.margin);
      Matrix4x4 matrix = default;
      var quat = rot.AsQuat() * Quaternion.AngleAxis(extraRotation, Vector3.up);
      matrix.SetTRS(r.center, quat, s);
      Graphics.DrawMesh(MeshPool.plane10, matrix, r.unfilledMat, 0);
      if (r.fillPercent > 0.001f)
      {
        s = new Vector3(r.size.x * r.fillPercent, 1f, r.size.y);
        matrix = default;
        var pos = r.center + (Vector3.up * 0.01f);
        pos += new Vector3((-r.size.x * 0.5f) + (0.5f * r.size.x * r.fillPercent), 0f, 0f).RotatedBy(fullAngle);
        matrix.SetTRS(pos, quat, s);
        Graphics.DrawMesh(MeshPool.plane10, matrix, r.filledMat, 0);
      }

      return false;
    }

    return true;
  }
}

[HarmonyPatch(typeof(MapDrawer), "ViewRect", MethodType.Getter)]
[PatchLevel(Level.Cautious)]
public static class Patch_MapDrawer_ViewRect
{
  public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
  {
    return instructions.MethodReplacer(CachedMethodInfo.m_CellRect_ClipInsideMap,
      CachedMethodInfo.m_ClipInsideVehicleMap);
  }
}

[HarmonyPatch(typeof(GenView), nameof(GenView.ShouldSpawnMotesAt))]
[PatchLevel(Level.Safe)]
public static class Patch_GenView_ShouldSpawnMotesAt
{
  [HarmonyPatch([typeof(IntVec3), typeof(Map), typeof(bool)])]
  public static void Postfix(IntVec3 loc, Map map, ref bool __result)
  {
    __result = __result || map.IsVehicleMap &&
      map.BaseMapOrCaravan == Find.CurrentMap.BaseMapOrCaravan && loc.InBounds(map);
  }
}