using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using HarmonyLib;
using Verse;

namespace VehicleMapFramework.VMF_HarmonyPatches;

public static class PatchHelper
{
  public static IEnumerable<KeyValuePair<OpCode, object>> ReadMethodBodyWrapper(MethodBase method)
  {
    try
    {
      return PatchProcessor.ReadMethodBody(method);
    }
    catch (Exception ex)
    {
      VMF_Log.Warning(
        $"Autopatching to {method.FullDescription()} failed. It may be referencing outdated signatures. The patch will simply be skipped.\n{ex}");
      return [];
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

  public static CodeMatcher AddAltitudeFor(this CodeMatcher codeMatcher, out LocalBuilder vehicle,
    float offset = 0f, CodeMatch[] matches = null, CodeInstruction[] getInstance = null)
  {
    matches ??= [CodeMatch.Calls(CachedMethodInfo.m_Altitudes_AltitudeFor)];
    getInstance ??= [CodeInstruction.LoadArgument(0)];
    codeMatcher
      .MatchStartForward(matches)
      .Advance()
      .CreateLabel(out var label)
      .DeclareLocal(typeof(VehiclePawnWithMap), out vehicle)
      .InsertAndAdvance(getInstance)
      .InsertAndAdvance(
        new CodeInstruction(OpCodes.Ldloca_S, vehicle),
        CachedMethodInfo.m_IsOnNonFocusedVehicleMapOf.CallInstruction,
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
      if (listCount < 500)
      {
        for (var i = 0; i < listCount; i++)
        {
          ProcessInstruction(list[i]);
        }
      }
      else
      {
        Parallel.ForEach(list, ProcessInstruction);
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