using RimWorld;
using System.Collections.Generic;
using System.Xml;
using Verse;

namespace VehicleMapFramework;

public class CompProperties_Alternator : CompProperties_Power
{
    public CompProperties_Alternator()
    {
        compClass = typeof(CompAlternator);
    }

    public List<FuelProperties> fuelConsumptionRates = [];

    public class FuelProperties
    {
        public ThingDef fuelDef;

        public float fuelConsumptionRate = 1f;

        public FuelProperties() { }

        public void LoadDataFromXmlCustom(XmlNode xmlRoot)
        {
            DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "fuelDef", xmlRoot.Name, null, null, null);
            fuelConsumptionRate = ParseHelper.FromString<float>(xmlRoot.InnerText);
        }
    }
}
