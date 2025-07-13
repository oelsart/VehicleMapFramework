using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace VehicleMapFramework
{
    public class SectionLayer_SubstructurePropsOnVehicle : SectionLayer_SubstructureProps
    {
        public SectionLayer_SubstructurePropsOnVehicle(Section section) : base(section)
        {
            for (var i = 0; i < 4; i++)
            {
                subMeshesByRot[i] = [];
            }
            Bottom.Material.mainTexture.wrapMode = TextureWrapMode.Clamp;
        }

        public List<LayerSubMesh>[] subMeshesByRot = new List<LayerSubMesh>[4];

        [Flags]
        private enum EdgeDirections
        {
            None = 0,
            North = 1,
            East = 2,
            South = 4,
            West = 8
        }

        [Flags]
        private enum CornerDirections
        {
            None = 0,
            SouthWest = 1,
            NorthWest = 2,
            NorthEast = 4,
            SouthEast = 8
        }

        private static readonly CachedMaterial OShape = new("Terrain/Surfaces/Substructure/SubstructureBorder_OShape", ShaderDatabase.Transparent);

        private static readonly CachedMaterial UShape = new("Terrain/Surfaces/Substructure/SubstructureBorder_UShape", ShaderDatabase.Transparent);

        private static readonly CachedMaterial CornerInner = new("Terrain/Surfaces/Substructure/SubstructureBorder_CornerInner", ShaderDatabase.Transparent);

        private static readonly CachedMaterial CornerOuter = new("Terrain/Surfaces/Substructure/SubstructureBorder_CornerOuter", ShaderDatabase.Transparent);

        private static readonly CachedMaterial Flat = new("Terrain/Surfaces/Substructure/SubstructureBorder_Flat", ShaderDatabase.Transparent);

        private static readonly CachedMaterial Bottom = new("VehicleMapFramework/Things/SubstructureProps/SubstructureProps_Bottom_NoShadow", ShaderDatabase.Transparent);

        private static readonly Vector2[] UVs =
        [
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(1f, 1f),
        new Vector2(1f, 0f)
        ];

        private static readonly Dictionary<EdgeDirections, (CachedMaterial, Rot4)[]> EdgeMats = new()
        {
            {
                EdgeDirections.North,
                new(CachedMaterial, Rot4)[1] { (Flat, Rot4.South) }
            },
            {
                EdgeDirections.East,
                new(CachedMaterial, Rot4)[1] { (Flat, Rot4.West) }
            },
            {
                EdgeDirections.South,
                new(CachedMaterial, Rot4)[1] { (Flat, Rot4.North) }
            },
            {
                EdgeDirections.West,
                new(CachedMaterial, Rot4)[1] { (Flat, Rot4.East) }
            },
            {
                EdgeDirections.North | EdgeDirections.East,
                new(CachedMaterial, Rot4)[1] { (CornerOuter, Rot4.West) }
            },
            {
                EdgeDirections.East | EdgeDirections.South,
                new(CachedMaterial, Rot4)[1] { (CornerOuter, Rot4.North) }
            },
            {
                EdgeDirections.South | EdgeDirections.West,
                new(CachedMaterial, Rot4)[1] { (CornerOuter, Rot4.East) }
            },
            {
                EdgeDirections.North | EdgeDirections.West,
                new(CachedMaterial, Rot4)[1] { (CornerOuter, Rot4.South) }
            },
            {
                EdgeDirections.North | EdgeDirections.South,
                new(CachedMaterial, Rot4)[2]
                {
                    (Flat, Rot4.South),
                    (Flat, Rot4.North)
                }
            },
            {
                EdgeDirections.East | EdgeDirections.West,
                new(CachedMaterial, Rot4)[2]
                {
                    (Flat, Rot4.West),
                    (Flat, Rot4.East)
                }
            },
            {
                EdgeDirections.North | EdgeDirections.East | EdgeDirections.South,
                new(CachedMaterial, Rot4)[1] { (UShape, Rot4.West) }
            },
            {
                EdgeDirections.East | EdgeDirections.South | EdgeDirections.West,
                new(CachedMaterial, Rot4)[1] { (UShape, Rot4.North) }
            },
            {
                EdgeDirections.North | EdgeDirections.South | EdgeDirections.West,
                new(CachedMaterial, Rot4)[1] { (UShape, Rot4.East) }
            },
            {
                EdgeDirections.North | EdgeDirections.East | EdgeDirections.West,
                new(CachedMaterial, Rot4)[1] { (UShape, Rot4.South) }
            },
            {
                EdgeDirections.North | EdgeDirections.East | EdgeDirections.South | EdgeDirections.West,
                new(CachedMaterial, Rot4)[1] { (OShape, Rot4.North) }
            }
        };

        //drawPlanetがオフでVehicleMapにフォーカスした時しか呼ばれないよ
        public override void DrawLayer()
        {
            DrawLayer(Rot8.North, Vector3.zero, 0f);
        }

        public void DrawLayer(Rot8 rot, Vector3 drawPos, float extraRotation)
        {
            var angle = Ext_Math.RotateAngle(-rot.AsRotationAngle, extraRotation);
            DrawMeshes(subMeshesByRot[rot.RotForVehicleDraw().AsInt], drawPos, angle);
        }

        public void DrawMeshes(List<LayerSubMesh> subMeshes, Vector3 drawPos, float extraRotation)
        {
            if (!Visible)
            {
                return;
            }
            int count = subMeshes.Count;
            for (int i = 0; i < count; i++)
            {
                LayerSubMesh layerSubMesh = subMeshes[i];
                if (layerSubMesh.finalized && !layerSubMesh.disabled)
                {
                    Graphics.DrawMesh(layerSubMesh.mesh, drawPos, Quaternion.AngleAxis(extraRotation, Vector3.up), layerSubMesh.material, 0);
                }
            }
        }

        public override void Regenerate()
        {
            if (!ModsConfig.OdysseyActive)
            {
                return;
            }

            for (var i = 0; i < 4; i++)
            {
                try
                {
                    subMeshes = subMeshesByRot[i];
                    ClearSubMeshes(MeshParts.All);
                    Map map = base.Map;
                    TerrainGrid terrainGrid = map.terrainGrid;
                    CellRect cellRect = section.CellRect;
                    float altitude = AltitudeLayer.TerrainScatter.AltitudeFor() / VehicleMapUtility.YCompress;
                    LayerSubMesh subMesh = GetSubMesh(Bottom.Material);
                    var south = IntVec3.South.RotatedBy(VehicleMapUtility.RotForPrintCounter);
                    foreach (IntVec3 item in cellRect)
                    {
                        if (ShouldDrawPropsOn(item, terrainGrid, out var edgeEdgeDirections, out var cornerDirections))
                        {
                            DrawEdges(item, edgeEdgeDirections, altitude);
                            DrawCorners(item, cornerDirections, edgeEdgeDirections, altitude);
                            SectionLayer_GravshipHullOnVehicle.ShouldDrawCornerPiece(item + south, map, terrainGrid, out var cornerType, out var _);
                            bool flag = cornerType == SectionLayer_GravshipHull.CornerType.Corner_NW || cornerType == SectionLayer_GravshipHull.CornerType.Diagonal_NW || cornerType == SectionLayer_GravshipHull.CornerType.Corner_NE || cornerType == SectionLayer_GravshipHull.CornerType.Diagonal_NE;
                            if (edgeEdgeDirections.HasFlag(EdgeDirections.South) && !flag)
                            {
                                AddQuad(subMesh, item + south, altitude, Rot4.North, SectionLayer_GravshipMask.IsValidSubstructure(item));
                            }
                        }
                    }
                    FinalizeMesh(MeshParts.All);
                }
                finally
                {
                    VehicleMapUtility.rotForPrint.Rotate(RotationDirection.Clockwise);
                }
            }
        }

        private void DrawEdges(IntVec3 c, EdgeDirections edgeDirs, float altitude)
        {
            if (EdgeMats.TryGetValue(edgeDirs, out var value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    var (cachedMaterial, rotation) = value[i];
                    AddQuad(GetSubMesh(cachedMaterial.Material), c, altitude, rotation, addGravshipMask: false);
                }
            }
        }

        private void DrawCorners(IntVec3 c, CornerDirections cornerDirections, EdgeDirections edgeDirs, float altitude)
        {
            if (cornerDirections.HasFlag(CornerDirections.NorthWest) && !edgeDirs.HasFlag(EdgeDirections.North) && !edgeDirs.HasFlag(EdgeDirections.West))
            {
                AddQuad(GetSubMesh(CornerInner.Material), c, altitude, Rot4.South, addGravshipMask: false);
            }
            if (cornerDirections.HasFlag(CornerDirections.NorthEast) && !edgeDirs.HasFlag(EdgeDirections.North) && !edgeDirs.HasFlag(EdgeDirections.East))
            {
                AddQuad(GetSubMesh(CornerInner.Material), c, altitude, Rot4.West, addGravshipMask: false);
            }
            if (cornerDirections.HasFlag(CornerDirections.SouthEast) && !edgeDirs.HasFlag(EdgeDirections.South) && !edgeDirs.HasFlag(EdgeDirections.East))
            {
                AddQuad(GetSubMesh(CornerInner.Material), c, altitude, Rot4.North, addGravshipMask: false);
            }
            if (cornerDirections.HasFlag(CornerDirections.SouthWest) && !edgeDirs.HasFlag(EdgeDirections.South) && !edgeDirs.HasFlag(EdgeDirections.West))
            {
                AddQuad(GetSubMesh(CornerInner.Material), c, altitude, Rot4.East, addGravshipMask: false);
            }
        }

        private void AddQuad(LayerSubMesh sm, IntVec3 c, float altitude, Rot4 rotation, bool addGravshipMask)
        {
            c = c.RotatedBy(VehicleMapUtility.rotForPrint);
            var offset = -UVs[VehicleMapUtility.rotForPrint.AsInt];

            int count = sm.verts.Count;
            int num = Mathf.Abs(4 - rotation.AsInt);
            for (int i = 0; i < 4; i++)
            {
                sm.verts.Add(new Vector3(c.x + UVs[i].x + offset.x, altitude, c.z + UVs[i].y + offset.y));
                sm.uvs.Add(UVs[(num + i) % 4]);
            }
            sm.tris.Add(count);
            sm.tris.Add(count + 1);
            sm.tris.Add(count + 2);
            sm.tris.Add(count);
            sm.tris.Add(count + 2);
            sm.tris.Add(count + 3);

            if (addGravshipMask)
            {
                Material material = MaterialPool.MatFrom(sm.material.mainTexture as Texture2D, color: sm.material.color, shader: ShaderDatabase.GravshipMaskMasked);
                AddQuad(GetSubMesh(material), c, altitude, rotation, addGravshipMask: false);
            }
        }

        private bool ShouldDrawPropsOn(IntVec3 c, TerrainGrid terrGrid, out EdgeDirections edgeEdgeDirections, out CornerDirections cornerDirections)
        {
            edgeEdgeDirections = EdgeDirections.None;
            cornerDirections = CornerDirections.None;
            TerrainDef terrainDef = terrGrid.FoundationAt(c);
            if (terrainDef == null || !terrainDef.IsSubstructure)
            {
                return false;
            }
            for (int i = 0; i < GenAdj.CardinalDirections.Length; i++)
            {
                IntVec3 c2 = c + GenAdj.CardinalDirections[GenMath.PositiveMod(i - VehicleMapUtility.rotForPrint.AsInt, 4)];
                if (!c2.InBounds(base.Map))
                {
                    edgeEdgeDirections |= (EdgeDirections)(1 << i);
                    continue;
                }
                TerrainDef terrainDef2 = terrGrid.FoundationAt(c2);
                if (terrainDef2 == null || !terrainDef2.IsSubstructure)
                {
                    edgeEdgeDirections |= (EdgeDirections)(1 << i);
                }
            }
            for (int j = 0; j < GenAdj.DiagonalDirections.Length; j++)
            {
                IntVec3 c3 = c + GenAdj.DiagonalDirections[GenMath.PositiveMod(j - VehicleMapUtility.rotForPrint.AsInt, 4)];
                if (!c3.InBounds(base.Map))
                {
                    cornerDirections |= (CornerDirections)(1 << j);
                    continue;
                }
                TerrainDef terrainDef3 = terrGrid.FoundationAt(c3);
                if (terrainDef3 == null || !terrainDef3.IsSubstructure)
                {
                    cornerDirections |= (CornerDirections)(1 << j);
                }
            }
            if (edgeEdgeDirections == EdgeDirections.None)
            {
                return cornerDirections != CornerDirections.None;
            }
            return true;
        }
    }
}
