namespace VehicleMapFramework;

public interface IBodyOffsetJobDriver
{
  public float PawnDrawPosOffset_Y { get; }

  float PawnBodyAngleOffset => 0f;
}