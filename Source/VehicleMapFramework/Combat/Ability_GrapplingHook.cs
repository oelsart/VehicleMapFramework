using RimWorld;
using SmashTools;
using Verse;

namespace VehicleMapFramework;

public class Ability_GrapplingHook : Ability
{
    public Ability_GrapplingHook()
    {
    }

    public Ability_GrapplingHook(Pawn pawn) : base(pawn)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, AbilityDef def) : base(pawn, def)
    {
    }

    public Ability_GrapplingHook(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def)
    {
    }
    
    public override LocalTargetInfo AIGetAOETarget()
    {
        var target = base.AIGetAOETarget();
        if (target.IsValid || !def.ai_SearchAOEForTargets) return target;
        
        var attackVerb = verb;
        var targetParams = attackVerb.targetParams;
        var canTargetLocations = targetParams.canTargetLocations;
        var map = pawn.Map;
        var groundMap = map.GroundMap;
        var component = groundMap.GetCachedMapComponent<VehicleMapGrid>();
        var cells = GenRadial.RadialCellsAround(pawn.PositionOnBaseMap,
            attackVerb.verbProps.EffectiveMinRange(true), attackVerb.EffectiveRange);
        foreach (var cell in cells)
        {
            if (!cell.InBounds(groundMap)) continue;
            var vehicle = component.VehicleAt(cell);
            var map2 = vehicle?.VehicleMap;
            if (map2 is not null && map != map2)
            {
                var cell2 = cell.ToVehicleMapCoord(vehicle);
                if (!cell2.InBounds(map2)) continue;
                
                pawn.TargetMap = map2;
                if (canTargetLocations && attackVerb.ValidateTarget(cell2, false) && attackVerb.CanHitTarget(cell2))
                {
                    return cell2;
                }
                pawn.RemoveTargetInfo();
            }
        }
        
        return LocalTargetInfo.Invalid;
    }

    public virtual void OnHit(ZiplineEnd ziplineEnd)
    {
        pawn.TargetMap = ziplineEnd.Map;
        JumpUtility.DoJump(pawn, ziplineEnd, null, verb.verbProps, this, ziplineEnd);
    }
}