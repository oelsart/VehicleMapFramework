using UnityEngine;
using Verse.AI;

namespace VehicleMapFramework;

public abstract class JobDriverBodyOffset : JobDriver, IBodyOffsetJobDriver
{
  public Vector3 drawOffset;

  public override Vector3 ForcedBodyOffset => drawOffset;

  float IBodyOffsetJobDriver.PawnDrawPosOffset_Y => drawOffset.y;
}
