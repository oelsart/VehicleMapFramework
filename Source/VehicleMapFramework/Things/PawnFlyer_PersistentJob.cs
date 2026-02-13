using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class PawnFlyer_PersistentJob : PawnFlyer
{
    private readonly AccessTools.FieldRef<PawnFlyer, JobQueue> jobQueue =
        AccessTools.FieldRefAccess<PawnFlyer, JobQueue>("jobQueue");
    
    protected override void RespawnPawn()
    {
        // 保持しているJobを逃がしてダミーのJobをセットし、保持していたJobを後からスタートさせるややハック的な実装
        if ((jobQueue(this)?.FirstOrDefault(), FlyingPawn) is ({ } jobQueue2, { } pawn))
        {
            var job = jobQueue2.job;
            jobQueue2.job = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            base.RespawnPawn();
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            return;
        }
        base.RespawnPawn();
    }
}