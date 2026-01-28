using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class TileFinderSea
{
    private static readonly List<(PlanetTile tile, int traversalDistance)> tmpTiles = [];

	private static readonly List<PlanetTile> tmpPlayerTiles = [];

	private static List<BiomeDef> WaterBiomes { get; } = DefDatabase<BiomeDef>.AllDefs.Where(biome => biome.isWaterBiome).ToList();

	public static bool TryFindNewSiteTile(out PlanetTile tile, int minDist = 7, int maxDist = 27, bool allowCaravans = false, List<LandmarkDef> allowedLandmarks = null, float selectLandmarkChance = 0.5f, bool canSelectComboLandmarks = true, TileFinderMode tileFinderMode = TileFinderMode.Near, bool exitOnFirstTileFound = false, bool canBeSpace = false, PlanetLayer layer = null, Predicate<PlanetTile> validator = null)
	{
		return TryFindNewSiteTile(out tile, PlanetTile.Invalid, minDist, maxDist, allowCaravans, allowedLandmarks, selectLandmarkChance, canSelectComboLandmarks, tileFinderMode, exitOnFirstTileFound, canBeSpace, layer, validator);
	}

	public static bool TryFindNewSiteTile(out PlanetTile tile, PlanetTile nearTile, int minDist = 7, int maxDist = 27, bool allowCaravans = false, List<LandmarkDef> allowedLandmarks = null, float selectLandmarkChance = 0.5f, bool canSelectComboLandmarks = true, TileFinderMode tileFinderMode = TileFinderMode.Near, bool exitOnFirstTileFound = false, bool canBeSpace = false, PlanetLayer layer = null, Predicate<PlanetTile> validator = null)
	{
		var flag = ModsConfig.OdysseyActive && Rand.ChanceSeeded(selectLandmarkChance, Gen.HashCombineInt(Find.TickManager.TicksGame, 18271));
		if (!nearTile.Valid && !TileFinder.TryFindRandomPlayerTile(out nearTile, allowCaravans, canBeSpace: true))
		{
			tile = PlanetTile.Invalid;
			return false;
		}
		layer ??= nearTile.Layer;
		if (!canBeSpace && layer.Def.isSpace && !Find.WorldGrid.TryGetFirstAdjacentLayerOfDef(nearTile, PlanetLayerDefOf.Surface, out layer))
		{
			Find.WorldGrid.PlanetLayers.Where(t => !t.Value.Def.isSpace).RandomElement().Deconstruct(out _, out var planetLayer);
			layer = planetLayer;
		}
		var landmarkMode = flag ? FastTileFinder.LandmarkMode.Required : FastTileFinder.LandmarkMode.Any;
		var query = new FastTileFinder.TileQueryParams(nearTile, minDist, maxDist, landmarkMode, reachable: true, Hilliness.Undefined, Hilliness.Undefined, checkBiome: true, validSettlement: true, canSelectComboLandmarks);
		var list = layer.FastTileFinder.Query(query, WaterBiomes, allowedLandmarks);
		if (validator != null)
		{
			for (var num2 = list.Count - 1; num2 >= 0; num2--)
			{
				if (!validator(list[num2]))
				{
					list.RemoveAt(num2);
				}
			}
		}
		if (list.Empty())
		{
			if (TryFillFindSeaTile(layer.GetClosestTile_NewTemp(nearTile), out tile, minDist, maxDist, allowedLandmarks, canSelectComboLandmarks, tileFinderMode, exitOnFirstTileFound, validator, flag))
			{
				return true;
			}
			tile = PlanetTile.Invalid;
			return false;
		}
		tile = list.RandomElement();
		return true;
	}

	private static bool TryFillFindSeaTile(PlanetTile root, out PlanetTile tile, int minDist = 7, int maxDist = 27, List<LandmarkDef> allowedLandmarks = null, bool canSelectComboLandmarks = true, TileFinderMode tileFinderMode = TileFinderMode.Near, bool exitOnFirstTileFound = false, Predicate<PlanetTile> validator = null, bool pickLandmark = false)
	{
		if (TileFinder.TryFindTileWithDistance(root, minDist, maxDist, out tile, c => ValidTile(c, pickLandmark), tileFinderMode, exitOnFirstTileFound))
		{
			return true;
		}
		if (pickLandmark && TileFinder.TryFindTileWithDistance(root, minDist, maxDist, out tile, c => ValidTile(c, landmark: false), tileFinderMode, exitOnFirstTileFound))
		{
			return true;
		}
		tile = PlanetTile.Invalid;
		return false;
		
		bool ValidTile(PlanetTile x, bool landmark)
		{
			if (ModsConfig.OdysseyActive)
			{
				var landmarkHere = Find.World.landmarks[x];
				switch (landmark)
				{
					case false when landmarkHere != null:
					case true when landmarkHere == null || (!allowedLandmarks.NullOrEmpty() && !allowedLandmarks.Any(l => l == landmarkHere.def)):
						return false;
				}

				if (!canSelectComboLandmarks && landmarkHere is { isComboLandmark: true })
				{
					return false;
				}
			}
			if (!Find.WorldObjects.AnyWorldObjectAt(x) && WaterBiomes.Contains(x.Tile.PrimaryBiome))
			{
				return validator?.Invoke(x) ?? true;
			}
			return false;
		}
	}
}