namespace Bkl.Models.MongoEntity
{
    public class MdAlarmRule : MdEntityBase
    {
        public MdAlarmRule() : base() { }


        //规则组ID 
        public long uniqId { get; set; }


        public long deviceId { get; set; }
        public string deviceName { get; set; }

        public long planId { get; set; }
        public string planName { get; set; }

        public long factoryId { get; set; }
        public string factoryName { get; set; }

        public long facilityId { get; set; } 
        public string facilityName { get; set; }
        /// <summary>
        /// true 所有计划 false 当前计划
        /// </summary>
        public bool forAll { get; set; }
        /// <summary>
        /// true  仅对当前设备 false 对所有设备
        /// </summary>
        public bool forDevice { get; set; }
        /// <summary>
        /// 仅对当前风机
        /// </summary>
        public bool forFacility { get; set; }

        public long ruleId { get; set; }
        public string ruleName { get; set; }
        /// <summary>
        /// 阈值 ai 算法名称  测温 温度值
        /// </summary>
        public string threshold { get; set; }

        /// <summary>
        /// 推送告警级别 一般 严重 紧急 
        /// </summary>
        public string level { get; set; }
        /// <summary>
        /// 推送间隔
        /// </summary>
        public int interval { get; set; }
        /// <summary>
        /// 推送次数
        /// </summary>
        public int count { get; set; }
        public string[] sms { get; set; }
        public string[] email { get; set; }

        /// <summary>
        /// ai 测温 
        /// </summary>
        public string sourceType { get; set; }
        /// <summary>
        /// 预警标题
        /// </summary>
        public string title { get; set; }


        public bool useSMSMsg { get; set; }

        public bool useEmailMsg { get; set; }

        public bool useSysMsg { get; set; }
    }
}
