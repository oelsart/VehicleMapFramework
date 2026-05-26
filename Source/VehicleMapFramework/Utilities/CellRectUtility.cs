using System;
using System.Collections;
using System.Collections.Generic;
using Verse;

namespace VehicleMapFramework;

public static class CellRectUtility
{
    extension(CellRect cellRect)
    {
        public CellRectReversible Reverse => new (cellRect, true);

        public CellRectReversible EdgeRectClockwise(Rot4 rot)
        {
            var edgeRect = cellRect.GetEdgeRect(rot);
            return rot.AsInt is Rot4.EastInt or Rot4.SouthInt
                ? new CellRectReversible(edgeRect, true)
                : new CellRectReversible(edgeRect);
        }
    }

    public readonly struct CellRectReversible(CellRect cellRect, bool reverse = false) : IEnumerable<IntVec3>
    {
        public CellRect InnerRect => cellRect;
        
        public Enumerator GetEnumerator()
        {
            return new Enumerator(cellRect, reverse);
        }
        
        IEnumerator<IntVec3> IEnumerable<IntVec3>.GetEnumerator()
        {
            return GetEnumerator();
        }
        
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    
        public struct Enumerator(CellRect ir, bool reverse = false) : IEnumerator<IntVec3>
        {
            private int x = reverse ? ir.maxX + 1 : ir.minX - 1;

            private int z = reverse ? ir.maxZ : ir.minZ;
        
            public IntVec3 Current => new(x, 0, z);

            object IEnumerator.Current => new IntVec3(x, 0, z);

            public bool MoveNext()
            {
                if (reverse)
                {
                    x--;
                    if (x < ir.minX)
                    {
                        x = ir.maxX;
                        z--;
                    }
                    return z >= ir.minZ;
                }
                x++;
                if (x > ir.maxX)
                {
                    x = ir.minX;
                    z++;
                }
                return z <= ir.maxZ;
            }

            public void Reset()
            {
                x = reverse ? ir.maxX + 1 : ir.minX - 1;
                z = reverse ? ir.maxZ : ir.minZ;
            }

            void IDisposable.Dispose()
            {
            }
        }
    }
}