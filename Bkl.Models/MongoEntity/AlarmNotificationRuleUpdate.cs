namespace Bkl.Models.MongoEntity
{
    public class AlarmNotificationRuleUpdate
    {
        public string mongoId { get; set; }

        public bool smsMsg { get; set; }

        public bool emailMsg { get; set; }

        public bool sysMsg { get; set; }


    }
}
