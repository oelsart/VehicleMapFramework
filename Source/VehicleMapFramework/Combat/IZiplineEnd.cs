using UnityEngine;

namespace VehicleMapFramework;

public interface IZiplineEnd
{
  CustomZipline.ZipLineData ZipLineData { get; set; }

  void DrawZipline(Vector3 drawLoc);
}
