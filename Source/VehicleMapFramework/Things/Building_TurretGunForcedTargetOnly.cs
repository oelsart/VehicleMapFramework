using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class Building_TurretGunForcedTargetOnly : Building_TurretGun
{
    private bool canSetForcedTargetThisTick;

    // ターゲッターによりTargetMapがセットされGUI上で不必要にターゲットにオフセットがかかることを防ぐ
    protected override bool CanSetForcedTarget =>
        canSetForcedTargetThisTick || !forcedTarget.IsValid && interactableComp is not CompInteractableRocketswarmLauncher;

    protected override void Tick()
    {
        canSetForcedTargetThisTick = true;
        base.Tick();
        canSetForcedTargetThisTick = false;
        if (!currentTargetInt.IsValid && forcedTarget.IsValid)
        {
            currentTargetInt = forcedTarget;
            top.TurretTopTick();
        }
    }

    public override LocalTargetInfo TryFindNewTarget()
    {
        return forcedTarget;
    }
}
