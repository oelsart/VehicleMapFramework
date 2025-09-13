using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobDriver_GotoDestMap : JobDriverAcrossMaps
{
    public static readonly ThinkNode_JobFromGotoDestMap thinkNode = new();

    public Job nextJob;

    protected override string ReportStringProcessed(string str)
    {
        return nextJob?.GetReport(pawn);
    }

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        var map = pawn.Map;
        pawn.VirtualMapTransfer(DestMap); //ScanCellのWorkなどの場合にVirtualMapTransferは必要
        try
        {
            return nextJob?.TryMakePreToilReservations(pawn, false) ?? true;
        }
        finally
        {
            pawn.VirtualMapTransfer(map);
        }
    }

    //次のJobDriverがNotify_Starting内でReserveを行っている場合があるため、先に次のJobのNotify_Startingを呼ぶ必要がある
    public override void Notify_Starting()
    {
        base.Notify_Starting();
        nextJob?.GetCachedDriver(pawn).Notify_Starting();
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        base.MakeNewToils();
        if (ShouldEnterTargetAMap)
        {
            foreach (var toil in GotoTargetMap(TargetIndex.A)) yield return toil;
        }
        if (nextJob != null)
        {
            var toil = ToilMaker.MakeToil("TryTakeNextJob");
            toil.defaultCompleteMode = ToilCompleteMode.Instant;
            toil.initAction = () =>
            {
                pawn.jobs.StartJob(nextJob, JobCondition.InterruptForced, thinkNode.DeepCopy(), keepCarryingThingOverride: true, preToilReservationsCanFail: true);
            };
            yield return toil;
        }
    }

    public override void ExposeData()
    {
        Scribe_Deep.Look(ref nextJob, "nextJob");
        base.ExposeData();
    }

    public class ThinkNode_JobFromGotoDestMap : ThinkNode
    {
        public override ThinkResult TryIssueJobPackage(Pawn pawn, JobIssueParams jobParams) => throw new NotImplementedException();
    }
}
