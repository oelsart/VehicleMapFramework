using System.Collections.Generic;
using System.Xml;
using RimWorld;
using Verse;

namespace VehicleMapFramework;

public class CompProperties_Alternator : CompProperties_Power
{

  public List<FuelProperties> fuelConsumptionRates;

  public CompProperties_Alternator()
  {
    compClass = typeof(CompAlternator);
  }

  public class FuelProperties
  {

    public float fuelConsumptionRate = 1f;
    public ThingDef fuelDef;

    public void LoadDataFromXmlCustom(XmlNode xmlRoot)
    {
      DirectXmlCrossRefLoader.RegisterObjectWantsCrossRef(this, "fuelDef", xmlRoot.Name);
      fuelConsumptionRate = ParseHelper.FromString<float>(xmlRoot.InnerText);
    }
  }
}
