using System.Collections.Generic;
using System.Xml;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompProperties_Alternator : CompProperties_Power
{
    public CompProperties_Alternator()
    {
        compClass = typeof(CompAlternator);
    }

    public List<FuelProperties> fuelConsumptionRates;

    public class FuelProperties
    {
        public ThingDef fuelDef;

        public float fuelConsumptionRate = 1f;

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "fuelDef", xmlRoot.Name);
            fuelConsumptionRate = ParseHelper.FromString<float>(xmlRoot.InnerText);
        }
    }
}
