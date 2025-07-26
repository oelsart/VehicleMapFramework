using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using SmashTools;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Vehicles;
using Vehicles.World;
using Verse;

namespace VehicleMapFramework
{
    [StaticConstructorOnStartup]
    public class VTOLTakeoff_Gravship : VTOLTakeoff
    {
        private MaterialPropertyBlock thrusterFlameBlock = new();

        private MaterialPropertyBlock engineGlowBlock = new();

        private static readonly Material MatGravEngineGlow = MatLoader.LoadMat("Map/Gravship/GravEngineGlow", -1);

        private static readonly int ShaderPropertyColor2 = Shader.PropertyToID("_Color2");

        public VTOLTakeoff_Gravship()
        {
        }

        public VTOLTakeoff_Gravship(VTOLTakeoff_Gravship reference, VehiclePawn vehicle) : base(reference, vehicle)
        {
        }

        public VerticalProtocolProperties_Gravship LaunchProperties_Gravship => LaunchProperties as VerticalProtocolProperties_Gravship;

        public VerticalProtocolProperties_Gravship LandingProperties_Gravship => LandingProperties as VerticalProtocolProperties_Gravship;

        protected override (Vector3 drawPos, float rotation, DynamicShadowData shadowData) AnimateTakeoff(Vector3 drawPos, float rotation, DynamicShadowData shadowData)
        {
            var curPos = drawPos;
            (drawPos, rotation, shadowData) = base.AnimateTakeoff(drawPos, rotation, shadowData);

            if (TicksPassed > 0)
            {
                var offset = Vector3.zero;
                if (!LaunchProperties_VTOL.zPositionVerticalCurve.NullOrEmpty())
                {
                    offset.z += LaunchProperties_VTOL.zPositionVerticalCurve.Evaluate(TimeInAnimationVTOL);
                }
                if (!LaunchProperties_VTOL.xPositionVerticalCurve.NullOrEmpty())
                {
                    offset.x += LaunchProperties_VTOL.xPositionVerticalCurve.Evaluate(TimeInAnimationVTOL);
                }
                if (!LaunchProperties_VTOL.offsetVerticalCurve.NullOrEmpty())
                {
                    Vector2 vector = LaunchProperties_VTOL.offsetVerticalCurve.EvaluateT(TimeInAnimationVTOL);
                    offset += new Vector3(vector.x, 0f, vector.y);
                }
                var offset2 = drawPos - curPos;
                drawPos -= offset2;
                drawPos += (offset2 - offset).RotatedBy(Vehicle?.Rotation.AsAngle ?? 0f) + offset;
            }

            if (ModsConfig.OdysseyActive && LaunchProperties_Gravship != null && Vehicle is VehiclePawnWithMap vehicle)
            {
                if (GravshipUtility.GetPlayerGravEngine(vehicle.VehicleMap) is Building_GravEngine engine)
                {
                    Color color = Color.white.WithAlpha(0);
                    if (!LaunchProperties_Gravship.thrusterFlameCurve.NullOrEmpty())
                    {
                        color.a += LaunchProperties_Gravship.thrusterFlameCurve.Evaluate(TimeInAnimation);
                    }
                    if (!LaunchProperties_Gravship.thrusterFlameVerticalCurve.NullOrEmpty())
                    {
                        color.a += LaunchProperties_Gravship.thrusterFlameVerticalCurve.Evaluate(TimeInAnimationVTOL);
                    }
                    thrusterFlameBlock.Clear();
                    thrusterFlameBlock.SetColor(ShaderPropertyColor2, color);
                    engine?.GravshipComponents.OfType<CompGravshipThruster>()
                        .Where(t => t.CanBeActive)
                        .Where(t => (t.Props.exhaustSettings?.enabled ?? false) && t.Props.exhaustSettings.ExhaustFleckDef != null)
                        .Do(t =>
                        {
                            var props = t.Props;
                            var thing = t.parent;
                            var num = thing.def.size.x * props.flameSize;
                            var rot = thing.BaseRotation();
                            var vector = rot.AsQuat * props.flameOffsetsPerDirection[rot.AsInt];
                            var vector2 = thing.DrawPos - (rot.AsIntVec3.ToVector3() * num * 0.5f + vector).RotatedBy(rotation);
                            Material material = MaterialPool.MatFrom(new MaterialRequest(props.FlameShaderType.Shader)
                            {
                                renderQueue = 3201
                            });
                            foreach (ShaderParameter shaderParameter in props.flameShaderParameters)
                            {
                                shaderParameter.Apply(thrusterFlameBlock);
                            }
                            GenDraw.DrawQuad(material, vector2.SetToAltitude(AltitudeLayer.Skyfaller).YOffsetFull(vehicle), Quaternion.AngleAxis(rot.AsAngle + rotation, Vector3.up), num, thrusterFlameBlock);
                        });

                    Color color2 = Color.white.WithAlpha(0);
                    color2 *= Mathf.Lerp(0.75f, 1f, Mathf.PerlinNoise1D((ticksPassed + ticksPassedVertical) / TotalTicks_Takeoff) * 100f);
                    if (!LaunchProperties_Gravship.engineGlowVerticalCurve.NullOrEmpty())
                    {
                        color2.a += LaunchProperties_Gravship.engineGlowVerticalCurve.Evaluate(TimeInAnimationVTOL);
                    }
                    if (!LaunchProperties_Gravship.engineGlowCurve.NullOrEmpty())
                    {
                        color2.a += LaunchProperties_Gravship.engineGlowCurve.Evaluate(TimeInAnimation);
                    }
                    engineGlowBlock.SetColor(ShaderPropertyColor2, color2);
                    GenDraw.DrawQuad(MatGravEngineGlow, engine.DrawPos.SetToAltitude(AltitudeLayer.MetaOverlays), Quaternion.identity, 12.5f, engineGlowBlock);
                }
            }
            return (drawPos, rotation, shadowData);
        }

        protected override (Vector3 drawPos, float rotation, DynamicShadowData shadowData) AnimateLanding(Vector3 drawPos, float rotation, DynamicShadowData shadowData)
        {
            var curPos = drawPos;
            (drawPos, rotation, shadowData) = base.AnimateLanding(drawPos, rotation, shadowData);

            if (TicksPassed <= 0)
            {
                var offset = Vector3.zero;
                if (!LandingProperties_VTOL.zPositionVerticalCurve.NullOrEmpty())
                {
                    offset.z += LandingProperties_VTOL.zPositionVerticalCurve.Evaluate(TimeInAnimationVTOL);
                }
                if (!LandingProperties_VTOL.xPositionVerticalCurve.NullOrEmpty())
                {
                    offset.x += LandingProperties_VTOL.xPositionVerticalCurve.Evaluate(TimeInAnimationVTOL);
                }
                if (!LandingProperties_VTOL.offsetVerticalCurve.NullOrEmpty())
                {
                    Vector2 vector = LandingProperties_VTOL.offsetVerticalCurve.EvaluateT(TimeInAnimationVTOL);
                    offset += new Vector3(vector.x, 0f, vector.y);
                }
                var offset2 = drawPos - curPos;
                drawPos -= offset2;
                drawPos += (offset2 - offset).RotatedBy(Vehicle?.Rotation.AsAngle ?? 0f) + offset;
            }

            if (ModsConfig.OdysseyActive && LandingProperties_Gravship != null && Vehicle is VehiclePawnWithMap vehicle)
            {
                if (GravshipUtility.GetPlayerGravEngine(vehicle.VehicleMap) is Building_GravEngine engine)
                {
                    Color color = Color.white.WithAlpha(0);
                    if (!LandingProperties_Gravship.thrusterFlameCurve.NullOrEmpty())
                    {
                        color.a += LandingProperties_Gravship.thrusterFlameCurve.Evaluate(TimeInAnimation);
                    }
                    if (!LandingProperties_Gravship.thrusterFlameVerticalCurve.NullOrEmpty())
                    {
                        color.a += LandingProperties_Gravship.thrusterFlameVerticalCurve.Evaluate(TimeInAnimationVTOL);
                    }
                    thrusterFlameBlock.Clear();
                    thrusterFlameBlock.SetColor(ShaderPropertyColor2, color);
                    engine?.GravshipComponents.OfType<CompGravshipThruster>()
                        .Where(t => t.CanBeActive)
                        .Where(t => (t.Props.exhaustSettings?.enabled ?? false) && t.Props.exhaustSettings.ExhaustFleckDef != null)
                        .Do(t =>
                        {
                            var props = t.Props;
                            var thing = t.parent;
                            var num = thing.def.size.x * props.flameSize;
                            var rot = thing.BaseRotation();
                            var vector = rot.AsQuat * props.flameOffsetsPerDirection[rot.AsInt];
                            var vector2 = thing.DrawPos - (rot.AsIntVec3.ToVector3() * num * 0.5f + vector).RotatedBy(rotation);
                            Material material = MaterialPool.MatFrom(new MaterialRequest(props.FlameShaderType.Shader)
                            {
                                renderQueue = 3201
                            });
                            foreach (ShaderParameter shaderParameter in props.flameShaderParameters)
                            {
                                shaderParameter.Apply(thrusterFlameBlock);
                            }
                            GenDraw.DrawQuad(material, vector2.SetToAltitude(AltitudeLayer.Skyfaller).YOffsetFull(vehicle), Quaternion.AngleAxis(rot.AsAngle + rotation, Vector3.up), num, thrusterFlameBlock);
                        });

                    Color color2 = Color.white.WithAlpha(0);
                    color2 *= Mathf.Lerp(0.75f, 1f, Mathf.PerlinNoise1D((ticksPassed + ticksPassedVertical) / TotalTicks_Takeoff) * 100f);
                    if (!LandingProperties_Gravship.engineGlowVerticalCurve.NullOrEmpty())
                    {
                        color2.a += LandingProperties_Gravship.engineGlowVerticalCurve.Evaluate(TimeInAnimationVTOL);
                    }
                    if (!LandingProperties_Gravship.engineGlowCurve.NullOrEmpty())
                    {
                        color2.a += LandingProperties_Gravship.engineGlowCurve.Evaluate(TimeInAnimation);
                    }
                    engineGlowBlock.SetColor(ShaderPropertyColor2, color2);
                    GenDraw.DrawQuad(MatGravEngineGlow, engine.DrawPos.SetToAltitude(AltitudeLayer.MetaOverlays), Quaternion.identity, 12.5f, engineGlowBlock);
                }
            }

            return (drawPos, rotation, shadowData);
        }

        public override void OrderProtocol(LaunchType launchType)
        {
            base.OrderProtocol(launchType);
            if (!ModsConfig.OdysseyActive || Vehicle is not VehiclePawnWithMap vehicle) return;

            if (launchType == LaunchType.Takeoff && GravshipUtility.GetPlayerGravEngine(vehicle.VehicleMap) is Building_GravEngine engine)
            {
                if (launchType == LaunchType.Takeoff)
                {
                    engine.cooldownCompleteTick = GenTicks.TicksGame + (int)GravshipUtility.LaunchCooldownFromQuality(engine.launchInfo?.quality ?? 1f);
                }
            }
        }

        public override IEnumerable<ArrivalOption> GetArrivalOptions(GlobalTargetInfo target)
        {
            foreach (var option in base.GetArrivalOptions(target))
            {
                yield return option;
            }
            if (target.WorldObject is MapParent mapParent && mapParent.Spawned && !mapParent.HasMap && !mapParent.EnterCooldownBlocksEntering())
            {
                yield return new ArrivalOption("VF_LandVehicleTargetedLanding".Translate(mapParent.Label), new ArrivalAction_LoadMap(vehicle, AerialVehicleArrivalModeDefOf.TargetedLanding));
            }
        }
    }
}