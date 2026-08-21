using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class TargetMapManager(World world) : WorldComponent(world)
{
  private Dictionary<Thing, TargetInfo> tmpTargetInfoDic = [];
  private List<Thing> tmpKeys = [];
  private List<TargetInfo> tmpValues = [];

  public ConditionalWeakTable<Thing, StrongBox<TargetInfo>> TargetInfoTable { get; } = [];

  internal StrongBox<TargetInfo> GetOrCreateTargetInfo(Thing thing)
  {
    if (thing is null) return null;
    return TargetInfoTable.GetValue(thing,
      _ =>
      {
        var box = new StrongBox<TargetInfo>
        {
          Value = TargetInfo.Invalid
        };
        return box;
      });
  }

  public override void FinalizeInit(bool fromLoad)
  {
    TargetMapUtility.manager = this;
  }

  public override void WorldComponentTick()
  {
    if (GenTicks.IsTickInterval(10800))
    {
      foreach (var pair in
               TargetInfoTable.Where(pair => pair is not { Value.Value.IsValid: true } and { Key: not null }))
      {
        tmpKeys.Add(pair.Key);
      }
      foreach (var thing in tmpKeys)
      {
        TargetInfoTable.Remove(thing);
      }
      tmpKeys.Clear();
    }
  }

  public override void ExposeData()
  {
    switch (Scribe.mode)
    {
      case LoadSaveMode.Saving:
      {
        var targetInfoDic = TargetInfoTable
          .Select(pair => (pair.Key, pair.Value?.Value ?? TargetInfo.Invalid))
          .Where(tuple => tuple is { Key: not null, Item2: { IsValid: true, Map: not null } })
          .ToDictionary(pair => pair.Key, pair => pair.Item2);
        Scribe_Collections.Look(ref targetInfoDic,
          "TargetInfo",
          LookMode.Reference,
          LookMode.TargetInfo,
          ref tmpKeys,
          ref tmpValues,
          false);
        tmpTargetInfoDic = null;
        break;
      }
      case LoadSaveMode.LoadingVars:
      case LoadSaveMode.ResolvingCrossRefs:
      {
        Scribe_Collections.Look(ref tmpTargetInfoDic,
          "TargetInfo",
          LookMode.Reference,
          LookMode.TargetInfo,
          ref tmpKeys,
          ref tmpValues,
          false);
        break;
      }
      case LoadSaveMode.PostLoadInit:
      {
        if (tmpTargetInfoDic is not null)
        {
          foreach (var pair in tmpTargetInfoDic)
          {
            if (pair.Key is null) continue;
            TargetInfoTable.Add(pair.Key, new StrongBox<TargetInfo>(pair.Value));
          }
          tmpTargetInfoDic = null;
        }
        tmpKeys ??= [];
        tmpValues ??= [];
        break;
      }
      case LoadSaveMode.Inactive:
      default: break;
    }
  }
}