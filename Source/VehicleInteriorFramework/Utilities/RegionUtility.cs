using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Vehicles;
using Verse;

namespace VehicleInteriors
{
    public static class RegionUtility
    {
        public static IntVec3 Anchor(this RegionLink link)
        {
            int SpanCenterX(EdgeSpan e)
            {
                return e.root.x + ((e.dir != SpanDirection.East) ? 0 : (e.length / 2));
            }

            int SpanCenterZ(EdgeSpan e)
            {
                return e.root.z + ((e.dir != SpanDirection.North) ? 0 : (e.length / 2));
            }

            return new IntVec3(SpanCenterX(link.span), 0, SpanCenterZ(link.span));
        }

        public static Region GetInFacingRegion(this RegionLink regionLinkA, RegionLink regionLinkB)
        {
            if (regionLinkA.RegionA == regionLinkB.RegionA || regionLinkA.RegionA == regionLinkB.RegionB)
            {
                return regionLinkA.RegionA;
            }
            if (regionLinkA.RegionB == regionLinkB.RegionB || regionLinkA.RegionB == regionLinkB.RegionA)
            {
                return regionLinkA.RegionB;
            }
            Log.Warning(string.Format("Attempting to fetch region between links {0} and {1}, but they do not share a region.\n--- Regions ---\n{2}\n{3}\n{4}\n{5}\n", new object[]
            {
                regionLinkA.Anchor(),
                regionLinkB.Anchor(),
                regionLinkA.RegionA,
                regionLinkA.RegionB,
                regionLinkB.RegionA,
                regionLinkB.RegionB
            }));
            return null;
        }

        public static int EuclideanDistance(this RegionLink link, IntVec3 cell)
        {
            IntVec3 intVec = cell - link.Anchor();
            return Mathf.RoundToInt(Mathf.Sqrt(Mathf.Pow((float)intVec.x, 2f) + Mathf.Pow((float)intVec.z, 2f)));
        }

        //private static int HashBetween(RegionLink linkA, RegionLink linkB)
        //{
        //    return Gen.HashCombine(linkA.Anchor().GetHashCode(), linkB.Anchor());
        //}

        //public static RegionWeight WeightBetween(RegionLink linkA, RegionLink linkB)
        //{
        //    int key = HashBetween(linkA, linkB);
        //    lock (new object())
        //    {
        //        RegionWeight result;
        //        if (this.weights.TryGetValue(key, out result))
        //        {
        //            return result;
        //        }
        //    }
        //    Log.Error(string.Format("Unable to pull weight between {0} and {1}", linkA.Anchor(), linkB.Anchor()));
        //    return new RegionWeight(linkA, linkB, 999);
        //}

        //public struct RegionWeight
        //{
        //    public RegionLink linkA;

        //    public RegionLink linkB;

        //    public int cost;

        //    public bool IsValid
        //    {
        //        get
        //        {
        //            if (linkA != null)
        //            {
        //                return linkB != null;
        //            }

        //            return false;
        //        }
        //    }

        //    public RegionWeight(RegionLink linkA, RegionLink linkB, int cost)
        //    {
        //        this.linkA = linkA;
        //        this.linkB = linkB;
        //        this.cost = cost;
        //    }
        //}
    }
}
