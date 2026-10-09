using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

public static class PatchHelper
{
  private static readonly AccessTools.FieldRef<CodeMatcher, List<CodeInstruction>> codes =
    AccessTools.FieldRefAccess<CodeMatcher, List<CodeInstruction>>("codes");
  private static readonly AccessTools.FieldRef<CodeMatcher, ILGenerator> generator =
    AccessTools.FieldRefAccess<CodeMatcher, ILGenerator>("generator");
  
  public static CodeMatcher CreateCodeMatcherFast(IEnumerable<CodeInstruction> instructions, ILGenerator ilGenerator = null)
  {
    var matcher = new CodeMatcher();
    if (instructions is List<CodeInstruction> list)
      codes(matcher) = list;
    else
      codes(matcher).AddRange(instructions);
    generator(matcher) = ilGenerator;
    return matcher;
  }
  
  public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBodyWrapper(MethodBase method)
  {
    try
    {
      return PatchProcessor.ReadMethodBody(method);
    }
    catch (Exception ex)
    {
      VMF_Log.Warning(
        $"Auto patching to {method.FullDescription()} failed. It may be referencing outdated signatures. The patch will simply be skipped.\n{ex}");
      return [];
    }
  }

  extension(Harmony harmony)
  {
    public int GetPatchedMethodCount
    {
      get
      {
#if DEBUG || DEV
        return Harmony.GetAllPatchedMethods().Count(m => Harmony.GetPatchInfo(m).Owners.Contains(harmony.Id));
#else
        return 0;
#endif
      }
    }
  }
  
  public static IEnumerable<MethodBase> WhereCallsMethod(this IEnumerable<MethodBase> methods, params MethodBase[] targetMethods)
  {
    return methods.Where(method => method.CallsMethod(targetMethods));
  }

  public static bool CallsMethod(this MethodBase method, params MethodBase[] targetMethods)
  {
    return method is not null && ReadMethodBodyWrapper(method).Any(i =>
      i.Value is MethodBase operandMethod && targetMethods.Contains(operandMethod));
  }

  private static readonly FieldInfo f_allBuildingsColonist =
    AccessTools.Field(typeof(ListerBuildings), nameof(ListerBuildings.allBuildingsColonist));
  
  extension(CodeMatcher codeMatcher)
  {
    public CodeMatcher NonFocusedMapVehicleForThing(out LocalBuilder vehicle)
    {
      return codeMatcher.NonFocusedMapVehicle(out vehicle, CodeInstruction.LoadArgument(0));
    }
    
    public CodeMatcher NonFocusedMapVehicleForThingComp(out LocalBuilder vehicle)
    {
      return codeMatcher.NonFocusedMapVehicle(out vehicle,
        CodeInstruction.LoadArgument(0), CodeInstruction.LoadField(typeof(ThingComp), nameof(ThingComp.parent)));
    }
    
    public CodeMatcher NonFocusedMapVehicle(out LocalBuilder vehicle, params CodeInstruction[] getInstance)
    {
      if (codeMatcher.IsInvalid)
        codeMatcher.Reset();
      
      return codeMatcher
        .DeclareLocal(typeof(VehiclePawnWithMap), out vehicle)
        .InsertAndAdvance(getInstance)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldloca_S, vehicle),
          CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf.CallInstruction,
          new CodeInstruction(OpCodes.Pop));
    }
    
    public CodeMatcher AddAltitudeFor(LocalBuilder vehicle, float offset = 0f, CodeMatch[] matches = null)
    {
      matches ??= [CodeMatch.Calls(CachedMethodInfo.m_Altitudes_AltitudeFor)];
      codeMatcher
        .MatchStartForward(matches).Advance()
        .CreateLabel(out var label)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          new CodeInstruction(OpCodes.Brfalse_S, label),
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          CachedMethodInfo.m_YOffsetFull.CallInstruction);
      if (offset != 0f)
      {
        codeMatcher
          .InsertAndAdvance(
            new CodeInstruction(OpCodes.Ldc_R4, offset),
            new CodeInstruction(OpCodes.Add));
      }

      return codeMatcher;
    }

    public CodeMatcher AddExtraAngle(LocalBuilder vehicle)
    {
      return codeMatcher
        .CreateLabel(out var label)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          new CodeInstruction(OpCodes.Brfalse_S, label),
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          CachedMethodInfo.m_ExtraAngle.CallInstruction,
          new CodeInstruction(OpCodes.Add));
    }

    public CodeMatcher MultiplyFullAngleQuat(LocalBuilder vehicle)
    {
      return codeMatcher
        .CreateLabel(out var label)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          new CodeInstruction(OpCodes.Brfalse_S, label),
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          CachedMethodInfo.m_FullAngleQuat.CallInstruction,
          CachedMethodInfo.o_Quaternion_Multiply.CallInstruction);
    }

    public CodeMatcher MultiplyExtraAngleQuat(LocalBuilder vehicle)
    {
      return codeMatcher
        .CreateLabel(out var label)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          new CodeInstruction(OpCodes.Brfalse_S, label),
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          CachedMethodInfo.m_ExtraAngle.CallInstruction,
          CachedMethodInfo.g_Vector3_up.CallInstruction,
          CachedMethodInfo.m_Quaternion_AngleAxis.CallInstruction,
          CachedMethodInfo.o_Quaternion_Multiply.CallInstruction);
    }

    public CodeMatcher FocusVehicleAroundMethod(LocalBuilder vehicle, MethodInfo method)
    {
      return codeMatcher
        .MatchStartForward(CodeMatch.Calls(method))
        .DeclareLocal(typeof(Command_FocusVehicleMap.FocusVehicleScope), out var scope)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          CachedMethodInfo.m_FocusVehicle.CallInstruction,
          new CodeInstruction(OpCodes.Stloc_S, scope))
        .InsertAfterAndAdvance(
          new CodeInstruction(OpCodes.Ldloca_S, scope),
          CachedMethodInfo.m_FocusVehicleScope_Dispose.CallInstruction);
    }

    public CodeMatcher RotatedByVehicleExtraAngle(LocalBuilder vehicle)
    {
      return codeMatcher
        .CreateLabel(out var label)
        .InsertAndAdvance(
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          new CodeInstruction(OpCodes.Brfalse_S, label),
          new CodeInstruction(OpCodes.Ldloc_S, vehicle),
          CachedMethodInfo.m_ExtraAngle.CallInstruction,
          CachedMethodInfo.m_RotatedBy.CallInstruction);
    }
  }


  extension(MethodInfo methodInfo)
  {
    public CodeInstruction CallInstruction => new (OpCodes.Call, methodInfo);
    public CodeInstruction CallvirtInstruction => new (OpCodes.Callvirt, methodInfo);
  }

  private static class Params<T> where T : struct, ITuple
  {
    // ReSharper disable once StaticMemberInGenericType
    [ThreadStatic] private static (MethodBase, MethodBase)[] @params;

    public static (MethodBase, MethodBase)[] Get(T tuple)
    {
      @params ??= new (MethodBase, MethodBase)[tuple.Length]; 
      for (var i = 0; i < tuple.Length; i++)
        @params[i] = ((MethodBase, MethodBase))tuple[i];
      return @params;
    }
  }
  
  extension(IEnumerable<CodeInstruction> instructions)
  {
    public List<CodeInstruction> MethodReplacer(MethodInfo from, MethodInfo to)
    {
      return instructions.MethodReplacer(Params<ValueTuple<(MethodBase, MethodBase)>>.Get(new ValueTuple<(MethodBase, MethodBase)>((from, to))));
    }
    
    public List<CodeInstruction> MethodReplacer((MethodInfo, MethodInfo) pair1, (MethodInfo, MethodInfo) pair2)
    {
      return instructions.MethodReplacer(Params<((MethodBase, MethodBase), (MethodBase, MethodBase))>.Get((pair1, pair2)));
    }
    
    public List<CodeInstruction> MethodReplacer((MethodInfo, MethodInfo) pair1, (MethodInfo, MethodInfo) pair2, (MethodInfo, MethodInfo) pair3)
    {
      return instructions.MethodReplacer(Params<((MethodBase, MethodBase), (MethodBase, MethodBase), (MethodBase, MethodBase))>.Get((pair1, pair2, pair3)));
    }
    
    public List<CodeInstruction> MethodReplacer(params (MethodBase from, MethodBase to)[] pairs)
    {
      var list = instructions as List<CodeInstruction> ?? [.. instructions];
      var pairCount = pairs.Length;
      var listCount = list.Count;
      for (var i = 0; i < listCount; i++)
      {
        ProcessInstruction(list[i]);
      }

      return list;

      void ProcessInstruction(CodeInstruction instruction)
      {
        if (instruction.operand is MethodBase methodBase)
        {
          for (var j = 0; j < pairCount; j++)
          {
            var pair = pairs[j];
            if (methodBase == pair.from)
            {
              instruction.opcode = pair.to.IsConstructor ? OpCodes.Newobj : OpCodes.Call;
              instruction.operand = pair.to;
              break;
            }
          }
        }
      }
    }

    public IEnumerable<CodeInstruction> AddAllBuildingsColonistForThingInstance(int argumentIndex = 0)
    {
      foreach (var instruction in instructions)
      {
        yield return instruction;
        if (instruction.LoadsField(f_allBuildingsColonist))
        {
          yield return CodeInstruction.LoadArgument(argumentIndex);
          yield return CachedMethodInfo.m_AddColonistBuildingList.CallInstruction;
        }
      }
    }
  }
}