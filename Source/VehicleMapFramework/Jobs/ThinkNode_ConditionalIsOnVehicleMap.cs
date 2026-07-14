using Verse;
using Verse.AI;

namespace VehicleMapFramework;

public class ThinkNode_ConditionalIsOnVehicleMap : ThinkNode_Conditional
{
  protected override bool Satisfied(Pawn pawn) => pawn.IsOnVehicleMap;
}