using RimWorld;
using Verse;

namespace VehicleMapFramework.Test_Logics;

public static class TestUtility
{
    public static bool EvacuateFromTestArea(Pawn pawn)
    {
        var map = pawn.Map;
        if (map is null)
            return false;
        const int padding = 5;
        var size = map.Size;
        IntVec3[] candidates =
        [
            new(padding, 0, padding),
            new(size.x / 2, 0, padding),
            new(size.x - padding, 0, padding),
            new(padding, 0, size.z / 2),
            new(size.x - padding, 0, size.z / 2),
            new(padding, 0, size.z - padding),
            new(size.x / 2, 0, size.z - 10),
            new(size.x - padding, 0, size.z - padding)
        ];
        foreach (var candidate in candidates)
        {
            var cell = RCellFinder.BestOrderedGotoDestNear(candidate, pawn);
            if (cell.IsValid)
            {
                pawn.Position = cell;
                return true;
            }
        }

        return false;
    }

    public static void MakePawnPerfect(Pawn pawn)
    {
        foreach (var skillDef in DefDatabase<SkillDef>.AllDefs)
        {
            pawn.skills.Learn(skillDef, 100000000f);
        }
        pawn.health.RemoveAllHediffs();
        pawn.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
        pawn.story.AllBackstories?.Clear();
        pawn.story.traits.allTraits?.Clear();
        pawn.Notify_DisabledWorkTypesChanged();
        foreach (var workTypeDef in DefDatabase<WorkTypeDef>.AllDefs)
        {
            pawn.workSettings.SetPriority(workTypeDef, 3);
        }
    }
}