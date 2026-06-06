using UnityEngine;
using Vehicles;

namespace VehicleMapFramework;

public class CompVehicleRotationTweener : VehicleComp
{
  private float tweenedAngle;
  private float curVelocity;

  private float TargetAngle
  {
    get
    {
      if (Vehicle.vehiclePather.curPath is { NodesLeft: > 2 } path)
      {
        var nextNode = path.Peek(1);
        if ((Vehicle.Position - nextNode).LengthManhattan < Vehicle.def.Size.z)
        {
          return (path.Peek(2) - nextNode).AngleFlat;
        }
      }
      return Vehicle.FullRotation.AsAngle;
    }
  }
  
  public override void CompTick()
  {
    var targetAngle = TargetAngle;
    if (!Mathf.Approximately(tweenedAngle, targetAngle))
    {
      tweenedAngle = Mathf.SmoothDampAngle(tweenedAngle, targetAngle, ref curVelocity, 0.5f);
      Vehicle.Transform.rotation = tweenedAngle - Vehicle.FullRotation.AsAngle;
    }
  }

  public override void OnDeSpawn()
  {
    tweenedAngle = 0f;
  }
}