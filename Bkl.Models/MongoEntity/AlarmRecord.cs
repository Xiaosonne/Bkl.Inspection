using Bkl.Models.MongoEntity;
using System;
using System.Collections.Generic;

namespace Bkl.Models.MongoEntity
{
    public class AlarmRecord:MdEntityBase
    {
        public AlarmRecord() : base()
        {

        }
        public string Title { get; set; } 
        public int AlarmCount { get; set; }
        public long FacilityId { get; set; }
        public string FacilityName { get; set; }
        public long FactoryId { get; set; }
        public string FactoryName{get;set;}
        public long PlanId { get; set; }
        public string PlanName{get;set;}
        public long DeviceId { get; set; }
        public string DeviceName{get;set;}
        public long YYYYMMDDHHMM { get; set; }

        /// <summary>
        /// 算法id.label name 
        /// 测温规则.温度类型
        /// </summary>
        public Dictionary<string,string> RecordMetadata { get; set; }
        public string AlarmKind { get;  set; }
        public long BundleId { get;  set; }
        public DateTime TimeEnd { get;  set; }
        public DateTime TimeBegin { get;  set; }
    }
}
