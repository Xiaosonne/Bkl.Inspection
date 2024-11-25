namespace Bkl.Models.MongoEntity
{
    public class AlarmNotificationRuleRequest
    {
        public long deviceId { get; set; }
        public long planId { get; set; }
        public long factoryId { get; set; }
        public long facilityId { get; set; }


        public string deviceName { get; set; }
        public string planName { get; set; }
        public string factoryName { get; set; }
        public string facilityName { get; set; }


        public bool forAll { get; set; }
        public bool forDevice { get; set; }
        public bool forFacility { get; set; }


        public long ruleId { get; set; }
        public string ruleName { get; set; }
        public string threshold { get; set; }
        public string level { get; set; }
        public int interval { get; set; }
        public int count { get; set; }
        public string[] sms { get; set; }
        public string[] email { get; set; }

        //ai算法 还是 测温  还是其它
        public string sourceType { get; set; }
        public string title { get; set; }


        public bool useSMSMsg { get; set; }

        public bool useEmailMsg { get; set; }

        public bool useSysMsg { get; set; }


    }
}
