using RimWorld;
using SmashTools;
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
        if (Faction != Faction.OfPlayer)
        {
            TryActivateBurst();
        }
        if (!currentTargetInt.IsValid && forcedTarget.IsValid)
        {
            currentTargetInt = forcedTarget;
            top.TurretTopTick();
        }
    }

    public override LocalTargetInfo TryFindNewTarget()
    {
        if (Faction == Faction.OfPlayer) return LocalTargetInfo.Invalid;
        
        var attackVerb = AttackVerb;
        var targetParams = attackVerb.targetParams;
        var canTargetLocations = targetParams.canTargetLocations;
        var canTargetThings = targetParams.canTargetPawns || targetParams.canTargetBuildings || targetParams.canTargetItems ||
                              targetParams.canTargetPlants || targetParams.canTargetSelf || targetParams.canTargetFires;
        var map = Map;
        var groundMap = this.GroundMap;
        var component = groundMap.GetCachedMapComponent<VehicleMapGrid>();
        var cells = GenRadial.RadialCellsAround(this.PositionOnBaseMap,
            attackVerb.verbProps.minRange, attackVerb.verbProps.range);
        foreach (var cell in cells)
        {
            if (!cell.InBounds(groundMap)) continue;
            var vehicle = component.VehicleAt(cell);
            var map2 = vehicle?.VehicleMap;
            if (map2 is not null && map != map2 && vehicle.Faction != Faction)
            {
                var cell2 = cell.ToVehicleMapCoord(vehicle);
                if (!cell2.InBounds(map2)) continue;
                
                this.TargetMap = map2;
                if (canTargetLocations && attackVerb.ValidateTarget(cell2, false) && attackVerb.CanHitTarget(cell2))
                {
                    return cell2;
                }
                this.RemoveTargetInfo();
                
                if (canTargetThings)
                {
                    foreach (var thing in map2.thingGrid.ThingsListAtFast(cell2))
                    {
                        if (targetParams.CanTarget(thing) && attackVerb.ValidateTarget(thing, false) &&
                            attackVerb.CanHitTarget(thing))
                        {
                            return thing;
                        }
                    }
                }
            }
        }

        return LocalTargetInfo.Invalid;
    }
}
