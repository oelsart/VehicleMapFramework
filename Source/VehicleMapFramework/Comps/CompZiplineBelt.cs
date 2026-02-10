using Verse;

namespace VehicleMapFramework;

public class CompZiplineBelt : ThingComp
{
    public override bool CompAllowVerbCast(Verb verb)
    {
        return !verb.IsMeleeAttack ||
               verb.caster.Map.attackTargetsCache.TargetsHostileToFaction(verb.caster.Faction).Any();
    }
}