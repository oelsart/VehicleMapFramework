using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class JobDriver_GotoDestMap : JobDriverAcrossMaps
{
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
                pawn.jobs.StartJob(nextJob, JobCondition.InterruptForced, keepCarryingThingOverride: true);
            };
            yield return toil;
        }
    }

    public override void ExposeData()
    {
        Scribe_Deep.Look(ref nextJob, "nextJob");
        base.ExposeData();
    }

    public Job nextJob;
}
