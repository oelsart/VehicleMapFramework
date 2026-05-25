using System;
using System.Collections.Generic;
using System.Linq;
using SmashTools;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class GenRadialDirectional
{
  private const int RadialPatternCount = 20000;
  private const int MAX_RADIUS = 79;
  private static readonly IntVec3[][] Patterns = new IntVec3[8][];
  private static readonly float[][] PatternRadii = new float[8][];
  private static readonly int[][] LengthSquaredToIndexArrays = new int[8][];

  static GenRadialDirectional()
  {
    var length = GenRadial.NumCellsInRadius(MAX_RADIUS);
    var tmpLists = new List<IntVec3>[8];
    for (var i = 0; i < 8; i++)
    {
      var capacity = i < 4 ? length / 2 : length / 4;
      tmpLists[i] = new List<IntVec3>(capacity);
    }

    for (var i = 0; i < length; i++)
    {
      var cell = GenRadial.RadialPattern[i];
      for (var r = 0; r < 8; r++)
      {
        if (IsInRotation(cell, new Rot8(r)))
        {
          tmpLists[r].Add(cell);
        }
      }
    }

    for (var r = 0; r < 8; r++)
    {
      Patterns[r] = tmpLists[r].ToArray();
      PatternRadii[r] = Patterns[r].Select(c => c.LengthHorizontal).ToArray();
      BuildLookupTable(r);
    }
    return;

    static void BuildLookupTable(int r)
    {
      var table = new int[RadialPatternCount + 1];
      for (var i = 0; i <= RadialPatternCount; i++)
      {
        table[i] = -1;
      }

      var pattern = Patterns[r];
      for (var i = 0; i < pattern.Length; i++)
      {
        var sq = pattern[i].LengthHorizontalSquared;
        if (sq <= RadialPatternCount && table[sq] == -1) table[sq] = i;
      }

      var lastIdx = 0;
      for (var i = 0; i <= RadialPatternCount; i++)
      {
        if (table[i] != -1) lastIdx = table[i];
        else table[i] = lastIdx;
      }
      LengthSquaredToIndexArrays[r] = table;
    }
  }

  private static bool IsInRotation(IntVec3 c, Rot8 rot)
  {
    return rot.AsInt switch
    {
      0 => c.z > 0,
      1 => c.x > 0,
      2 => c.z < 0,
      3 => c.x < 0,
      4 => c is { x: > 0, z: > 0 },
      5 => c is { x: > 0, z: < 0 },
      6 => c is { x: < 0, z: < 0 },
      7 => c is { x: < 0, z: > 0 },
      _ => false
    };
  }

  private static Rot8 Rot8ToCellRect(IntVec3 from, CellRect to)
  {
    return from switch
    {
      _ when from.x < to.minX && from.z < to.minZ => Rot8.NorthEast,
      _ when from.x < to.minX && from.z > to.maxZ => Rot8.SouthEast,
      _ when from.x > to.maxX && from.z > to.maxZ => Rot8.SouthWest,
      _ when from.x > to.maxX && from.z < to.minZ => Rot8.NorthWest,
      _ when from.z < to.minZ => Rot8.North,
      _ when from.x < to.minX => Rot8.East,
      _ when from.z > to.maxZ => Rot8.South,
      _ when from.x > to.maxX => Rot8.West,
      _ => Rot8.Invalid
    };
  }

  public static int NumCellsInRadiusToCellRect(IntVec3 from, CellRect to, float radius)
  {
    return NumCellsInRadius(radius, Rot8ToCellRect(from, to));
  }

  public static int NumCellsInRadius(float radius, Rot8 rot)
  {
    if (!rot.IsValid) return GenRadial.NumCellsInRadius(radius);
    if (radius >= GenRadial.MaxRadialPatternRadius)
    {
      Log.Error($"Not enough squares to get to radius {radius}. Max is {GenRadial.MaxRadialPatternRadius}");
      return 20000;
    }
    var num = radius + float.Epsilon;
    var num2 = (int)Math.Floor(num * num);
    const int num3 = 6400;
    if (num2 > num3)
    {
      num2 = num3;
    }

    var r = rot.AsInt;
    for (var i = LengthSquaredToIndexArrays[r][num2]; i < PatternRadii[r].Length; i++)
    {
      if (PatternRadii[r][i] > num)
      {
        return i;
      }
    }
    return 20000;
  }

  public static IntVec3[] PatternFor(IntVec3 from, CellRect to, float minRange, float maxRange, out IntRange indexRange)
  {
    var rot = Rot8ToCellRect(from, to);
    int min;
    if (!rot.IsValid)
    {
      min = minRange <= 1f ? 0 : GenRadial.NumCellsInRadius(minRange - 1f);
      indexRange = new IntRange(min, GenRadial.NumCellsInRadius(maxRange));
      return GenRadial.RadialPattern;
    }
    min = minRange <= 1f ? 0 : NumCellsInRadius(minRange - 1f, rot);
    indexRange = new IntRange(min, NumCellsInRadius(maxRange, rot));
    return Patterns[rot.AsInt];
  }
}
