using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class PawnFlyer_PersistentJob : PawnFlyer
{
    public event Action OnLanded; 
    
    private readonly AccessTools.FieldRef<PawnFlyer, JobQueue> jobQueue =
        AccessTools.FieldRefAccess<PawnFlyer, JobQueue>("jobQueue");
    
    protected override void RespawnPawn()
    {
        // 保持しているJobを逃がしてダミーのJobをセットし、保持していたJobを後からスタートさせるややハック的な実装
        if ((jobQueue(this)?.FirstOrDefault(), FlyingPawn) is ({ } queuedJob, { } pawn))
        {
            var job = queuedJob.job;
            queuedJob.job = JobMaker.MakeJob(JobDefOf.Wait_Combat);
            base.RespawnPawn();
            pawn.jobs.StartJob(job, JobCondition.InterruptForced);
            OnLanded?.Invoke();
            return;
        }
        base.RespawnPawn();
        OnLanded?.Invoke();
    }
}