using System.Linq;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

public class QuestNode_GetSeaSiteTile : QuestNode_GetSiteTile
{
    protected override bool TestRunInt(Slate slate)
    {
        if (!TryFindTile(slate, out var tile))
        {
            return false;
        }
        if (clampRangeBySiteParts.GetValue(slate) == true && sitePartDefs.GetValue(slate) == null)
        {
            return false;
        }
        slate.Set(storeAs.GetValue(slate), tile);
        return true;
    }

    protected override void RunInt()
    {
        var slate = QuestGen.slate;
        if (!slate.TryGet<int>(storeAs.GetValue(slate), out _) && TryFindTile(QuestGen.slate, out var tile))
        {
            QuestGen.slate.Set(storeAs.GetValue(slate), tile);
        }
    }

    private bool TryFindTile(Slate slate, out PlanetTile tile)
    {
        var value = canSelectSpace.GetValue(slate);
        var nearTile = (slate.Get<Map>("map") ?? (value ? Find.RandomPlayerHomeMap : Find.RandomSurfacePlayerHomeMap))?.Tile ?? PlanetTile.Invalid;
        if (nearTile.Valid && nearTile.LayerDef.isSpace && !value)
        {
            nearTile = PlanetTile.Invalid;
        }
        var num = int.MaxValue;
        var value2 = clampRangeBySiteParts.GetValue(slate);
        if (value2.HasValue && value2.Value)
        {
            num = sitePartDefs.GetValue(slate)
                .Where(item => item.conditionCauserDef != null)
                .Aggregate(num,
                    (current,
                        item) => Mathf.Min(current,
                        item.conditionCauserDef.GetCompProperties<CompProperties_CausesGameCondition>()
                            .worldRange));
        }
        if (!slate.TryGet<IntRange>("siteDistRange", out var var))
        {
            var = new IntRange(7, Mathf.Min(27, num));
        }
        else if (num != int.MaxValue)
        {
            var = new IntRange(Mathf.Min(var.min, num), Mathf.Min(var.max, num));
        }
        var tileFinderMode = preferCloserTiles.GetValue(slate) ? TileFinderMode.Near : TileFinderMode.Random;
        return TileFinderSea.TryFindNewSiteTile(out tile, nearTile, var.min, var.max, allowCaravans.GetValue(slate), allowedLandmarks.GetValue(slate), 0.5f, canSelectComboLandmarks.GetValue(slate), tileFinderMode, exitOnFirstTileFound: false, value);
    }
}