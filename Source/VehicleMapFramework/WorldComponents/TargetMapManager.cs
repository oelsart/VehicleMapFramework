using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

public class TargetMapManager(World world) : WorldComponent(world)
{
    private List<Thing> tmpKeys = [];

    private List<TargetInfo> tmpValues = [];

    public ConditionalWeakTable<Thing, StrongBox<TargetInfo>> TargetInfoTable { get; } = [];

    internal StrongBox<TargetInfo> GetOrCreateTargetInfo(Thing thing)
    {
        if (thing is null) return null;
        VMF_Log.DebugMessage($"GetOrCreateTargetInfo: {thing}");
        return TargetInfoTable.GetValue(thing, _ =>
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
            foreach (var pair in TargetInfoTable.Where(pair => !pair.Value.Value.IsValid))
                tmpKeys.Add(pair.Key);
            foreach (var thing in tmpKeys.Where(thing => thing is not null))
                TargetInfoTable.Remove(thing);
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
                    .Select(pair => (pair.Key, pair.Value.Value))
                    .Where(tuple => tuple.Value.IsValid)
                    .ToDictionary(pair => pair.Key, pair => pair.Value);
                Scribe_Collections.Look(ref targetInfoDic, "TargetInfo", LookMode.Reference, LookMode.TargetInfo, ref tmpKeys, ref tmpValues, false);
                break;
            }
            case LoadSaveMode.LoadingVars:
            {
                Dictionary<Thing, TargetInfo> targetInfoDic = null;
                Scribe_Collections.Look(ref targetInfoDic, "TargetInfo", LookMode.Reference, LookMode.TargetInfo, ref tmpKeys, ref tmpValues, false);
                foreach (var pair in targetInfoDic)
                    TargetInfoTable.Add(pair.Key, new StrongBox<TargetInfo>(pair.Value));
                break;
            }
            case LoadSaveMode.Inactive:
            case LoadSaveMode.ResolvingCrossRefs:
            case LoadSaveMode.PostLoadInit:
            default: break;
        }
    }
}
