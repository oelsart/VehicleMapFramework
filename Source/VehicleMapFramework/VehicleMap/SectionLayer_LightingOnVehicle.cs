using System.Linq;
using System.Text;
using JetBrains.Annotations;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace VehicleMapFramework;

[StaticConstructorOnStartup]
public class SectionLayer_LightingOnVehicle : SectionLayer
{
    private int firstCenterInd;
    private CellRect sectRect;

    private static Material LightOverlayInverseMultiply;
    private static MaterialPropertyBlock materialPropertyBlock = new();
    private static readonly int RestoreFactor = Shader.PropertyToID("_RestoreFactor");
    private static readonly int MaxRestore = Shader.PropertyToID("_MaxRestore");
    private static readonly int ColorPreservation = Shader.PropertyToID("_ColorPreservation");

    [TweakValue("SectionLayer_LightingOnVehicle._RestoreFactor", 0f, 10f)]
    [UsedImplicitly] private static float restoreFactor = 1.5f;
    [TweakValue("SectionLayer_LightingOnVehicle._MaxRestore", 0f, 1f)]
    [UsedImplicitly] private static float maxRestore = 0.85f;

    private readonly bool[] expand = new bool[4];

    private const byte RoofedAreaMinSkyCover = 100;
    private const int ExpandSize = 10;

    static SectionLayer_LightingOnVehicle()
    {
        LongEventHandler.ExecuteWhenFinished(() =>
        {
            LightOverlayInverseMultiply = MaterialPool.MatFrom(VMF_DefOf.VMF_LightOverlayInverseMultiply.Shader);
            materialPropertyBlock = new MaterialPropertyBlock();
        });
    }
    
    public override bool Visible => DebugViewSettings.drawLightingOverlay && (Find.CurrentMap != Map || VehicleMapFramework.settings.drawPlanet);

    public SectionLayer_LightingOnVehicle(Section section) : base(section)
    {
        relevantChangeTypes = MapMeshFlagDefOf.Roofs | MapMeshFlagDefOf.GroundGlow;
    }

    //drawPlanetがオフでVehicleMapにフォーカスした時しか呼ばれないよ
    public override void DrawLayer()
    {
    }

    public void DrawLayer(Vector3 drawPos)
    {
        if (!Visible || !Map.IsVehicleMapOf(out var vehicle))
        {
            return;
        }
        var baseMap = Map.BaseMap();
        var rot = Quaternion.AngleAxis(vehicle.FullAngle, Vector3.up);
        for (var i = 0; i < subMeshes.Count; i++)
        {
            var subMesh = subMeshes[i];
            if (subMesh.finalized && !subMesh.disabled)
            {
                if (subMesh.material == LightOverlayInverseMultiply)
                {
                    materialPropertyBlock.SetColor(ShaderPropertyIDs.Color, baseMap.skyManager.CurSky.colors.sky);
                    materialPropertyBlock.SetFloat(RestoreFactor, restoreFactor);
                    materialPropertyBlock.SetFloat(MaxRestore, maxRestore);
                }
                else
                {
                    materialPropertyBlock.SetColor(ShaderPropertyIDs.Color, Color.white);
                }
                Graphics.DrawMesh(subMesh.mesh, drawPos, rot, subMesh.material, 0, null, 0, materialPropertyBlock);
            }
        }
    }

    public string GlowReportAt(IntVec3 c)
    {
        var colors = GetSubMesh(LightOverlayInverseMultiply).mesh.colors32;
        CalculateVertexIndices(c.x, c.z, out var num, out var num2, out var num3, out var num4, out var num5);
        StringBuilder stringBuilder = new();
        stringBuilder.Append("BL=" + colors[num]);
        stringBuilder.Append("\nTL=" + colors[num2]);
        stringBuilder.Append("\nTR=" + colors[num3]);
        stringBuilder.Append("\nBR=" + colors[num4]);
        stringBuilder.Append("\nCenter=" + colors[num5]);
        return stringBuilder.ToString();
    }

    public override void Regenerate()
    {
        if (!Map.IsVehicleMap)
        {
            return;
        }
        var subMesh = GetSubMesh(LightOverlayInverseMultiply);
        var subMesh2 = GetSubMesh(MatBases.LightOverlay);
        if (subMesh.verts.Count == 0)
        {
            MakeBaseGeometry(subMesh, AltitudeLayer.LightingOverlay.AltitudeFor().YOffset());
        }
        if (subMesh2.verts.Count == 0)
        {
            MakeBaseGeometry(subMesh2, AltitudeLayer.LightingOverlay.AltitudeFor().YOffset());
        }
        var array = new Color32[subMesh.verts.Count];
        var origRect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
        origRect.ClipInsideMap(Map);
        var maxX = origRect.maxX;
        var maxZ = origRect.maxZ;
        var width = sectRect.Width;
        var map = Map;
        var x = map.Size.x;
        var innerArray = map.edificeGrid.InnerArray;
        var num = innerArray.Length;
        var roofGrid = map.roofGrid;
        var cellIndices = map.cellIndices;
        CalculateVertexIndices(origRect.minX, origRect.minZ, out var num2, out _, out _, out _, out _);
        var num7 = cellIndices.CellToIndex(new IntVec3(origRect.minX, 0, origRect.minZ));
        var array3 = new int[4];
        array3[0] = -x - 1;
        array3[1] = -x;
        array3[2] = -1;
        var array4 = new int[4];
        array4[0] = -1;
        array4[1] = -1;
        for (var i = origRect.minZ; i <= maxZ + 1; i++)
        {
            var num8 = num7 / x;
            var j = origRect.minX;
            while (j <= maxX + 1)
            {
                ColorInt colorInt = new(0, 0, 0, 0);
                var num9 = 0;
				var canShowLight = false;
                for (var k = 0; k < 4; k++)
                {
                    var num10 = num7 + array3[k];
                    if (num10 >= 0 && num10 < num && num10 / x == num8 + array4[k])
                    {
                        var thing = innerArray[num10];
                        var roofDef = roofGrid.RoofAt(num10);
						if (roofDef != null && thing is not { def: { holdsRoof: true, altitudeLayer: not AltitudeLayer.DoorMoveable } })
                        {
							canShowLight = true;
                        }
                        if (thing is not { def.blockLight: true })
                        {
                            colorInt += map.glowGrid.VisualGlowAt(num10);
                            num9++;
                            if (!canShowLight && Mathf.Max(colorInt.r, colorInt.g, colorInt.b) > 0)
                                canShowLight = true;
                        }
                    }
                }
                if (num9 > 0)
                {
                    array[num2] = (colorInt / num9).ProjectToColor32();
                }
                else
                {
                    array[num2] = new Color32(255, 255, 255, 0);
                }
				if (canShowLight && array[num2].a < RoofedAreaMinSkyCover)
                {
                    array[num2].a = RoofedAreaMinSkyCover;
                }
                j++;
                num2++;
                num7++;
            }
            var num11 = maxX + 2 - sectRect.minX;
            var offset = num11;
            if (expand[3]) offset -= ExpandSize;
            num2 -= offset;
            num7 -= offset;
            num2 += width + 1;
            num7 += map.Size.x;
        }

        CalculateVertexIndices(origRect.minX, origRect.minZ, out var num12, out _, out _, out _, out var num13);
        for (var l = origRect.minZ; l <= maxZ; l++)
        {
            var m = origRect.minX;
            while (m <= maxX)
            {
                var colorInt = default(ColorInt) + array[num12];
                colorInt += array[num12 + 1];
                colorInt += array[num12 + width + 1];
                colorInt += array[num12 + width + 2];
                array[num13] = new Color32((byte)(colorInt.r / 4), (byte)(colorInt.g / 4), (byte)(colorInt.b / 4), (byte)(colorInt.a / 4));
                m++;
                num12++;
                num13++;
            }
            var offset = 0;
            if (expand[3]) offset++;
            if (expand[1]) offset++;
            num12 += (offset * ExpandSize) + 1;
            num13 += offset * ExpandSize;
        }
        
        //こっから下でマップ周辺に漏れ出る光の計算
        var rect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
        rect.ClipInsideMap(Map);
        rect = rect.MovedBy(-rect.Min);
        var initRect = rect;

        if (expand.Any(e => e))
        {
            for (var i = 0; i < ExpandSize; i++)
            {
                if (expand[0])
                {
                    rect.maxZ += 1;
                }
                if (expand[1])
                {
                    rect.maxX += 1;
                }
                if (expand[2])
                {
                    rect.minZ -= 1;
                }
                if (expand[3])
                {
                    rect.minX -= 1;
                }
                var rect2 = rect;
                rect2.maxX++;
                rect2.maxZ++;
                for (var j = 0; j < 4; j++)
                {
                    var rot = new Rot4(j);
                    if (expand[j])
                    {
                        var edgeRect = rect2.GetEdgeRect(rot);
                        TrimCorner(ref edgeRect, j);
                        foreach (var cell in edgeRect)
                        {
                            var edge = initRect.ClosestCellTo(cell);
                            var edgeColorCorner = array[IndexGetterCorner(edge)];
                            var corner = IndexGetterCorner(cell);
                            var length = (edge - cell).LengthHorizontal;
                            var factor = Mathf.Lerp(1f, 0f, length / ExpandSize);
                            array[corner] = new Color32((byte)(edgeColorCorner.r * factor), (byte)(edgeColorCorner.g * factor), (byte)(edgeColorCorner.b * factor), (byte)(edgeColorCorner.a * factor));
                        }

                        var edgeRect2 = rect.GetEdgeRect(rot);
                        TrimCorner(ref edgeRect2, j);
                        foreach (var cell in edgeRect2)
                        {
                            var corner = IndexGetterCorner(cell);
                            var center = IndexGetterCenter(cell);
                            var colorInt = default(ColorInt) + array[corner];
                            colorInt += array[corner + 1];
                            colorInt += array[corner + width + 1];
                            colorInt += array[corner + width + 2];
                            array[center] = new Color32((byte)(colorInt.r / 4), (byte)(colorInt.g / 4), (byte)(colorInt.b / 4), (byte)(colorInt.a / 4));
                        }
                    }
                }
            }
        }

        subMesh.mesh.colors32 = array;
        subMesh2.mesh.colors32 = array;
        return;

        void TrimCorner(ref CellRect edgeRect, int index)
        {
            if (expand[(index + 1) % 4])
            {
                switch (index)
                {
                    case 0:
                        edgeRect.maxX -= 1;
                        break;
                    case 1:
                        edgeRect.minZ += 1;
                        break;
                    case 2:
                        edgeRect.minX += 1;
                        break;
                    case 3:
                        edgeRect.maxZ -= 1;
                        break;
                }
            }
        }

        int IndexGetterCorner(IntVec3 c)
        {
            return (((expand[2] ? ExpandSize : 0) + c.z) * (width + 1)) + (expand[3] ? ExpandSize : 0) + c.x;
        }

        int IndexGetterCenter(IntVec3 c)
        {
            return firstCenterInd + (((expand[2] ? ExpandSize : 0) + c.z) * width) + (expand[3] ? ExpandSize : 0) + c.x;
        }
    }

    private void MakeBaseGeometry(LayerSubMesh sm, float altitude)
    {
        sectRect = new CellRect(section.botLeft.x, section.botLeft.z, 17, 17);
        sectRect.ClipInsideMap(Map);
        var min = sectRect.Min;
        var max = sectRect.Max;
        if (!(max + IntVec3.North).InBounds(Map))
        {
            expand[0] = true;
            sectRect.maxZ += ExpandSize;
        }
        if (!(max + IntVec3.East).InBounds(Map))
        {
            expand[1] = true;
            sectRect.maxX += ExpandSize;
        }
        if (!(min + IntVec3.South).InBounds(Map))
        {
            expand[2] = true;
            sectRect.minZ -= ExpandSize;
        }
        if (!(min + IntVec3.West).InBounds(Map))
        {
            expand[3] = true;
            sectRect.minX -= ExpandSize;
        }
        var capacity = ((sectRect.Width + 1) * (sectRect.Height + 1)) + sectRect.Area;
        sm.verts.Capacity = capacity;
        for (var i = sectRect.minZ; i <= sectRect.maxZ + 1; i++)
        {
            for (var j = sectRect.minX; j <= sectRect.maxX + 1; j++)
            {
                sm.verts.Add(new Vector3(j, altitude, i));
            }
        }
        firstCenterInd = sm.verts.Count;
        for (var k = sectRect.minZ; k <= sectRect.maxZ; k++)
        {
            for (var l = sectRect.minX; l <= sectRect.maxX; l++)
            {
                sm.verts.Add(new Vector3(l + 0.5f, altitude, k + 0.5f));
            }
        }
        sm.tris.Capacity = sectRect.Area * 4 * 3;
        for (var m = sectRect.minZ; m <= sectRect.maxZ; m++)
        {
            for (var n = sectRect.minX; n <= sectRect.maxX; n++)
            {
                CalculateVertexIndices(n, m, out var item, out var item2, out var item3, out var item4, out var item5);
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
        var num = worldX - sectRect.minX;
        var num2 = worldZ - sectRect.minZ;
        botLeft = (num2 * (sectRect.Width + 1)) + num;
        topLeft = ((num2 + 1) * (sectRect.Width + 1)) + num;
        topRight = ((num2 + 1) * (sectRect.Width + 1)) + num + 1;
        botRight = (num2 * (sectRect.Width + 1)) + num + 1;
        center = firstCenterInd + (num2 * sectRect.Width) + num;
    }
}
