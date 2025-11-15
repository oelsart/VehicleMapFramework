using RimWorld;
using Vehicles.UnitTesting;
using Verse;

namespace VehicleMapFramework.Test_Logics;

[HotSwap]
internal sealed class UnitTest_StudyInteract(VehicleGroup group) : WorkGiverTestBase(group)
{
    public override WorkGiverDef WorkGiverDef => DefDatabase<WorkGiverDef>.GetNamed("StudyInteract");

    public override void SetUp()
    {
        var component = Current.Game.GetComponent<GameComponent_Anomaly>();
        CellFinder.TryFindRandomReachableCellNearPosition(Pawn.Position, Pawn.Position,Pawn.Map, 20f,
            TraverseParms.For(Pawn),
            c => GenConstruct.CanPlaceBlueprintAt(ThingDefOf.VoidMonolith, c, Rot4.North, Pawn.Map),
            null, out var cell);
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