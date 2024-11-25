using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Bkl.Models.RequestResponse.CreateStatisticRequest;

namespace Bkl.Models.MongoEntity
{
    public class ReportData : MdEntityBase
    {


        public List<Dictionary<string, string>> data { get; set; }
        public string Type { set; get; }
    }
    public class ReportCount : MdEntityBase
    {

        public string ZongShu { set; get; }
        public string ErrorClass { set; get; }
        public string ErrorTypeMonth { set; get; }
        public string ErrorTypeWeek { set; get; }
        public string FacilityErrorClass { set; get; }
        public string ErrorCount { set; get; }
        public string FacilityErrorCount { set; get; }
        public string FacilityDatas { set; get; }
        public long PlanId { get; set; }
        public long FactoryId { get; set; }
        public long FacilityId { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }
   
}
