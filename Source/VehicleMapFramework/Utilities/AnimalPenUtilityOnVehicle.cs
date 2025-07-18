using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public static class AnimalPenUtilityOnVehicle
{
    public static bool CanUseAndReach(Pawn animal, CompAnimalPenMarker penMarker, bool allowUnenclosedPens, Pawn roper = null)
    {
        bool flag = false;
        return CheckUseAndReach(animal, penMarker, allowUnenclosedPens, roper, ref flag, ref flag, ref flag);
    }

    public static bool CheckUseAndReach(Pawn animal, CompAnimalPenMarker penMarker, bool allowUnenclosedPens, Pawn roper, ref bool foundEnclosed, ref bool foundUsable, ref bool foundReachable)
    {
        if (!allowUnenclosedPens && penMarker.PenState.Unenclosed)
        {
            return false;
        }
        foundEnclosed = true;
        if (!penMarker.AcceptsToPen(animal))
        {
            return false;
        }
        if (roper == null && penMarker.parent.IsForbidden(Faction.OfPlayer))
        {
            return false;
        }
        if (roper != null && penMarker.parent.IsForbidden(roper))
        {
            return false;
        }
        foundUsable = true;
        bool flag;
        if (roper == null)
        {
            TraverseParms traverseParams = TraverseParms.For(TraverseMode.PassDoors, Danger.Deadly, false, false, false).WithFenceblockedOf(animal);
            flag = CrossMapReachabilityUtility.CanReach(animal.Map, animal.Position, penMarker.parent, PathEndMode.Touch, traverseParams, penMarker.parent.Map, out _, out _);
        }
        else
        {
            TraverseParms traverseParams2 = TraverseParms.For(roper, Danger.Deadly, TraverseMode.ByPawn, false, false, false).WithFenceblockedOf(animal);
            flag = CrossMapReachabilityUtility.CanReach(animal.Map, animal.Position, penMarker.parent, PathEndMode.Touch, traverseParams2, penMarker.parent.Map, out _, out _);
        }
        if (!flag)
        {
            return false;
        }
        foundReachable = true;
        return true;
    }
}
