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
    
    public new LocalTargetInfo ForcedTarget
    {
        get => forcedTarget;
        set => forcedTarget = value;
    }
    
    protected override void Tick()
    {
        if (AttackVerb is Verb_LaunchZipline { ziplineEnd: { Spawned: true } ziplineEnd })
        {
            var rotation = (ziplineEnd.DrawPos - DrawPos).AngleFlat();
            if (this.IsOnNonFocusedVehicleMapOf(out var vehicle) && !CurrentTarget.IsValid)
            {
                rotation = Ext_Math.RotateAngle(rotation, -vehicle.FullAngle);
            }
            top.CurRotation = rotation;
            return;
        }
        canSetForcedTargetThisTick = true;
        base.Tick();
        canSetForcedTargetThisTick = false;
    }

    protected override void TickInterval(int delta)
    {
        base.TickInterval(delta);
        if (AttackVerb is Verb_LaunchZipline verb_LaunchZipline)
        {
            if (verb_LaunchZipline.ziplineEnd is { Spawned: true } ziplineEnd)
            {
                if (ziplineEnd is ZiplineEnd && (forcedTarget != ziplineEnd ||
                                                 !verb_LaunchZipline.TryFindShootLineFromToOnVehicle(
                                                     this.PositionOnBaseMap, ziplineEnd.PositionOnBaseMap, out _)))
                    ziplineEnd.Destroy();
                return;
            }
            if (Faction != Faction.OfPlayer && !IsStunned && burstCooldownTicksLeft <= 0)
                TryActivateBurst();
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
        var minIndex = GenRadial.NumCellsInRadius(attackVerb.verbProps.EffectiveMinRange(true));
        var maxIndex = GenRadial.NumCellsInRadius(attackVerb.EffectiveRange);
        var center = this.PositionOnBaseMapSpawned;
        for (var i = minIndex; i < maxIndex; i++)
        {
            var cell = center + GenRadial.RadialPattern[i];
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
