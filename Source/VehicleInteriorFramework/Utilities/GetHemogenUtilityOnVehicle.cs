using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public class GetHemogenUtilityOnVehicle
    {
        public static AcceptanceReport CanFeedOnPrisoner(Pawn bloodfeeder, Pawn prisoner)
        {
            if (prisoner.WouldDieFromAdditionalBloodLoss(0.4499f))
            {
                return "CannotFeedOnWouldKill".Translate(prisoner.Named("PAWN"));
            }
            if (!prisoner.IsPrisonerOfColony || !prisoner.guest.PrisonerIsSecure || prisoner.guest.IsInteractionDisabled(PrisonerInteractionModeDefOf.Bloodfeed) || prisoner.IsForbidden(bloodfeeder) || !bloodfeeder.CanReserveAndReach(prisoner.Map, prisoner, PathEndMode.OnCell, bloodfeeder.NormalMaxDanger(), 1, -1, null, false, out _, out _) || prisoner.InAggroMentalState)
            {
                return false;
            }
            return true;
        }
    }
}
