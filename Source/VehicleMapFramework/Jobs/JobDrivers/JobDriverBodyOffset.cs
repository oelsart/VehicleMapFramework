using UnityEngine;
using Verse.AI;

namespace VehicleMapFramework;

public abstract class JobDriverBodyOffset : JobDriver
{
    public Vector3 drawOffset;
    
    public override Vector3 ForcedBodyOffset => drawOffset;
}