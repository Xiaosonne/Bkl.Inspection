using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bkl.Models.MongoEntity
{
    public class AlgorithmManagement : MdEntityBase
    {

    }
    public class FaceAlgorithm : MdEntityBase
    {
        public string UserCode { get; set; }
        public string Name { set; get; }
        public string ImageUrl { set; get; }
        public string ImageId { set; get; }
        public string Department { set; get; }
        public string FeatureId { get; set; }
        public string Feature { get; set; }
        public long FaceId { get; set; }
        //public string Group { set; get; }
        //public string Grade { set; get; }
    }

}
