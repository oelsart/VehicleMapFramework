using RimWorld;
using Verse;

namespace VMF_PUAHPatch
{
    [DefOf]
    public static class VMF_PUAH_DefOf
    {
        static VMF_PUAH_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(VMF_PUAH_DefOf));
        }

        public static JobDef HaulToInventoryAcrossMaps;
    }
}
