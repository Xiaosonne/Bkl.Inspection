
using System;
using System.Collections.Generic;

namespace Bkl.Models.MongoEntity
{
    public class TempRecord : MdEntityBase
    {
        public TempRecord() : base() { }
        public long deviceId { get; set; }
        public int ruleId { get; set; }
        public string ruleName { get; set; }
        public float minTemperature { get; set; }
        public float maxTemperature { get; set; }
        public float averageTemperature { get; set; }
        public DateTime beginTime { get; set; }
        public DateTime endTime { get; set; }
        public string deviceName { get; set; }
        public long facilityId { get; set; }
        public string facilityName { get; set; }

        public long factoryId { get; set; }
        public string factoryName { get; set; }
    }
    public class AiAlarmStatistic : MdEntityBase
    {
        public AiAlarmStatistic() : base()
        {

        }
        public long FacilityId { get; set; }
        public long FactoryId { get; set; }
        public long PlanId { get; set; }
        public long DeviceId { get; set; }
        public int YearMonthDay { get; set; }
        public long AlgorithmId { get; set; }
        
        public Dictionary<string, HourCount[]> HoursCount { get; set; }
    }
    public class HourCount
    {
        public int Hour { get; set; }
        public int Count { get; set; }
    }
}
