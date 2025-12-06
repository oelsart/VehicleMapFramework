using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Vehicles;
using Verse;

namespace VehicleMapFramework;

public class CompRelatedBuildCommands : VehicleComp
{
    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        foreach (var dropdownGroup in BuildRelatedCommandUtility.RelatedBuildCommands(Vehicle.VehicleDef.buildDef)
                     .OfType<Designator_Build>()
                     .GroupBy(des => des.PlacingDef?.designatorDropdown))
        {
            if (dropdownGroup.Key is null)
            {
                foreach (var des in dropdownGroup)
                {
                    yield return des;
                }
            }
            else
            {
                foreach (var categoryGroup in dropdownGroup
                             .GroupBy(des => des.PlacingDef?.designationCategory))
                {
                    var dropdown = categoryGroup.Key?.ResolvedAllowedDesignators
                        .FirstOrDefault(des =>
                            des is Designator_Dropdown designatorDropdown &&
                            designatorDropdown.Elements.Any(des2 => categoryGroup.Contains(des2)));
                    if (dropdown != null)
                        yield return dropdown;
                }
            }
        }
    }
}