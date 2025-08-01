using LudeonTK;
using RimWorld;
using SmashTools;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace VehicleMapFramework
{
    public class SectionLayer_GravshipHullOnVehicle : SectionLayer_GravshipHull
    {
        public SectionLayer_GravshipHullOnVehicle(Section section) : base(section)
        {
            for (var i = 0; i < 4; i++)
            {
                subMeshesByRot[i] = [];
            }
        }

        public List<LayerSubMesh>[] subMeshesByRot = new List<LayerSubMesh>[4];

        private static readonly Vector2[] UVs =
        [
        new(0f, 0f),
        new(0f, 1f),
        new(1f, 1f),
        new(1f, 0f)
        ];

        [TweakValue("HullCorners", 0f, 2f)]
        private static float HullCornerScale = 2f;

        private const string TexPath_Corner_NW = "Things/Building/Linked/GravshipHull/AngledGravshipHull_northwest";

        private const string TexPath_Corner_NE = "Things/Building/Linked/GravshipHull/AngledGravshipHull_northeast";

        private const string TexPath_Corner_SW = "Things/Building/Linked/GravshipHull/AngledGravshipHull_southwest";

        private const string TexPath_Corner_SE = "Things/Building/Linked/GravshipHull/AngledGravshipHull_southeast";

        private const string TexPath_Diagonal_NW = "Things/Building/Linked/GravshipHull/AngledGravshipHull_Partial_northwest";

        private const string TexPath_Diagonal_NE = "Things/Building/Linked/GravshipHull/AngledGravshipHull_Partial_northeast";

        private const string TexPath_Diagonal_SW = "Things/Building/Linked/GravshipHull/AngledGravshipHull_Partial_southwest";

        private const string TexPath_Diagonal_SE = "Things/Building/Linked/GravshipHull/AngledGravshipHull_Partial_southeast";

        private const string TexPath_SubStructure_W = "VehicleMapFramework/Things/SubstructureProps/SubstructureCorner_Full_west_NoShadow";

        private const string TexPath_SubStructure_E = "VehicleMapFramework/Things/SubstructureProps/SubstructureCorner_Full_east_NoShadow";

        private const string TexPath_SubStructureExtra_W = "VehicleMapFramework/Things/SubstructureProps/SubstructureCorner_Tip_west_NoShadow";

        private const string TexPath_SubStructureExtra_E = "VehicleMapFramework/Things/SubstructureProps/SubstructureCorner_Tip_east_NoShadow";

        private const int BakedIndoorMaskRenderQueue = 3185;

        private static CachedMaterial mat_Corner_NW;

        private static CachedMaterial mat_Corner_NE;

        private static CachedMaterial mat_Corner_SW;

        private static CachedMaterial mat_Corner_SE;

        private static CachedMaterial mat_Diagonal_NW;

        private static CachedMaterial mat_Diagonal_NE;

        private static CachedMaterial mat_Diagonal_SW;

        private static CachedMaterial mat_Diagonal_SE;

        private static CachedMaterial mat_SubStructure_W;

        private static CachedMaterial mat_SubStructure_E;

        private static CachedMaterial mat_SubStructureExtra_W;

        private static CachedMaterial mat_SubStructureExtra_E;

        private static readonly float cornerAltitude = AltitudeLayer.BuildingOnTop.AltitudeFor() / VehicleMapUtility.YCompress;

        private static readonly float substructureAltitude = AltitudeLayer.TerrainEdges.AltitudeFor() / VehicleMapUtility.YCompress;

        private static readonly float bakedAltitude = AltitudeLayer.MetaOverlays.AltitudeFor() / VehicleMapUtility.YCompress;

        private static bool initalized;

        private static readonly IntVec3[] Directions =
        [
        IntVec3.North,
        IntVec3.East,
        IntVec3.South,
        IntVec3.West,
        IntVec3.North + IntVec3.West,
        IntVec3.North + IntVec3.East,
        IntVec3.South + IntVec3.East,
        IntVec3.South + IntVec3.West
        ];

        private static readonly int[][] directionPairs =
        [
        [0, 2],
        [1, 3],
        [4, 6],
        [5, 7]
        ];

        private static bool[] tmpChecks = new bool[Directions.Length];

        private static Shader WallShader => ShaderDatabase.CutoutOverlay;

        private static Shader SubstructureShader => ShaderDatabase.Transparent;

        public override bool Visible => ModsConfig.OdysseyActive;

        public override CellRect GetBoundaryRect()
        {
            var rect = base.GetBoundaryRect();
            if (section.map.IsVehicleMapOf(out var vehicle))
            {
                var longside = Mathf.Max(vehicle.def.size.x, vehicle.def.size.z);
                rect = rect.ExpandedBy(longside);
            }
            return rect;
        }

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
                    Graphics.DrawMesh(layerSubMesh.mesh, drawPos, Quaternion.AngleAxis(extraRotation, Vector3.up), layerSubMesh.material, layerSubMesh.renderLayer);
                }
            }
        }

        //new public static List<LayerSubMesh> BakeGravshipIndoorMesh(Map map, CellRect bounds, Vector3 center)
        //{
        //    Dictionary<CornerType, LayerSubMesh> dictionary = [];
        //    TerrainGrid terrainGrid = map.terrainGrid;
        //    foreach (IntVec3 item in bounds)
        //    {
        //        if (ShouldDrawCornerPiece(item, map, terrainGrid, out var cornerType, out var color) && IsCornerSubstructure(item, cornerType) && IsCornerIndoorMasked(item, cornerType, map))
        //        {
        //            Material material = GetMaterial(cornerType).Material;
        //            Texture2D srcTex = material.mainTexture as Texture2D;
        //            Color color2 = material.color;
        //            Material material2 = MaterialPool.MatFrom(srcTex, ShaderDatabase.IndoorMaskMasked, color2, 3185);
        //            IntVec3 offset = GetOffset(cornerType).RotatedBy(VehicleMapUtility.rotForPrint.IsHorizontal ? VehicleMapUtility.rotForPrint.Opposite : VehicleMapUtility.rotForPrint);
        //            if (!dictionary.TryGetValue(cornerType, out var value))
        //            {
        //                dictionary.Add(cornerType, value = MapDrawLayer.CreateFreeSubMesh(material2, map));
        //            }
        //            AddQuad(value, (item + offset).ToVector3() - center, HullCornerScale, bakedAltitude, color);
        //        }
        //    }
        //    foreach (LayerSubMesh value2 in dictionary.Values)
        //    {
        //        value2.FinalizeMesh(MeshParts.All);
        //    }
        //    return [.. dictionary.Values];
        //}

        public override void Regenerate()
        {
            if (!ModsConfig.OdysseyActive)
            {
                return;
            }

            VehicleMapUtility.RotForPrint = Rot4.North;
            for (var i = 0; i < 4; i++)
            {
                try
                {
                    subMeshes = subMeshesByRot[i];
                    ClearSubMeshes(MeshParts.All);
                    Map map = Map;
                    TerrainGrid terrainGrid = map.terrainGrid;
                    var south = IntVec3.South.RotatedBy(VehicleMapUtility.RotForPrintCounter);
                    foreach (IntVec3 item in section.CellRect)
                    {
                        if (ShouldDrawCornerPiece(item, map, terrainGrid, out var cornerType, out var color))
                        {
                            CachedMaterial material = GetMaterial(cornerType);
                            IntVec3 offset = GetOffset(cornerType).RotatedBy(VehicleMapUtility.RotForPrintCounter);
                            bool addGravshipMask = IsCornerSubstructure(item, cornerType);
                            bool addIndoorMask = IsCornerIndoorMasked(item, cornerType, map);
                            AddQuad(material.Material, item + offset, HullCornerScale, cornerAltitude, color, addGravshipMask, addIndoorMask);
                            bool substructureToSouth = terrainGrid.FoundationAt(item + south)?.IsSubstructure ?? false;
                            AddSubstructure(cornerType, item, substructureToSouth, addGravshipMask, addIndoorMask);
                        }
                    }
                    FinalizeMesh(MeshParts.All);
                }
                finally
                {
                    VehicleMapUtility.RotForPrint = VehicleMapUtility.RotForPrint.Rotated(RotationDirection.Clockwise);
                }
            }
            VehicleMapUtility.RotForPrint = Rot4.North;
        }

        private static void EnsureInitialized()
        {
            if (!initalized)
            {
                initalized = true;
                mat_Corner_NW = new CachedMaterial(TexPath_Corner_NW, WallShader);
                mat_Corner_NE = new CachedMaterial(TexPath_Corner_NE, WallShader);
                mat_Corner_SW = new CachedMaterial(TexPath_Corner_SW, WallShader);
                mat_Corner_SE = new CachedMaterial(TexPath_Corner_SE, WallShader);
                mat_Diagonal_NW = new CachedMaterial(TexPath_Diagonal_NW, WallShader);
                mat_Diagonal_NE = new CachedMaterial(TexPath_Diagonal_NE, WallShader);
                mat_Diagonal_SW = new CachedMaterial(TexPath_Diagonal_SW, WallShader);
                mat_Diagonal_SE = new CachedMaterial(TexPath_Diagonal_SE, WallShader);
                mat_SubStructure_W = new CachedMaterial(TexPath_SubStructure_W, SubstructureShader);
                mat_SubStructure_E = new CachedMaterial(TexPath_SubStructure_E, SubstructureShader);
                mat_SubStructureExtra_W = new CachedMaterial(TexPath_SubStructureExtra_W, SubstructureShader);
                mat_SubStructureExtra_E = new CachedMaterial(TexPath_SubStructureExtra_E, SubstructureShader);

                mat_SubStructure_W.Material.mainTexture.wrapMode = TextureWrapMode.Clamp;
                mat_SubStructure_E.Material.mainTexture.wrapMode = TextureWrapMode.Clamp;
                mat_SubStructureExtra_W.Material.mainTexture.wrapMode = TextureWrapMode.Clamp;
                mat_SubStructureExtra_E.Material.mainTexture.wrapMode = TextureWrapMode.Clamp;
            }
        }

        private static bool IsIndoorMasked(IntVec3 c, Map map)
        {
            return c.Roofed(map);
        }

        private static bool IsCornerSubstructure(IntVec3 c, CornerType cornerType)
        {
            switch (cornerType)
            {
                case CornerType.Corner_NE:
                case CornerType.Diagonal_NE:
                    if (!SectionLayer_GravshipMask.IsValidSubstructure(c + IntVec3.North))
                    {
                        return SectionLayer_GravshipMask.IsValidSubstructure(c + IntVec3.East);
                    }
                    return true;
                case CornerType.Corner_NW:
                case CornerType.Diagonal_NW:
                    if (!SectionLayer_GravshipMask.IsValidSubstructure(c + IntVec3.North))
                    {
                        return SectionLayer_GravshipMask.IsValidSubstructure(c + IntVec3.West);
                    }
                    return true;
                case CornerType.Corner_SE:
                case CornerType.Diagonal_SE:
                    if (!SectionLayer_GravshipMask.IsValidSubstructure(c + IntVec3.South))
                    {
                        return SectionLayer_GravshipMask.IsValidSubstructure(c + IntVec3.East);
                    }
                    return true;
                case CornerType.Corner_SW:
                case CornerType.Diagonal_SW:
                    if (!SectionLayer_GravshipMask.IsValidSubstructure(c + IntVec3.South))
                    {
                        return SectionLayer_GravshipMask.IsValidSubstructure(c + IntVec3.West);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsCornerIndoorMasked(IntVec3 c, CornerType cornerType, Map map)
        {
            switch (cornerType)
            {
                case CornerType.Corner_NE:
                case CornerType.Diagonal_NE:
                    if (!IsIndoorMasked(c + IntVec3.North, map))
                    {
                        return IsIndoorMasked(c + IntVec3.East, map);
                    }
                    return true;
                case CornerType.Corner_NW:
                case CornerType.Diagonal_NW:
                    if (!IsIndoorMasked(c + IntVec3.North, map))
                    {
                        return IsIndoorMasked(c + IntVec3.West, map);
                    }
                    return true;
                case CornerType.Corner_SE:
                case CornerType.Diagonal_SE:
                    if (!IsIndoorMasked(c + IntVec3.South, map))
                    {
                        return IsIndoorMasked(c + IntVec3.East, map);
                    }
                    return true;
                case CornerType.Corner_SW:
                case CornerType.Diagonal_SW:
                    if (!IsIndoorMasked(c + IntVec3.South, map))
                    {
                        return IsIndoorMasked(c + IntVec3.West, map);
                    }
                    return true;
                default:
                    return false;
            }
        }

        private static CachedMaterial GetMaterial(CornerType edgeType)
        {
            EnsureInitialized();
            return edgeType switch
            {
                CornerType.Corner_NW => mat_Corner_NW,
                CornerType.Corner_NE => mat_Corner_NE,
                CornerType.Corner_SW => mat_Corner_SW,
                CornerType.Corner_SE => mat_Corner_SE,
                CornerType.Diagonal_NW => mat_Diagonal_NW,
                CornerType.Diagonal_NE => mat_Diagonal_NE,
                CornerType.Diagonal_SW => mat_Diagonal_SW,
                CornerType.Diagonal_SE => mat_Diagonal_SE,
                _ => throw new ArgumentOutOfRangeException("edgeType", edgeType, null),
            };
        }

        private static IntVec3 GetOffset(CornerType cornerType)
        {
            switch (cornerType)
            {
                case CornerType.Corner_NE:
                case CornerType.Diagonal_NE:
                    return new IntVec3(0, 0, 0);
                case CornerType.Corner_NW:
                case CornerType.Diagonal_NW:
                    return new IntVec3(-1, 0, 0);
                case CornerType.Corner_SE:
                case CornerType.Diagonal_SE:
                    return new IntVec3(0, 0, -1);
                case CornerType.Corner_SW:
                case CornerType.Diagonal_SW:
                    return new IntVec3(-1, 0, -1);
                default:
                    return IntVec3.Zero;
            }
        }

        private static void AddQuad(LayerSubMesh sm, Vector3 c, float scale, float altitude, Color color)
        {
            c = c.RotatedBy(VehicleMapUtility.RotForPrint);
            var offset = -UVs[VehicleMapUtility.RotForPrint.AsInt];
            int count = sm.verts.Count;
            for (int i = 0; i < 4; i++)
            {
                sm.verts.Add(new Vector3(c.x + UVs[i].x * scale + offset.x, altitude, c.z + UVs[i].y * scale + offset.y));
                sm.uvs.Add(UVs[i % 4]);
                sm.colors.Add(color);
            }
            sm.tris.Add(count);
            sm.tris.Add(count + 1);
            sm.tris.Add(count + 2);
            sm.tris.Add(count);
            sm.tris.Add(count + 2);
            sm.tris.Add(count + 3);
        }

        private void AddQuad(Material mat, IntVec3 c, float scale, float altitude, Color color, bool addGravshipMask, bool addIndoorMask)
        {
            LayerSubMesh subMesh = GetSubMesh(mat);
            AddQuad(subMesh, c.ToVector3(), scale, altitude, color);
            if (addGravshipMask)
            {
                Texture2D srcTex = subMesh.material.mainTexture as Texture2D;
                Color color2 = subMesh.material.color;
                Material material = MaterialPool.MatFrom(srcTex, ShaderDatabase.GravshipMaskMasked, color2);
                AddQuad(GetSubMesh(material), c.ToVector3(), scale, altitude, color);
            }
            if (addIndoorMask)
            {
                Texture2D srcTex2 = subMesh.material.mainTexture as Texture2D;
                Color color3 = subMesh.material.color;
                Material material2 = MaterialPool.MatFrom(srcTex2, ShaderDatabase.IndoorMaskMasked, color3);
                AddQuad(GetSubMesh(material2), c.ToVector3(), scale, altitude, color);
            }
        }

        private void AddSubstructure(CornerType cornerType, IntVec3 c, bool substructureToSouth, bool addGravshipMask, bool addIndoorMask)
        {
            if (cornerType == CornerType.Corner_NW || cornerType == CornerType.Diagonal_NW)
            {
                AddQuad(mat_SubStructure_W.Material, c, 1f, substructureAltitude, Color.white, addGravshipMask, addIndoorMask);
                if (!substructureToSouth)
                {
                    AddQuad(mat_SubStructureExtra_W.Material, c + IntVec3.South.RotatedBy(VehicleMapUtility.RotForPrintCounter), 1f, substructureAltitude, Color.white, addGravshipMask, addIndoorMask);
                }
            }
            if (cornerType == CornerType.Corner_NE || cornerType == CornerType.Diagonal_NE)
            {
                AddQuad(mat_SubStructure_E.Material, c, 1f, substructureAltitude, Color.white, addGravshipMask, addIndoorMask);
                if (!substructureToSouth)
                {
                    AddQuad(mat_SubStructureExtra_E.Material, c + IntVec3.South.RotatedBy(VehicleMapUtility.RotForPrintCounter), 1f, substructureAltitude, Color.white, addGravshipMask, addIndoorMask);
                }
            }
        }

        new public static bool ShouldDrawCornerPiece(IntVec3 pos, Map map, TerrainGrid terrGrid, out CornerType cornerType, out Color color)
        {
            cornerType = CornerType.None;
            color = Color.white;
            var edifice = pos.GetEdifice(map);
            if (edifice != null && edifice.def != VMF_DefOf.VMF_VehicleStructureEmpty)
            {
                return false;
            }
            TerrainDef terrainDef = terrGrid.FoundationAt(pos);
            if (terrainDef != null && terrainDef.IsSubstructure)
            {
                return false;
            }
            for (int i = 0; i < Directions.Length; i++)
            {
                tmpChecks[i] = (pos + Directions[i].RotatedBy(VehicleMapUtility.RotForPrintCounter)).GetEdificeSafe(map)?.def == ThingDefOf.GravshipHull;
            }
            if (tmpChecks[0] && tmpChecks[3] && !tmpChecks[2] && !tmpChecks[1])
            {
                cornerType = (tmpChecks[4] ? CornerType.Corner_NW : CornerType.Diagonal_NW);
            }
            else if (tmpChecks[0] && tmpChecks[1] && !tmpChecks[2] && !tmpChecks[3])
            {
                cornerType = (tmpChecks[5] ? CornerType.Corner_NE : CornerType.Diagonal_NE);
            }
            else if (tmpChecks[2] && tmpChecks[1] && !tmpChecks[0] && !tmpChecks[3])
            {
                cornerType = (tmpChecks[6] ? CornerType.Corner_SE : CornerType.Diagonal_SE);
            }
            else if (tmpChecks[2] && tmpChecks[3] && !tmpChecks[0] && !tmpChecks[1])
            {
                cornerType = (tmpChecks[7] ? CornerType.Corner_SW : CornerType.Diagonal_SW);
            }
            if (cornerType == CornerType.None)
            {
                return false;
            }
            int[][] array = directionPairs;
            for (int j = 0; j < array.Length; j++)
            {
                List<int> list = [.. array[j].Where(num2 => tmpChecks[num2])];
                if (list.Count > 0)
                {
                    int num = list.First();
                    color = (pos + Directions[num].RotatedBy(VehicleMapUtility.RotForPrintCounter)).GetEdificeSafe(map).DrawColor;
                    break;
                }
            }
            return true;
        }
    }
}
