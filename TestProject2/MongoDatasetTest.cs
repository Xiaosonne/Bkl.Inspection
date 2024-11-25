using Bkl.Models.MongoEntity;
using MongoDB.Driver;
using System;
using Xunit;
using MdSettings = Bkl.Models.MongoEntity.MdSettings;

namespace TestProject2
{
    public class MongoDatasetTest
    {
        //[Fact]
        //public void TestMongodataset()
        //{
        //    MongoDbContext context = new MongoDbContext(new MdSettings
        //    {
        //        ConnectionString = "mongodb://192.168.31.108:27017",
        //        DatabaseName = "test",
        //    });
        //    context.MdTest.CreateAsync(new MdTest { text = "asd", Createtime = DateTime.Now }).GetAwaiter().GetResult();
        //    var lis = context.MdTest.GetAsync().GetAwaiter().GetResult();
        //    Assert.NotEqual(0, lis.Count);
        //}
        [Fact]
        public void TestMongodatasetCollection()
        {
            MongoDbContext context = new MongoDbContext(new MdSettings
            {
                ConnectionString = "mongodb://192.168.31.108:27017",
                DatabaseName = "test",
            });
            var collect = context.GetCollection<Test2Colle>("Test2Collection");
            collect.InsertOne(new Test2Colle { Createtime = DateTime.Now, name = "test method", test = "11111" });
            var aaa = collect.Find(_ => true).ToListAsync().GetAwaiter().GetResult();
            Assert.NotEqual(0, aaa.Count);
        }

        public class Test2Colle : MdEntityBase
        {
            public string name { get; set; }
            public string test { get; set; }
        }
    }
}
