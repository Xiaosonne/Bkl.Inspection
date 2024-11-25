using Bkl.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Minio;
using MySql.Data.MySqlClient;
using SharpKml.Base;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using StackExchange.Redis;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Yitter.IdGenerator;
using static Bkl.Models.BklConfig;
using static Org.BouncyCastle.Math.EC.ECCurve;

namespace UploadTools
{
    public class HostService : IHostedService
    {
        private string _destBucket;
        private string _uploadDir;
        private string _apiEndPoint;
        private MinioClient _minioClient;
        private string _databaseStr;
        private Task _runningTask;
        private HttpClient _httpClient = new HttpClient();
        private IConfiguration _config;
        const string one = "前缘/背风面";
        const string two = "后缘/背风面";
        const string three = "后缘/迎风面";
        const string four = "前缘/迎风面";
        Dictionary<string, string> dirBladeMap = new Dictionary<string, string>
            {
                { "A1", $"叶片A/{one}" },
                { "A2", $"叶片A/{two}" },
                { "A3", $"叶片A/{three}" },
                { "A4", $"叶片A/{four}" },
                { "B1", $"叶片B/{one}" },
                { "B2", $"叶片B/{two}" },
                { "B3", $"叶片B/{three}" },
                { "B4", $"叶片B/{four}" },
                { "C1", $"叶片C/{one}" },
                { "C2", $"叶片C/{two}" },
                { "C3", $"叶片C/{three}" },
                { "C4", $"叶片C/{four}" },
            };

        long selectFactoryId = 0;
        long selectTaskId = 0;
        Dictionary<string, object> currentFactory = null;
        Dictionary<string, object>[] facilities = null;

        Queue<string> queueGendata = new Queue<string>();
        Channel<string> queueUploadFiles = Channel.CreateUnbounded<string>();

        CommandLineOption _option = null;
        private int _redisPort;
        private string _redisHost;
        private int _redisDb;
        private string _redisAuth;

        public HostService(IConfiguration configuration, CommandLineOption option)
        {
            _config = configuration;
            var min = configuration.GetValue<string>("minio_endpoint");
            var user = configuration.GetValue<string>("minio_user");
            var pswd = configuration.GetValue<string>("minio_pswd");
            var region = configuration.GetValue<string>("minio_region");
            _destBucket = configuration.GetValue<string>("minio_bucket");
            _uploadDir = configuration.GetValue<string>("uploaddir");

            _apiEndPoint = configuration.GetValue<string>("api_endpoint");

            _minioClient = new MinioClient()
                .WithEndpoint(min)
                .WithCredentials(user, pswd)
                .WithRegion(region)
                .Build();

            _databaseStr = configuration.GetValue<string>("database_constr");

            _option = option;

            _redisAuth = configuration.GetValue<string>("redis_auth");
            _redisHost = configuration.GetValue<string>("redis_host");
            _redisDb = configuration.GetValue<int>("redis_db");
            _redisPort = configuration.GetValue<int>("redis_port");
            var SnowConfig = new Snow();


            SnowId.SetIdGenerator(new IdGeneratorOptions
            {
                WorkerId = (ushort)SnowConfig.WorkerId,
                DataCenterId = SnowConfig.DataCenterId,
                DataCenterIdBitLength = SnowConfig.DataCenterIdBitLength,
                WorkerIdBitLength = SnowConfig.WorkerIdBitLength,
                SeqBitLength = SnowConfig.SeqBitLength,
            });


        }
        async Task updateBladeIndexFun(Stream s)
        {
            StreamReader sr = new StreamReader(s);
            var opt = new ConfigurationOptions
            {
                Password = _redisAuth,
                DefaultDatabase = _redisDb,
            };
            opt.EndPoints.Add(_redisHost, _redisPort);
            var con = StackExchange.Redis.ConnectionMultiplexer.Connect(opt);

            var redis = new RedisClient(con);
            List<dynamic> lis = new List<dynamic>();
            while (true)
            {
                var strLine = (await sr.ReadLineAsync());
                if (string.IsNullOrEmpty(strLine))
                    break;
                var line1 = strLine.Split(",");
                lis.Add(new
                {
                    TaskId = long.Parse(line1[0]),
                    FacilityId = long.Parse(line1[1]),
                    BladeIndex = line1[4],
                    StartIndex = int.Parse(line1[5]),
                    EndIndex = int.Parse(line1[6]),
                    OverLap = 80,
                    Except = ""
                });
            }
            foreach (var sametask in lis.GroupBy(q => q.TaskId))
            {
                foreach (var samefaci in lis.GroupBy(s => s.FacilityId))
                {
                    foreach (var item in samefaci)
                    {
                        var da = JsonSerializer.Serialize(new
                        {
                            StartIndex = item.StartIndex,
                            EndIndex = item.EndIndex,
                            OverLap = item.OverLap,
                            Except = item.Except,
                        });
                        redis.SetEntryInHash($"Task.{sametask.Key}:Facility.{samefaci.Key}", item.BladeIndex, da);
                    }

                }
            }
        }

        async Task chooseFactory()
        {
            var resp1 = await _httpClient.GetAsync($"{_apiEndPoint}/management/factories");
            var facsStr = await resp1.Content.ReadAsStringAsync();
            var facs = JsonSerializer.Deserialize<Dictionary<string, object>[]>(await resp1.Content.ReadAsStringAsync());
            Console.WriteLine("选择电厂：");
            for (int i = 0; i < facs.Length; i++)
            {
                var fac = facs[i];
                Console.WriteLine($"{i},{fac["factoryName"]}");
            }
            var choose = Console.ReadLine();
            int id = 0;
            while (false == int.TryParse(choose, out id) || id >= facs.Length)
            {
                Console.WriteLine("请输入正确选项：");
                for (int i = 0; i < facs.Length; i++)
                {
                    var fac = facs[i];
                    Console.WriteLine($"{i},{fac["factoryName"]}");
                }
            }
            currentFactory = facs[id];
            selectFactoryId = (long.Parse(facs[id]["id"].ToString()));
        }

        async Task chooseTask()
        {
            var resp1 = await _httpClient.GetAsync($"{_apiEndPoint}/inspection/list-task?factoryId={selectFactoryId}");
            var facsStr = await resp1.Content.ReadAsStringAsync();
            var facs = JsonSerializer.Deserialize<Dictionary<string, object>[]>(await resp1.Content.ReadAsStringAsync());
            if (facs.Length == 0)
            {
                Console.WriteLine("创建任务：");
                var tname = Console.ReadLine();
                Console.WriteLine("任务描述：");
                var tdesc = Console.ReadLine();

                var resp3 = await _httpClient.PostAsync($"{_apiEndPoint}/inspection/create-task",
                        new StringContent(JsonSerializer.Serialize(new
                        {
                            factoryId = currentFactory["id"].ToString(),
                            factoryName = currentFactory["factoryName"].ToString(),
                            taskName = tname,
                            description = tdesc,
                            taskType = "FJYPXJ"
                        }), Encoding.UTF8, "application/json"));
                Console.WriteLine(resp3.Content.ReadAsStringAsync().Result);
                await chooseTask();
                return;
            }
            else
            {
                Console.WriteLine("选择任务：");
                for (int i = 0; i < facs.Length; i++)
                {
                    var fac = facs[i];
                    Console.WriteLine($"{i},{fac["taskName"]}");
                }
            }
            var choose = Console.ReadLine();
            int id = 0;
            while (false == int.TryParse(choose, out id) || id >= facs.Length)
            {
                Console.WriteLine("请输入正确选项：");
                for (int i = 0; i < facs.Length; i++)
                {
                    var fac = facs[i];
                    Console.WriteLine($"{i},{fac["factoryName"]}");
                }
                choose = Console.ReadLine();
            }
            selectTaskId = (long.Parse(facs[id]["id"].ToString()));
        }

        async Task createFacility()
        {
            var len = _config.GetValue<string>("upload_fan_length");
            var ypfac = _config.GetValue<string>("upload_fan_fac");
            var yptype = _config.GetValue<string>("upload_fan_type");
            Console.WriteLine("叶片长度：" + len);
            Console.WriteLine("叶片厂商：" + ypfac);
            Console.WriteLine("叶片型号：" + yptype);

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("所属电厂\t名称\tKEY-巡检时间\t类型\tKEY-风机型号\tKEY-叶片A编号\tKEY-叶片B编号\tKEY-叶片C编号\tKEY-bucket\tKEY-叶片长度\tKEY-叶片厂商\tKEY-叶片型号");
            foreach (var facilityDir in Directory.GetDirectories(_uploadDir))
            {
                try
                {
                    var arr = Path.GetFileName(facilityDir).Split("_");
                    DateTime time = DateTime.ParseExact(arr[1], "yyyyMMddHHmm", null);
                    string faci = arr[3];
                    sb.AppendLine($"{currentFactory["factoryName"]}\t{faci}\t{time.ToString("yyyy-MM-dd")}\tWindPowerGenerator\t1.5MW\tAA\tBB\tCC\t{_destBucket}\t{len}\t{ypfac}\t{yptype}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine("发生错误 yes 继续？");
                    if (Console.ReadLine() != "yes")
                    {
                        return;
                    }
                }

            }
            Console.WriteLine("风机信息：");
            Console.WriteLine(sb.ToString());
            Console.WriteLine("请输入回车键继续导入：");
            var resp = await _httpClient.PostAsync($"{_apiEndPoint}/management/batch-create-facility", new StringContent(sb.ToString()));
            var respstr = await resp.Content.ReadAsStringAsync();
            Console.WriteLine("服务器返回：" + respstr);
            var data = JsonSerializer.Deserialize<Dictionary<string, object>>(respstr);
            if (data != null && data["error"].ToString() == "0")
            {
                Console.WriteLine("导入成功！！！");
            }
            else
            {
                Console.WriteLine("导入失败！！！");
            }
            await loadFacilities();
        }


        async Task doUpload(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                if (queueUploadFiles.Reader.Count == 0)
                    break;
                var path = await queueUploadFiles.Reader.ReadAsync();
                if (path == null)
                    break;
                FileInfo fi = new FileInfo(path);

                try
                {
                    CancellationTokenSource cts = new CancellationTokenSource();
                    var observable = _minioClient.ListObjectsAsync(new ListObjectsArgs().WithBucket(_destBucket)
                            .WithPrefix(fi.Name));
                    bool exists = false;
                    ManualResetEvent mre = new ManualResetEvent(false);
                    observable.Subscribe(p =>
                    {
                        exists = p.Key == fi.Name;
                    }, () =>
                    {
                        mre.Set();
                    });
                    mre.WaitOne();
                    if (exists)
                    {
                        continue;
                    }

                    using (var img = Image.Load(path))
                    {

                        using MemoryStream ms = new MemoryStream();
                        using MemoryStream mssmall = new MemoryStream();
                        if (img.Width != 4000 || img.Height != 3000)
                        {
                            img.Mutate(ctx => ctx.Resize(4000, 3000));
                        }
                        img.SaveAsJpeg(ms, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 60 });
                        img.Mutate(ctx => ctx.Resize(200, 150));
                        img.SaveAsJpeg(mssmall);
                        ms.Seek(0, SeekOrigin.Begin);
                        mssmall.Seek(0, SeekOrigin.Begin);

                        await _minioClient.UploadStream(ms, fi.Name, _destBucket);
                        await _minioClient.UploadStream(mssmall, fi.Name, _destBucket + "-small");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"WriteFileError {fi.Name}" + ex.ToString());
                    Console.WriteLine($"WriteFileError {fi.Name}" + ex.StackTrace.ToString());
                }
            }
        }

        async Task beginUpload(int tasks, CancellationToken token)
        {
            var allcount = queueUploadFiles.Reader.Count;
            List<Task> lis = new List<Task>();
            for (int i = 0; i < tasks; i++)
            {
                lis.Add(doUpload(token));
            }
            Console.WriteLine("");
            (var left, var top) = Console.GetCursorPosition();
            while (true)
            {
                if (Task.WaitAll(lis.ToArray(), 1000))
                {
                    break;
                }
                if (queueUploadFiles.Reader.Count == 0)
                {
                    break;
                }
                Console.SetCursorPosition(left, top);
                Console.WriteLine($"总计：\t{allcount} ，  剩余：\t  {queueUploadFiles.Reader.Count}\t         ");

            }
        }

        async Task writeDatabase()
        {

            using MemoryStream ms = new MemoryStream();
            int row = 0;
            while (true)
            {
                if (queueGendata.Count == 0)
                    break;
                var str = queueGendata.Dequeue();
                if (str == null)
                    break;
                row++;
                var bts = Encoding.UTF8.GetBytes(str);
                ms.Write(bts, 0, bts.Length);
            }
            ms.Seek(0, SeekOrigin.Begin);
            File.WriteAllBytes($"databaseInsert{DateTime.Now.ToString("yyyyMMddHHmmss")}.txt", ms.ToArray());
            int inserted = 0;
            //Id,TaskId,FactoryId,FactoryName,FacilityId,FacilityName,Position,LocalImagePath,
            //Error,RemoteImagePath,ImageType,ImageWidth,ImageHeight,Createtime
            using (MySqlConnection con = new MySqlConnection(_databaseStr))
            {
                MySqlBulkLoader bulk = new MySqlBulkLoader(con)
                {
                    LineTerminator = "\\n",
                    FieldTerminator = ",",
                    TableName = "bkl_inspection_task_detail",
                    NumberOfLinesToSkip = 0,
                };
                bulk.Columns.AddRange(new List<string> { "Id", "TaskId", "FactoryId", "FactoryName", "FacilityId", "FacilityName", "Position", "LocalImagePath", "Error", "RemoteImagePath", "ImageType", "ImageWidth", "ImageHeight", "Createtime" });
                inserted = bulk.Load(ms);
            }
            if (inserted == row)
            {
                Console.WriteLine($"=================系统录入完成,总计：{row}，插入：{inserted}==============");
            }
            else
            {
                Console.WriteLine($"=================系统录入失败，总计：{row}，插入：{inserted}==============");
            }
        }


        async Task loadImages()
        {
            Dictionary<string, string> imageIdCache = null;
            if (File.Exists($"{selectFactoryId}-{selectTaskId}-imageIdCache.txt"))
            {
                var lines = File.ReadAllLines($"{selectFactoryId}-{selectTaskId}-imageIdCache.txt");
                imageIdCache = lines.Select(s => s.Split('\t')).ToDictionary(k => k[0], k => k[1]);
            }
            else
            {
                imageIdCache = new Dictionary<string, string>();
            }

            StringBuilder pathIndex = new StringBuilder();
            StringBuilder genData = new StringBuilder();
            StringBuilder imageCache = new StringBuilder();
            foreach (var facilityDir in Directory.GetDirectories(_uploadDir))
            {
                string faci = Path.GetFileName(facilityDir).Split("_")[3];
                var facility = facilities.FirstOrDefault(s => s["factoryId"].ToString() == selectFactoryId.ToString() && s["name"].ToString() == faci);
                if (string.IsNullOrEmpty(faci) || facility == null)
                {
                    Console.WriteLine(faci + " null value");
                    continue;
                }
                foreach (var path in Directory.GetDirectories(facilityDir))
                {
                    var ypPath = Path.GetFileName(path);
                    if (!dirBladeMap.ContainsKey(ypPath))
                    {
                        Console.WriteLine(faci + " yp path error " + ypPath);
                        continue;
                    }
                    var bladeIndex = dirBladeMap[ypPath];
                    var files = Directory.GetFiles(path).Where(s => s.EndsWith("JPG")).ToArray();
                    List<Task> lis = new List<Task>();
                    Console.WriteLine($"BeginWriteFile {path} {files.Length}");
                    if (files.Length == 0)
                        continue;
                    try
                    {
                        var arr = files.Select(s => int.Parse(Path.GetFileNameWithoutExtension(s).Split("_")[2])).ToArray();
                        var minIndex = arr.Min();
                        var maxIndex = arr.Max();
                        var shunxu = bladeIndex.Contains("前缘/迎风面") || bladeIndex.Contains("后缘/背风面");
                        var line = $"{selectTaskId},{facility["id"]},{facility["name"]},{ypPath},{bladeIndex},{(shunxu ? minIndex : maxIndex)},{(shunxu ? maxIndex : minIndex)}";
                        pathIndex.AppendLine(line);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        Console.WriteLine($"SyncError {path}" + ex.StackTrace.ToString());
                    }
                    foreach (var filename in files)
                    {
                        FileInfo fi = new FileInfo(filename);
                        var time = DateTime.ParseExact(fi.Name.Split('_')[1], "yyyyMMddHHmmss", null);
                        var id = imageIdCache.TryGetValue(fi.Name, out var imageId) ? imageId : SnowId.NextId().ToString();
                        if (!imageIdCache.ContainsKey(fi.Name))
                            imageIdCache.Add(fi.Name, id);
                        var line = $"{id},{selectTaskId},{selectFactoryId},{facility["factoryName"]},{facility["id"]},{facility["name"]},{dirBladeMap[ypPath]},#,0,{_destBucket}/{fi.Name},image/jpeg,4000,3000,{time.ToString("yyyy-MM-dd HH:mm:ss")}\n";
                        genData.Append(line);
                        imageCache.AppendLine(filename);
                        queueGendata.Enqueue(line);
                        await queueUploadFiles.Writer.WriteAsync(filename);
                    }
                }
            }
            using (FileStream fs = new FileStream($"{selectFactoryId}-{selectTaskId}-imageIdCache.txt", FileMode.OpenOrCreate))
            {
                fs.SetLength(0);
                fs.Flush();
                using StreamWriter sw = new StreamWriter(fs);
                foreach (var item in imageIdCache)
                {
                    sw.WriteLine($"{item.Key}\t{item.Value}");
                }
                sw.Flush();
                fs.Flush();
            }
            File.AppendAllText($"{selectFactoryId}-{selectTaskId}-genData.txt", genData.ToString());
            File.AppendAllText($"{selectFactoryId}-{selectTaskId}-pathIndex.txt", pathIndex.ToString());

            using (MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(pathIndex.ToString())))
            {
                ms.Seek(0, SeekOrigin.Begin);
                await updateBladeIndexFun(ms);
            }
        }


        private async Task doUploadProcced(CancellationToken cancellationToken)
        {

            var resp = await _httpClient.PostAsync($"{_apiEndPoint}/management/getToken", new StringContent(JsonSerializer.Serialize(new { account = "admin", password = "123456" }), Encoding.UTF8, "application/json")); ;
            var token = JsonSerializer.Deserialize<Dictionary<string, string>>(await resp.Content.ReadAsStringAsync());
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token["accessToken"]);



            await _minioClient.CreateBucket(_destBucket);
            await _minioClient.CreateBucket(_destBucket + "-small");

            await chooseFactory();




            Console.WriteLine("===========================电厂选择成功=================================");
            Console.WriteLine("继续？");
            Console.ReadLine();

            await chooseTask();
            Console.WriteLine("===========================任务选择成功=================================");
            Console.WriteLine("继续？");
            Console.ReadLine();

            await createFacility();
            Console.WriteLine("===========================风机创建成功=================================");
            Console.WriteLine("继续？");
            Console.ReadLine();



            if (_option.type == "updateBladeIndex")
            {
                while (true)
                {
                    var arrs = Directory.GetFiles(Directory.GetCurrentDirectory(), "*pathIndex*");
                    int f1 = 0;
                    foreach (var arr in arrs)
                    {
                        Console.WriteLine($"选择：{f1++},{arr}");
                    }
                    var str = Console.ReadLine();
                    if (str == "q")
                    {
                        return;
                    }
                    var ind = int.Parse(str);
                    using (var fs = File.Open(arrs[ind], FileMode.Open))
                    {
                        await updateBladeIndexFun(fs);
                        Console.WriteLine("叶片索引更新成功,结束请按q");
                    }
                }
            }

            await loadImages();

            Console.WriteLine("===========================照片载入成功=================================");
            Console.WriteLine("继续？");
            Console.ReadLine();
            CancellationTokenSource cts = new CancellationTokenSource();

            await beginUpload(_config.GetValue<int>("upload_parrallel"), cts.Token);

            Console.WriteLine("===========================照片上传成功=================================");
            Console.WriteLine("继续？");
            Console.ReadLine();



            await writeDatabase();
        }

        private async Task loadFacilities()
        {
            var faciresp = await _httpClient.GetAsync($"{_apiEndPoint}/management/facilities?factoryId={selectFactoryId}");
            var str111 = await faciresp.Content.ReadAsStringAsync();
            facilities = JsonSerializer.Deserialize<Dictionary<string, object>[]>(str111);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {

            _runningTask = doUploadProcced(cancellationToken);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}