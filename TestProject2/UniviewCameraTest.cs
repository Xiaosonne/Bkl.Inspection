using System.Net.Http;
using System.Net;
using Xunit;
using Xunit.Abstractions;
using System.IO;
using System;
using Bkl.Infrastructure;
using System.Threading.Tasks;

namespace TestProject2
{
    public class UniviewCameraTest
    {
        private ITestOutputHelper _log;

        public UniviewCameraTest(ITestOutputHelper console)
        {
            _log = console;
        }
        [Fact]
        public async void TestCameraAuth()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                Credentials = new NetworkCredential("admin", "admin123456")
            });
            var resp = await client.GetAsync("http://192.168.31.80/LAPI/V1.0/System/Time");
            Assert.Equal(resp.StatusCode.ToString(), HttpStatusCode.OK.ToString());
            _log.WriteLine(resp.ToString());
            _log.WriteLine(resp.Content.ReadAsStringAsync().Result);
        }

        [Fact]
        public async void TestCamerSnap()
        {
            var client = new HttpClient(new HttpClientHandler
            {
                Credentials = new NetworkCredential("admin", "Hd123456...")
            });
            client.Timeout = TimeSpan.FromSeconds(3);
            var now = DateTime.Now;
            while (DateTime.Now.Subtract(now).TotalSeconds <= 10)
            {
                var fname = DateTime.Now.ToString("yyyyMMddHHmmss");
                var resp = await client.GetAsync($"http://192.168.31.173:7243/LAPI/V1.0/Channels/{0}/Media/Video/Streams/{0}/Snapshot");
                Assert.Equal(resp.StatusCode.ToString(), HttpStatusCode.OK.ToString());
                var imgpic = await resp.Content.ReadAsByteArrayAsync();
                _log.WriteLine(DateTime.Now.ToString() + Directory.GetCurrentDirectory());
                System.IO.File.WriteAllBytes($"D:/deploy/test/{fname}.jpg", imgpic);
                await Task.Delay(1000);
            }

        }


        [Fact]
        public void TestCamerSnap2()
        {
            UniviewHelper helper = new UniviewHelper("192.168.31.173", 7161, "admin", "bkl666666");
            var bts = helper.Snapshop(1, 0);
            Assert.NotNull(bts);
        }
        [Fact]
        public async void TestCamerTalkUrl()
        {
            //UniviewHelper helper = new UniviewHelper("192.168.31.173", 7161, "admin", "bkl666666");
            UniviewHelper helper = new UniviewHelper("192.168.31.74", 80, "admin", "admin123456");
            var bts = await helper.GetTalkUrl();
            _log.WriteLine(bts.Response.Data);
            Assert.NotNull(bts);
        }

    }
}
