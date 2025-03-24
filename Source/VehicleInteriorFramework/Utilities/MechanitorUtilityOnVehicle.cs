using RimWorld;
using System;
using Verse;

namespace VehicleInteriors
{
    public static class MechanitorUtilityOnVehicle
    {
        [Obsolete]
        public static string GetMechGestationJobString(JobDriver_DoBillAcrossMaps job, Pawn mechanitor, Bill_Mech bill)
        {
            switch ((int)bill.State)
            {
                case 0:
                    if (job.AnyIngredientsQueued)
                    {
                        return "LoadingMechGestator".Translate() + ".";
                    }

                    if (job.AnyIngredientsQueued)
                    {
                        break;
                    }

                    goto case 1;
                case 1:
                    return "InitMechGestationCycle".Translate() + ".";
                case 3:
                    return "InitMechBirth".Translate() + ".";
            }

            Log.Error("Unknown mech gestation job state.");
            return null;
        }
    }
}
