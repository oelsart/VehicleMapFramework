using RimWorld;
using Verse;
using Verse.AI;

namespace VehicleInteriors
{
    public static class WorkGiverUtilityOnVehicle
    {
        public static Job HaulStuffOffBillGiverJob(Pawn pawn, IBillGiver giver, Thing thingToIgnore)
        {
            foreach (IntVec3 ingredientStackCell in giver.IngredientStackCells)
            {
                Thing thing = giver.Map.thingGrid.ThingAt(ingredientStackCell, ThingCategory.Item);
                if (thing != null && thing != thingToIgnore)
                {
                    return HaulAIAcrossMapsUtility.HaulAsideJobFor(pawn, thing);
                }
            }

            return null;
        }
    }
}
