namespace Bkl.Models
{
    public class CreateAlgorithmRequest
    {
        public class ClassMap
        {
            public string Key { get; set; }
            public string Name { get; set; }
            public string Level { get; set; }
            public string Threshold { get; set; }
        }
        public ClassMap[] ClassMaps { get; set; }
        public string Name { get; set; }

        public long FactoryId { get; set; }
        public string FactoryName { get; set; }

        public int DetectSleep { get; set; }
        public int DetectInterval { get; set; }


        public int DetectResultGenerateInterval { get; set; }
        public int DetectResultMaxDistance { get; set; }
        public string DetectResultCaculateDistanceMethod { get; set; }


        public string SavedPath { get; set; }
        public string SavedFormat { get; set; }

        public int InstanceCount { get; set; }
        public int AggragatePort { get; set; }

        public string Description { get; set; }
        public string AlgorithmType { get; set; }
        public string StartCommand { get; set; }
        public string Weights { get; set; }
        public string PortRange { get; set; }
        public string[] GpuIds { get; set; }
        public string StartArgs { get; set; }
        public string Category { get; set; }
        public string DeploymentType { get; set; }
    }


}
