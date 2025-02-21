using HarmonyLib;
using RimWorld;
using SmashTools;
using System;
using System.Linq;
using System.Text;
using UnityEngine;
using Verse;

namespace VehicleInteriors
{
    [StaticConstructorOnStartup]
    public class SectionLayer_LightingOnVehicle : SectionLayer
    {
        static SectionLayer_LightingOnVehicle()
        {
            var offset = Altitudes.AltInc * 230;
            for (int i = 30; i < 38; i++)
            {
                Alts[i] += offset;
            }
        }

        public override bool Visible
        {
            get
            {
                return DebugViewSettings.drawLightingOverlay && Find.CurrentMap != base.Map;
            }
        }

        public SectionLayer_LightingOnVehicle(Section section) : base(section)
        {
            this.relevantChangeTypes = (MapMeshFlagDefOf.Roofs | MapMeshFlagDefOf.GroundGlow);
            this.propertyBlock = new MaterialPropertyBlock();
        }

        public void DrawLayer(VehiclePawnWithMap vehicle, Vector3 drawPos, float extraRotation)
        {
            if (!this.Visible)
            {
                return;
            }
            var baseMap = base.Map.BaseMap();
            var angle = Ext_Math.RotateAngle(vehicle.FullRotation.AsAngle, extraRotation);
            float a = Mathf.Clamp01(1f - Mathf.Min(baseMap.gameConditionManager.MapBrightness, baseMap.skyManager.CurSkyGlow));
            this.propertyBlock.SetColor(ShaderPropertyIDs.ColorTwo, new Color(1f, 1f, 1f, a));
            foreach (var subMesh in this.subMeshes)
            {
                if (subMesh.finalized && !subMesh.disabled)
                {
                    Graphics.DrawMesh(subMesh.mesh, drawPos, Quaternion.AngleAxis(angle, Vector3.up), subMesh.material, 0, null, 0, this.propertyBlock);
                }
            }
        }

        public string GlowReportAt(IntVec3 c)
        {
            Color32[] colors = base.GetSubMesh(VMF_Materials.LightOverlayColorDodge).mesh.colors32;
            this.CalculateVertexIndices(c.x, c.z, out int num, out int num2, out int num3, out int num4, out int num5);
            StringBuilder stringBuilder = new StringBuilder();
            stringBuilder.Append("BL=" + colors[num]);
            stringBuilder.Append("\nTL=" + colors[num2]);
            stringBuilder.Append("\nTR=" + colors[num3]);
            stringBuilder.Append("\nBR=" + colors[num4]);
            stringBuilder.Append("\nCenter=" + colors[num5]);
            return stringBuilder.ToString();
        }

        public override void Regenerate()
        {
            LayerSubMesh subMesh = base.GetSubMesh(VMF_Materials.LightOverlayColorDodge);
            if (subMesh.verts.Count == 0)
            {
                this.MakeBaseGeometry(subMesh);
            }
            Color32[] array = new Color32[subMesh.verts.Count];
            var origRect = new CellRect(this.section.botLeft.x, this.section.botLeft.z, 17, 17);
            origRect.ClipInsideMap(base.Map);
            int maxX = origRect.maxX;
            int maxZ = origRect.maxZ;
            int width = this.sectRect.Width;
            Map map = base.Map;
            int x = map.Size.x;
            Thing[] innerArray = map.edificeGrid.InnerArray;
            int num = innerArray.Length;
            RoofGrid roofGrid = map.roofGrid;
            CellIndices cellIndices = map.cellIndices;
            this.CalculateVertexIndices(origRect.minX, origRect.minZ, out int num2, out _, out _, out _, out _);
            int num7 = cellIndices.CellToIndex(new IntVec3(origRect.minX, 0, origRect.minZ));
            int[] array2 = new int[4];
            array2[0] = -x - 1;
            array2[1] = -x;
            array2[2] = -1;
            int[] array3 = new int[4];
            array3[0] = -1;
            array3[1] = -1;
            for (int i = origRect.minZ; i <= maxZ + 1; i++)
            {
                int num8 = num7 / x;
                int j = origRect.minX;
                while (j <= maxX + 1)
                {
                    ColorInt colorInt = new ColorInt(0, 0, 0, 0);
                    int num9 = 0;
                    bool flag = false;
                    for (int k = 0; k < 4; k++)
                    {
                        int num10 = num7 + array2[k];
                        if (num10 >= 0 && num10 < num && num10 / x == num8 + array3[k])
                        {
                            Thing thing = innerArray[num10];
                            RoofDef roofDef = roofGrid.RoofAt(num10);
                            if (roofDef != null && (roofDef.isThickRoof || thing == null || !thing.def.holdsRoof || thing.def.altitudeLayer == AltitudeLayer.DoorMoveable))
                            {
                                flag = true;
                            }
                            if (thing == null || !thing.def.blockLight)
                            {
                                colorInt += map.glowGrid.VisualGlowAt(num10);
                                num9++;
                            }
                        }
                    }
                    if (num9 > 0)
                    {
                        array[num2] = (colorInt / num9).ProjectToColor32;
                    }
                    else
                    {
                        array[num2] = new Color32(0, 0, 0, 0);
                    }
                    if (flag && array[num2].a < RoofedAreaMinSkyCover)
                    {
                        array[num2].a = RoofedAreaMinSkyCover;
                    }
                    j++;
                    num2++;
                    num7++;
                }
                int num11 = maxX + 2 - this.sectRect.minX;
                var offset = num11;
                if (this.expandWest) offset -= ExpandSize;
                num2 -= offset;
                num7 -= offset;
                num2 += width + 1;
                num7 += map.Size.x;
            }

            this.CalculateVertexIndices(origRect.minX, origRect.minZ, out int num12, out _, out int _, out int _, out int num13);
            int num14 = cellIndices.CellToIndex(origRect.minX, origRect.minZ);
            for (int l = origRect.minZ; l <= maxZ; l++)
            {
                int m = origRect.minX;
                while (m <= maxX)
                {
                    ColorInt colorInt2 = default(ColorInt) + array[num12];
                    colorInt2 += array[num12 + 1];
                    colorInt2 += array[num12 + width + 1];
                    colorInt2 += array[num12 + width + 2];
                    array[num13] = new Color32((byte)(colorInt2.r / 4), (byte)(colorInt2.g / 4), (byte)(colorInt2.b / 4), (byte)(colorInt2.a / 4));
                    if (array[num13].a < RoofedAreaMinSkyCover && roofGrid.Roofed(num14))
                    {
                        Thing thing2 = innerArray[num14];
                        if (thing2 == null || !thing2.def.holdsRoof)
                        {
                            array[num13].a = RoofedAreaMinSkyCover;
                        }
                    }
                    m++;
                    num12++;
                    num13++;
                    num14++;
                }
                var offset = 0;
                if (this.expandWest) offset++;
                if (this.expandEast) offset++;
                num12 += offset * ExpandSize + 1;
                num13 += offset * ExpandSize;
                num14 -= width - offset * ExpandSize;
                num14 += map.Size.x;
            }

            //こっから下でマップ周辺に漏れ出る光の計算
            var rect = new CellRect(this.section.botLeft.x, this.section.botLeft.z, 17, 17);
            rect.ClipInsideMap(base.Map);
            rect = rect.MovedBy(-rect.Min);

            int IndexGetterCorner(IntVec3 c)
            {
                return ((expandSouth ? ExpandSize : 0) + c.z) * (width + 1) + (expandWest ? ExpandSize : 0) + c.x;
            }

            int IndexGetterCenter(IntVec3 c)
            {
                return this.firstCenterInd + ((expandSouth ? ExpandSize : 0) + c.z) * width + (expandWest ? ExpandSize : 0) + c.x;
            }

            if (this.expandEast || this.expandSouth || this.expandEast || this.expandNorth)
            {
                for (var i = ExpandSize; i > 0; i--)
                {
                    var prevRect = rect;
                    if (this.expandWest)
                    {
                        rect.minX -= 1;
                    }
                    if (this.expandSouth)
                    {
                        rect.minZ -= 1;
                    }
                    if (this.expandEast)
                    {
                        rect.maxX += 1;
                    }
                    if (this.expandNorth)
                    {
                        rect.maxZ += 1;
                    }
                    var rect2 = rect;
                    rect2.maxX++;
                    rect2.maxZ++;
                    prevRect.maxX++;
                    prevRect.maxZ++;
                    var cells = Enumerable.Empty<IntVec3>();
                    if (this.expandWest)
                    {
                        cells = cells.Union(rect2.GetEdgeCells(Rot4.West));
                    }
                    if (this.expandSouth)
                    {
                        cells = cells.Union(rect2.GetEdgeCells(Rot4.South));
                    }
                    if (this.expandEast)
                    {
                        cells = cells.Union(rect2.GetEdgeCells(Rot4.East));
                    }
                    if (this.expandNorth)
                    {
                        cells = cells.Union(rect2.GetEdgeCells(Rot4.North));
                    }
                    foreach (var cell in cells)
                    {
                        var edge = prevRect.ClosestCellTo(cell);
                        var cardinal = (edge - cell).IsCardinal;
                        byte Decrease(byte cur)
                        {
                            return (byte)Math.Max(0, cur - cur / i - (cardinal ? 50 : 100) / ExpandSize);
                        }
                        var edgeColorCorner = array[IndexGetterCorner(edge)];
                        array[IndexGetterCorner(cell)] = new Color32(Decrease(edgeColorCorner.r), Decrease(edgeColorCorner.g), Decrease(edgeColorCorner.b), edgeColorCorner.a);
                    }

                    cells = Enumerable.Empty<IntVec3>();
                    if (this.expandWest)
                    {
                        cells = cells.Union(rect.GetEdgeCells(Rot4.West));
                    }
                    if (this.expandSouth)
                    {
                        cells = cells.Union(rect.GetEdgeCells(Rot4.South));
                    }
                    if (this.expandEast)
                    {
                        cells = cells.Union(rect.GetEdgeCells(Rot4.East));
                    }
                    if (this.expandNorth)
                    {
                        cells = cells.Union(rect.GetEdgeCells(Rot4.North));
                    }
                    foreach (var cell in cells)
                    {
                        var corner = IndexGetterCorner(cell);
                        ColorInt colorInt = default(ColorInt) + array[corner];
                        colorInt += array[corner + 1];
                        colorInt += array[corner + width + 1];
                        colorInt += array[corner + width + 2];
                        array[IndexGetterCenter(cell)] = new Color32((byte)(colorInt.r / 4), (byte)(colorInt.g / 4), (byte)(colorInt.b / 4), (byte)(colorInt.a / 4));
                    }
                }
            }

            subMesh.mesh.colors32 = array;
        }

        private void MakeBaseGeometry(LayerSubMesh sm)
        {
            this.sectRect = new CellRect(this.section.botLeft.x, this.section.botLeft.z, 17, 17);
            this.sectRect.ClipInsideMap(base.Map);
            var min = this.sectRect.Min;
            var max = this.sectRect.Max;
            if (!(min + IntVec3.West).InBounds(base.Map))
            {
                this.expandWest = true;
                this.sectRect.minX -= ExpandSize;
            }
            if (!(min + IntVec3.South).InBounds(base.Map))
            {
                this.expandSouth = true;
                this.sectRect.minZ -= ExpandSize;
            }
            if (!(max + IntVec3.East).InBounds(base.Map))
            {
                this.expandEast = true;
                this.sectRect.maxX += ExpandSize;
            }
            if (!(max + IntVec3.North).InBounds(base.Map))
            {
                this.expandNorth = true;
                this.sectRect.maxZ += ExpandSize;
            }
            int capacity = (this.sectRect.Width + 1) * (this.sectRect.Height + 1) + this.sectRect.Area;
            float y = AltitudeLayer.PawnState.AltitudeFor();
            sm.verts.Capacity = capacity;
            for (int i = this.sectRect.minZ; i <= this.sectRect.maxZ + 1; i++)
            {
                for (int j = this.sectRect.minX; j <= this.sectRect.maxX + 1; j++)
                {
                    sm.verts.Add(new Vector3((float)j, y, (float)i));
                }
            }
            this.firstCenterInd = sm.verts.Count;
            for (int k = this.sectRect.minZ; k <= this.sectRect.maxZ; k++)
            {
                for (int l = this.sectRect.minX; l <= this.sectRect.maxX; l++)
                {
                    sm.verts.Add(new Vector3((float)l + 0.5f, y, (float)k + 0.5f));
                }
            }
            sm.tris.Capacity = this.sectRect.Area * 4 * 3;
            for (int m = this.sectRect.minZ; m <= this.sectRect.maxZ; m++)
            {
                for (int n = this.sectRect.minX; n <= this.sectRect.maxX; n++)
                {
                    this.CalculateVertexIndices(n, m, out int item, out int item2, out int item3, out int item4, out int item5);
                    sm.tris.Add(item);
                    sm.tris.Add(item5);
                    sm.tris.Add(item4);
                    sm.tris.Add(item);
                    sm.tris.Add(item2);
                    sm.tris.Add(item5);
                    sm.tris.Add(item2);
                    sm.tris.Add(item3);
                    sm.tris.Add(item5);
                    sm.tris.Add(item3);
                    sm.tris.Add(item4);
                    sm.tris.Add(item5);
                }
            }
            sm.FinalizeMesh(MeshParts.Verts | MeshParts.Tris);
        }

        private void CalculateVertexIndices(int worldX, int worldZ, out int botLeft, out int topLeft, out int topRight, out int botRight, out int center)
        {
            int num = worldX - this.sectRect.minX;
            int num2 = worldZ - this.sectRect.minZ;
            botLeft = num2 * (this.sectRect.Width + 1) + num;
            topLeft = (num2 + 1) * (this.sectRect.Width + 1) + num;
            topRight = (num2 + 1) * (this.sectRect.Width + 1) + (num + 1);
            botRight = num2 * (this.sectRect.Width + 1) + (num + 1);
            center = this.firstCenterInd + (num2 * this.sectRect.Width + num);
        }

        private int firstCenterInd;

        private CellRect sectRect;

        private const byte RoofedAreaMinSkyCover = 100;

        private MaterialPropertyBlock propertyBlock;

        private bool expandWest;

        private bool expandSouth;

        private bool expandEast;

        private bool expandNorth;

        private const int ExpandSize = 5;

        private static readonly float[] Alts = AccessTools.StaticFieldRefAccess<float[]>(typeof(Altitudes), "Alts");
    }
}
