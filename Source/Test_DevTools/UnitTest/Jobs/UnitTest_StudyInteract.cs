using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

internal sealed class UnitTest_StudyInteract(VehicleGroup group) : WorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("StudyInteract");

    public override void SetUp()
    {
        var component = Current.Game.GetComponent<GameComponent_Anomaly>();
        var cell = new IntVec3(Pawn.Map.Size.x - 3, 0, Pawn.Map.Size.z - 3);
        if (!component.MonolithSpawned || component.monolith.Map != Pawn.Map)
            component.SpawnNewMonolith(cell, Pawn.Map);
        else
        {
            component.ResetMonolith();
            component.monolith.Position = cell;
        }

        Pawn.pather.TryRecoverFromUnwalkablePosition();
        component.monolith.Activate(Pawn);
    }
    
    public override void TearDown()
    {
        Thing.allowDestroyNonDestroyable = true;
        Current.Game.GetComponent<GameComponent_Anomaly>().monolith.Destroy();
        Thing.allowDestroyNonDestroyable = false;
    }
}