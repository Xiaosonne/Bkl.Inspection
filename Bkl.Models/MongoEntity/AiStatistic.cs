using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bkl.Models.MongoEntity
{
    public class AiStatistic : MdEntityBase
    {
        

        public List<string> nameList { get; set; }
        public List<string> errorList { set; get; }
        public List<string> pointList { set; get; }
         
        public string dailyCount { set; get; }
    }
}
