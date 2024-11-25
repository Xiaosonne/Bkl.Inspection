using System;
using MongoDB.Driver;
using System.Reflection;

namespace Bkl.Models.MongoEntity
{
    public static class ObjExt
    {
        public static MongoDbSet<T> MdbActive<T>(this IMongoCollection<T> obj) where T : MdEntityBase
        {
            return (MongoDbSet<T>)typeof(MongoDbSet<T>).GetConstructor(new Type[] { typeof(IMongoCollection<T>) }).Invoke(new object[] { obj });
        }
    }

    public class MongoDbContext
    {
        private MongoClient _mongoClient;
        private IMongoDatabase _mongoDatabase;

        [MdCollectionName("AlarmRuleCollection")]
        public MongoDbSet<MdAlarmRule> MdAlarmRule { get; set; }
        
        [MdCollectionName("AlarmRecord")]
        public MongoDbSet<AlarmRecord> AlarmRecord { get; set; }

        [MdCollectionName("AiAlarmStatistic")]
        public MongoDbSet<AiAlarmStatistic> AiAlarmStatistic { get; set; }

        [MdCollectionName("TempRecord")]
        public MongoDbSet<TempRecord> TempRecord { get; set; }

        [MdCollectionName("AiStatistic")]
        public MongoDbSet<TempRecord> AiStatistic { get; set; }
        public MongoDbContext(MdSettings settings)
        {
            _mongoClient = new MongoClient(settings.ConnectionString);
            _mongoDatabase = _mongoClient.GetDatabase(settings.DatabaseName);
            var typedb = typeof(IMongoDatabase);
            foreach (var p in this.GetType().GetProperties())
            {
                var pp = p.GetCustomAttribute<MdCollectionName>();
                if (pp != null)
                {
                    var pgetype = p.PropertyType.GenericTypeArguments[0];

                    var getcollectionMethod = typedb.GetMethod("GetCollection");
                    var method = getcollectionMethod.MakeGenericMethod(pgetype);
                    var obj = method.Invoke(_mongoDatabase, new object[] { pp.Name, default(MongoCollectionSettings) });
                    var t = typeof(IMongoCollection<>).MakeGenericType(pgetype);
                    var data = p.PropertyType.GetConstructor(new Type[] { t }).Invoke(new object[] { obj });
                    p.GetSetMethod().Invoke(this, new object[] { data });
                }
            }
        }
        public IMongoCollection<TCol> GetCollection<TCol>(string collectionName)
        {
            var typedb = typeof(IMongoDatabase);
            var getcollectionMethod = typedb.GetMethod("GetCollection");
            var method = getcollectionMethod.MakeGenericMethod(typeof(TCol));
            var obj = method.Invoke(_mongoDatabase, new object[] { collectionName, default(MongoCollectionSettings) });
            return (IMongoCollection<TCol>)obj;
        }
    }
}
