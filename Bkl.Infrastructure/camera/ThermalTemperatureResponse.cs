namespace Bkl.Infrastructure
{
    public class ThermalTemperatureResponse
    {
        public int ruleId { get; set; }
        public float max { get; set; }
        public float min { get; set; }
        public float average { get; set; }
        public float threshold { get; set; }
        public int condition { get; set; }
        public long deviceId { get; set; }
        public long facilityId { get; set; }
        public long factoryId { get; set; }
    }

}
