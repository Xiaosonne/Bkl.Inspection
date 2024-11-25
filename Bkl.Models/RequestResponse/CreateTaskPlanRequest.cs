using Org.BouncyCastle.Asn1.Ocsp;
using System;

namespace Bkl.Models
{
   
    public class CreateTaskPlanRequest
    {
        public string PlanName { get; set; }
        public string PlanType { get; set; }

        public long FactoryId { get; set; }
        public string FactoryName { get; set; }

        public DateTime BeginDate { get; set; }
        public DateTime EndDate { get; set; }
        public int RunInterval { get; set; }
        public int StopInterval { get; set; }
        public int[] RunDaysOfMonth { get; set; }
        public int[] RunDaysOfWeek { get; set; }
        public int[] RunMonthsOfYear { get; set; }


    }
    public record DeviceListItem(string name,long id);
    public class PlanDeviceConfig
    {
        public long FacilityId { get; set; }
        public DeviceListItem[] DeviceList { get; set; }
        public string FacilityName { get; set; } 
        public long AlgorithmId { get; set; }
    }

    public class PlanAlgorithmConfig
    {

        public long Id { get; set; }
        public string Name { get; set; }
        /// <summary>
        /// 检测频率
        /// </summary>
        public int DetectInterval { get; set; }
        /// <summary>
        /// 检测结果级别map 脱落：leve1
        /// </summary>
        public string DetectResultLevel { get; set; }
        /// <summary>
        /// 检测阈值 0.3 0.6
        /// </summary>
        public string DetectResultThreshold { get; set; }
        /// <summary>
        /// 检测名称字典 jwz:绝缘子
        /// </summary>
        public string DetectClassNameMap { get; set; }
        /// <summary>
        /// 检测一次后休眠时间
        /// </summary>
        public int DetectSleep { get; set; }
        /// <summary>
        /// 最大生成结果间隔 10秒一次 之类
        /// </summary>
        public int DetectResultGenerateInterval { get; set; }
        /// <summary>
        /// 两个结果像素距离
        /// </summary>
        public int DetectResultMaxDistance { get; set; }
        /// <summary>
        /// 检测方法 l1 l2
        /// </summary>
        public string DetectResultCaculateDistanceMethod { get; set; }
        /// <summary>
        /// 保存地址 minio http://192.168.10.100
        /// </summary>
        public string SavedPath { get; set; }
        /// <summary>
        /// 保存文件名称format {id}_{taskId}.jpg
        /// </summary>
        public string SavedFormat { get; set; }

    }


    public class CreateTaskPlanDetailRequest
    {
        public PlanDeviceConfig[] DeviceConfigs { get; set; }

        public PlanAlgorithmConfig[] AlgorithmConfigs { get; set; }
        public long PlanId { get; set; }
        public string PlanName { get; set; }
        public long FactoryId { get; set; }
        public string FactoryName { get; set; }
    }

   

}
