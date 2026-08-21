using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class VehicleFormationComp : WorldObjectComp
{
  public Dictionary<VehiclePawn, DrawData> DrawPositions => drawPositions;
  
  private Dictionary<VehiclePawn, DrawData> drawPositions = [];
  private List<VehiclePawn> keysWorkingList;
  private List<DrawData> valuesWorkingList;

  public override void Initialize(WorldObjectCompProperties _props)
  {
    base.Initialize(_props);
    FrameDelay.DelayOne(state =>
    {
      state.RecalculateVehiclePositions();
    }, this);
  }

  public void RecalculateVehiclePositions()
  {
    drawPositions.Clear();
    
    foreach (var vehicle in parent.Vehicles)
    {
      if (vehicle is VehiclePawnWithMap { VehicleDef.IsUniqueVehicle: true } vehicle2)
        vehicle2.ResizeNow();
      
      FindVehiclePosition(vehicle);
    }

    CenteredDrawPositions();
  }

  public void FindVehiclePosition(VehiclePawn vehicle)
  {
    if (drawPositions.ContainsKey(vehicle))
      return;
    
    var cellRect = CellRect.CenteredOn(IntVec3.Zero, vehicle.VehicleDef.Size);
    var radialCount = GenRadial.NumCellsInRadius(CombatExtended ? 119f : GenRadial.MaxRadialPatternRadius - 0.1f);
    for (var i = 0; i < radialCount; i++)
    {
      var cellRect2 = cellRect.MovedBy(GenRadial.RadialPattern[i]);
      var flag = true;
      foreach (var pair in drawPositions)
      {
        if (cellRect2.Overlaps(pair.Value.cellRect.ExpandedBy(1)))
        {
          flag = false;
          break;
        }
      }

      if (flag)
      {
        drawPositions[vehicle] = new DrawData(cellRect2, cellRect2.CenterVector3.SetToAltitude(AltitudeLayer.LayingPawn));
        return;
      }
    }
    
    VMF_Log.Error($"Could not find draw position for {vehicle.Name}.");
    drawPositions[vehicle] = new DrawData(CellRect.Empty, Vector3.zero.WithY(AltitudeLayer.LayingPawn.AltitudeFor()));
  }

  public void CenteredDrawPositions()
  {
    if (drawPositions.NullOrEmpty()) return;
    var values = drawPositions.Values;
    var average = new Vector3(
      values.Average(p => p.cellRect.CenterVector3.x),
      0,
      values.Average(p => p.cellRect.CenterVector3.z));
    foreach (var vehicle in drawPositions.Keys.ToArray())
    {
      var tuple = drawPositions[vehicle];
      drawPositions[vehicle] = tuple with
      {
        position = tuple.cellRect.CenterVector3.SetToAltitude(AltitudeLayer.LayingPawn) - average
      };
    }
    foreach (var vehicle in parent.Vehicles.OfType<VehiclePawnWithMap>())
      vehicle.RecacheDrawPos(drawPositions[vehicle].position);
  }
  
  public override void PostExposeData()
  {
    Scribe_Collections.Look(ref drawPositions, nameof(drawPositions),
      LookMode.Reference, LookMode.Deep, ref keysWorkingList, ref valuesWorkingList, false);
    if (Scribe.mode is LoadSaveMode.PostLoadInit)
    {
      drawPositions ??= [];
      if (drawPositions.Count == 0)
        RecalculateVehiclePositions();
    }
  }

  public struct DrawData(CellRect cellRect, Vector3 position) : IExposable
  {
    public CellRect cellRect = cellRect;
    public Vector3 position = position;
    
    void IExposable.ExposeData()
    {
      Scribe_Values.Look(ref cellRect, nameof(cellRect));
      Scribe_Values.Look(ref position, nameof(position));
    }
  }
}