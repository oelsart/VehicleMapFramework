using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[UsedImplicitly(ImplicitUseTargetFlags.Members)]
[StaticConstructorOnStartup]
public class CustomZipline : DefModExtension
{

  private static Material DefaultZiplineMat;

  public float? launcherOffset;
  public string texPath;

  [Unsaved] public ZipLineData zipLineData;

  public ThingDef ziplineEndDef;

  public float? ziplineEndOffset;

  public ThingDef ziplineReturnDef;

  public float? ziplineWidth;

  static CustomZipline()
  {
    LongEventHandler.ExecuteWhenFinished(() => { DefaultZiplineMat = MaterialPool.MatFrom("VehicleMapFramework/Things/ZiplineTurret/Zipline"); });
  }

  public override void ResolveReferences(Def parentDef)
  {
    base.ResolveReferences(parentDef);
    LongEventHandler.ExecuteWhenFinished(() => { zipLineData = new ZipLineData(texPath, ziplineWidth, ziplineEndOffset, launcherOffset, ziplineEndDef, ziplineReturnDef); });
  }

  public override IEnumerable<string> ConfigErrors()
  {
    if (ziplineEndDef?.thingClass.SameOrSubclassOf<ZiplineEnd>() == false)
    {
      yield return "ZiplineEndDef must be a subclass of ZiplineEnd";
    }
    if (ziplineReturnDef?.thingClass.SameOrSubclassOf<Bullet_ZiplineEndReturn>() == false)
    {
      yield return "ZiplineReturnDef must be a subclass of Bullet_ZiplineEndReturn";
    }
  }

  public readonly struct ZipLineData
  {
    private readonly float? ziplineWidth;

    private readonly float? ziplineEndOffset;

    private readonly float? launcherOffset;

    private readonly Material ziplineMat;

    public ZipLineData(string texPath, float? ziplineWidth, float? ziplineEndOffset, float? launcherOffset,
      ThingDef ziplineEndDef, ThingDef ziplineReturnDef)
    {
      this.ziplineWidth = ziplineWidth;
      this.ziplineEndOffset = ziplineEndOffset;
      this.launcherOffset = launcherOffset;
      ZiplineEndDef = ziplineEndDef;
      ZiplineReturnDef = ziplineReturnDef;
      if (texPath != null)
      {
        ziplineMat = MaterialPool.MatFrom(texPath, ShaderDatabase.Transparent);
        ziplineMat.mainTexture.wrapMode = TextureWrapMode.Repeat;
        ziplineMat.enableInstancing = true;
      }
    }

    public Material ZiplineMat => ziplineMat ?? DefaultZiplineMat;

    public float ZiplineWidth => ziplineWidth ?? DefaultZiplineWidth;

    public float ZiplineEndOffset => ziplineEndOffset ?? DefaultZiplineEndOffset;

    public float LauncherOffset => launcherOffset ?? DefaultLauncherOffset;

    public ThingDef ZiplineEndDef => field ?? VMF_DefOf.VMF_ZiplineEnd;

    public ThingDef ZiplineReturnDef => field ?? VMF_DefOf.VMF_Bullet_ZiplineTurretReturn;

    private const float DefaultZiplineWidth = 0.12f;

    private const float DefaultZiplineEndOffset = 0.42f;

    private const float DefaultLauncherOffset = 0.85f;
  }
}
