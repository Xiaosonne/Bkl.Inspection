using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Dynamic;
using System.Linq.Expressions;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using DocumentFormat.OpenXml.Math;
using Org.BouncyCastle.Asn1.Tsp;

namespace Bkl.Inspection
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class InspectionController : Controller
    {
        private IServiceProvider serviceProvider;
        BklDbContext context;
        private ILogger<InspectionController> logger;
        private IBackgroundTaskQueue<GenerateAllTaskRequest> taskQueue;

        public InspectionController(BklDbContext context, IServiceProvider serviceProvider,
         IBackgroundTaskQueue<GenerateAllTaskRequest> taskqueue,
         IBackgroundTaskQueue<DetectTaskInfo> taskInfoQueue,
         ILogger<InspectionController> logger)
        {
            this.serviceProvider = serviceProvider;
            this.context = context;
            this.logger = logger;
            this.taskQueue = taskqueue;
        }




        [HttpGet("reverse-blade-info")]
        public IActionResult ReverseOrder(
            [FromServices] IRedisClient redis,
            [FromQuery] long taskId,
            [FromQuery] long facilityId,
            [FromQuery] string position
        )
        {
            var dic = redis.GetValuesFromHash($"Task.{taskId}:Facility.{facilityId}");
            Dictionary<string, BladeExtraInfo> dic2 = new Dictionary<string, BladeExtraInfo>();
            foreach (var kv in dic)
            {
                dic2.Add(kv.Key, JsonSerializer.Deserialize<BladeExtraInfo>(kv.Value));
            }
            var posData = JsonSerializer.Deserialize<BladeExtraInfo>(dic[position]);
            posData = new BladeExtraInfo
            {
                StartIndex = posData.EndIndex,
                EndIndex = posData.StartIndex,
                Except = posData.Except,
                OverLap = posData.OverLap
            };
            redis.SetEntryInHash(
                $"Task.{taskId}:Facility.{facilityId}",
                position,
                JsonSerializer.Serialize(posData)
            );
            dic = redis.GetValuesFromHash($"Task.{taskId}:Facility.{facilityId}");
            dic2 = new Dictionary<string, BladeExtraInfo>();
            foreach (var kv in dic)
            {
                dic2.Add(kv.Key, JsonSerializer.Deserialize<BladeExtraInfo>(kv.Value));
            }
            return new JsonResult(dic2);
        }
        [AllowAnonymous]
        [HttpPost("update-blade-index")]
        public async Task<IActionResult> SetBladeExtraInfo(
                    [FromServices] LogonUser user,
                    [FromServices] IRedisClient redis,
                    [FromQuery] string splitter = ","
                )
        {
            StreamReader sr = new StreamReader(this.Request.Body);
            var headers = (await sr.ReadLineAsync()).Split(splitter);
            List<SetBladeRequest> lis = new List<SetBladeRequest>();
            while (true)
            {
                var strLine = (await sr.ReadLineAsync());
                if (string.IsNullOrEmpty(strLine))
                    break;
                var line1 = strLine.Split(splitter);
                lis.Add(new SetBladeRequest
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
                        var da = JsonSerializer.Serialize(new BladeExtraInfo
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
            return new JsonResult("ok");
        }

        [HttpPost("set-blade-extra-info")]
        public IActionResult SetBladeExtraInfo(
            [FromServices] LogonUser user,
            [FromServices] IRedisClient redis,
            [FromBody] SetBladeRequest request
        )
        {
            redis.SetEntryInHash(
                $"Task.{request.TaskId}:Facility.{request.FacilityId}",
                request.BladeIndex,
                JsonSerializer.Serialize(
                    new
                    {
                        StartIndex = request.StartIndex,
                        EndIndex = request.EndIndex,
                        OverLap = request.OverLap,
                        Except = request.Except,
                    }
                )
            );
            return new JsonResult(request);
        }

        [HttpPost("get-blade-extra-info")]
        public IActionResult GetBladeExtraInfo(
            [FromServices] LogonUser user,
            [FromServices] IRedisClient redis,
            [FromQuery] long taskId,
            [FromQuery] long facilityId
        )
        {
            var dic = redis.GetValuesFromHash($"Task.{taskId}:Facility.{facilityId}");
            var dicmeta = redis.GetValuesFromHash($"FacilityMeta:{facilityId}");
            Dictionary<string, object> dic2 = new Dictionary<string, object>();
            foreach (var kv in dic)
            {
                dic2.Add(kv.Key, JsonSerializer.Deserialize<BladeExtraInfo>(kv.Value));
            }
            foreach (var kv in dicmeta)
            {
                if (!dic2.ContainsKey(kv.Key))
                    dic2.Add(kv.Key, kv.Value.ToString());
            }
            return new JsonResult(dic2);
        }


        [AllowAnonymous]
        [HttpGet("sync_meta_info")]
        public IActionResult SyncMetaInfo(
            [FromServices] IRedisClient redis,
            [FromQuery] long factoryId,
            [FromQuery] string bucket,
            [FromQuery] string mainPath = @"D:\BaiduYunDownload\FJ-luoyangyiyangchuancheng\good"
        )
        {
            var fas = context.BklFactoryFacility.Where(s => s.FactoryId == factoryId).ToList();
            foreach (var fa in fas)
            {
                context.BklInspectionTaskDetail.Where(s => s.FacilityId == fa.Id).FirstOrDefault();
            }
            foreach (var facilityDir in Directory.GetDirectories(mainPath))
            {
                var filename = Path.GetFileName(facilityDir);
                var arr = filename.Split("_");
                var time = DateTime.ParseExact(arr[1], "yyyyMMddHHmm", null);
                var fa = fas.FirstOrDefault(s => s.Name == arr[3]);
                if (fa != null)
                {
                    redis.SetEntryInHash(
                        $"FacilityMeta:{fa.Id}",
                        "巡检时间",
                        time.ToString("yyyy年MM月dd日")
                    );
                    redis.SetEntryInHash($"FacilityMeta:{fa.Id}", "bucket", bucket);
                }
            }
            return Json("");
        }

        // [AllowAnonymous]
        // [HttpPost("sync_meta_info")]
        // public async IActionResult SyncPostMetaInfo([FromServices] IRedisClient redis)
        // {
        //     var result = await this.Request.BodyReader.ReadAsync();
        //     StreamReader st=new StreamReader(new MemoryStream(Encoding.UTF8.GetString()));
        // }

        [AllowAnonymous]
        [HttpGet("sync_folder_pics")]
        public IActionResult SyncFolderPic(
            [FromServices] BklConfig config,
            [FromServices] IRedisClient redis,
            [FromQuery] string bucket = "xj-liangfengao",
            [FromQuery] string rootDir = @"D:\BaiduYunDownload\liangfengao\good2",
            [FromQuery] long factoryId = 6,
            [FromQuery] long taskId = 5,
            [FromQuery] string one = "前缘/背风面",
            [FromQuery] string two = "后缘/背风面",
            [FromQuery] string three = "后缘/迎风面",
            [FromQuery] string four = "前缘/迎风面"
        )
        {
            // Dictionary<string, string> pathMap = new Dictionary<string, string>{
            // 		{"C-q-y","叶片C/前缘/迎风面"},
            // 		{"C-h-y","叶片C/后缘/迎风面"},
            // 		{"C-h-b","叶片C/后缘/背风面"},
            // 		{"C-q-b","叶片C/前缘/背风面"},
            // 		{"A-q-y","叶片A/前缘/迎风面"},
            // 		{"A-h-y","叶片A/后缘/迎风面"},
            // 		{"A-h-b","叶片A/后缘/背风面"},
            // 		{"A-q-b","叶片A/前缘/背风面"},
            // 		{"B-q-y","叶片B/前缘/迎风面"},
            // 		{"B-h-y","叶片B/后缘/迎风面"},
            // 		{"B-h-b","叶片B/后缘/背风面"},
            // 		{"B-q-b","叶片B/前缘/背风面"},
            // };

            // Dictionary<string, string> pathMap = new Dictionary<string, string>{
            //         {"1","叶片C/前缘/迎风面"},
            //         {"2","叶片C/后缘/迎风面"},
            //         {"3","叶片C/后缘/背风面"},
            //         {"4","叶片C/前缘/背风面"},
            //         {"5","叶片A/前缘/迎风面"},
            //         {"6","叶片A/后缘/迎风面"},
            //         {"7","叶片A/后缘/背风面"},
            //         {"8","叶片A/前缘/背风面"},
            //         {"9","叶片B/前缘/迎风面"},
            //         {"10","叶片B/后缘/迎风面"},
            //         {"11","叶片B/后缘/背风面"},
            //         {"12","叶片B/前缘/背风面"},
            // };
            Dictionary<string, string> pathMap = new Dictionary<string, string>
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

            string mainPath = rootDir;
            var pt = System.IO.Path.Combine(config.FileBasePath, "SmallPics");
            if (!Directory.Exists(pt))
            {
                Directory.CreateDirectory(pt);
            }

            var task = context.BklInspectionTask.Where(s => s.Id == taskId);

            ConcurrentQueue<string> queueGendata =
                new System.Collections.Concurrent.ConcurrentQueue<string>();
            ConcurrentQueue<string> queuePathIndex =
                new System.Collections.Concurrent.ConcurrentQueue<string>();
            var minioDir = Path.Combine(config.MinioDataPath, bucket);
            if (!Directory.Exists(minioDir))
            {
                Directory.CreateDirectory(minioDir);
            }
            var minioThumbDir = Path.Combine(config.MinioDataPath, bucket + "-small");
            if (!Directory.Exists(minioThumbDir))
            {
                Directory.CreateDirectory(minioThumbDir);
            }
            foreach (var facilityDir in Directory.GetDirectories(mainPath))
            {
                string faci = Path.GetFileName(facilityDir).Split("_")[3];
                BklFactoryFacility facility = context.BklFactoryFacility
                    .FirstOrDefault(s => s.FactoryId == factoryId && s.Name == faci);
                if (string.IsNullOrEmpty(faci) || facility == null)
                    continue;
                foreach (var path in Directory.GetDirectories(facilityDir))
                {
                    var ypPath = Path.GetFileName(path);
                    if (!pathMap.ContainsKey(ypPath))
                        continue;
                    var bladeIndex = pathMap[ypPath];
                    var files = Directory.GetFiles(path);
                    List<Task> lis = new List<Task>();
                    Console.WriteLine($"BeginWriteFile {path} {files.Length}");
                    try
                    {
                        var arr = files.Select(s => int.Parse(Path.GetFileNameWithoutExtension(s).Split("_")[2])).ToArray();
                        var minIndex = arr.Min();
                        var maxIndex = arr.Max();
                        var shunxu = bladeIndex.Contains("前缘/迎风面") || bladeIndex.Contains("后缘/背风面");
                        redis.SetEntryInHash($"Task.{taskId}:Facility.{facility.Id}", bladeIndex, JsonSerializer.Serialize(
                                new
                                {
                                    StartIndex = shunxu ? minIndex : maxIndex,
                                    EndIndex = shunxu ? maxIndex : minIndex,
                                    OverLap = 80,
                                    Except = "",
                                }
                            )
                        );
                        queuePathIndex.Enqueue($"{SnowId.NextId()},{taskId},{facility.Id},{facility.Name},{ypPath},{bladeIndex},{(shunxu ? minIndex : maxIndex)},{(shunxu ? maxIndex : minIndex)}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                        Console.WriteLine($"SyncError {path}" + ex.StackTrace.ToString());
                    }

                    Parallel.For(0, files.Length, (index, state) =>
                    {
                        var filename = files[index];
                        FileInfo fi = new FileInfo(filename);
                        var smallfile = Path.Combine(minioDir, fi.Name);
                        var smallThumbfile = Path.Combine(minioThumbDir, fi.Name);
                        if (!System.IO.File.Exists(smallfile) && !System.IO.File.Exists(smallThumbfile))
                        {
                            try
                            {
                                using (var img = Image.Load(filename))
                                {
                                    img.SaveAsJpeg(smallfile, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = 60 });
                                    img.Mutate(ctx => ctx.Resize(200, 150));
                                    img.SaveAsJpeg(smallThumbfile);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"WriteFileError {fi.Name}" + ex.ToString());
                                Console.WriteLine($"WriteFileError {fi.Name}" + ex.StackTrace.ToString());
                            }
                        }
                        queueGendata.Enqueue($"{SnowId.NextId()},{taskId},{factoryId},{facility.FactoryName},{facility.Id},{facility.Name},{pathMap[ypPath]},#,0,{bucket}/{fi.Name},image/jpeg,4000,3000,2022-06-13 00:00:00");
                    });
                    Console.WriteLine($"WriteFileSuccess  {path} {files.Length}");
                }
            }
            //6066
            Console.WriteLine("WriteGenData  ");

            try
            {
                using (FileStream fs = new FileStream(Path.Combine(config.FileBasePath, $"{taskId}-{DateTime.Now.ToString("yyyy-MM-dd-HHmmss")}-gendata.txt"), FileMode.OpenOrCreate))
                {
                    StreamWriter sw = new StreamWriter(fs);
                    sw.WriteLine("Id,TaskId,FactoryId,FactoryName,FacilityId,FacilityName,Position,LocalImagePath,Error,RemoteImagePath,ImageType,ImageWidth,ImageHeight,Createtime");
                    while (queueGendata.TryDequeue(out var data))
                    {
                        sw.WriteLine(data);
                    }
                    sw.Flush();
                    fs.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine($"SyncWriteError " + ex.StackTrace.ToString());
            }
            try
            {
                using (FileStream fs = new FileStream(Path.Combine(config.FileBasePath, $"{taskId}-{DateTime.Now.ToString("yyyy-MM-dd-HHmmss")}-pathIndex.txt"), FileMode.OpenOrCreate))
                {
                    StreamWriter sw = new StreamWriter(fs);
                    while (queuePathIndex.TryDequeue(out var data))
                    {
                        sw.WriteLine(data);
                    }
                    sw.Flush();
                    fs.Flush();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                Console.WriteLine($"SyncWriteError " + ex.StackTrace.ToString());
            }

            return Json("asd");
        }


        [HttpGet("get-detect-progress")]
        public IActionResult GetDetectProgress([FromServices] BklConfig config,
            [FromServices] BklDbContext context,
            [FromServices] IRedisClient redis,
            long facilityId,
            long taskId)
        {
            var strDetect = redis.GetValueFromHash($"DetectTask:Tid.{taskId}", facilityId.ToString());
            var strSeg = redis.GetValueFromHash($"SegTask:Tid.{taskId}", facilityId.ToString());
            var detect = ((string)strDetect).Empty() ? new DetectTaskInfo() : JsonSerializer.Deserialize<DetectTaskInfo>(strDetect);
            var seg = ((string)strSeg).Empty() ? new SegTaskInfo() : JsonSerializer.Deserialize<SegTaskInfo>(strSeg);
            var fuse1 = redis.GetValuesFromHash($"FuseTaskResult:Tid.{taskId}.Faid.{facilityId}")
                .ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<FuseImagesResponse>(s.Value));
            var fuse = fuse1.Select(s => new { path = s.Key, result = s.Value.save_path })
               .ToList();
            var detect1 = new TaskProgressItem(detect);

            var seg1 = new TaskProgressItem(seg);

            var fuse11 = new TaskProgressItem(fuse1.Values.ToList()) { data = fuse };

            return Json(new
            {
                detect = detect1,
                seg = seg1,
                fuse = fuse11,
                totalPercent = (0.3 * double.Parse(detect1.percent) + 0.3 * double.Parse(seg1.percent) + 0.4 * double.Parse(fuse11.percent)).ToString("F2")
            });
        }
        [HttpGet("facility-task-result")]
        public IActionResult GetFacilityTaskResult(
            [FromServices] LogonUser user,
            [FromQuery] long taskId,
            [FromQuery] long facilityId,
            [FromQuery] long factoryId
        )
        {
            var where = context.BklInspectionTaskResult.Where(
                p => p.FacilityId == facilityId && p.FactoryId == factoryId && p.TaskId == taskId
            );

            var list = where.ToList();
            var ids = list.Select(s => s.TaskDetailId).ToArray();

            var piclIst = context.BklInspectionTaskDetail.Where(s => ids.Contains(s.Id))
                .Select(s => new { id = s.Id, path = s.RemoteImagePath })
                .ToList();
            var rets = list.Select(s =>
            {
                dynamic doo = new ExtensionDynamicObject(s);
                doo.path = piclIst.FirstOrDefault(q => q.id == s.TaskDetailId)?.path;
                return doo;
            });
            var content = JsonSerializer.Serialize(rets, new JsonSerializerOptions
            {
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            return Content(content, "application/json");
        }
        [HttpPost("autogen-save")]
        public async Task<IActionResult> SaveAutoGenReport(
            [FromServices] LogonUser user,
                 [FromServices] IRedisClient redis,
            [FromQuery] long taskId,
            [FromQuery] long facilityId,
            [FromQuery] long factoryId
        )
        {
            var result = await this.Request.BodyReader.ReadAsync();
            var str = Encoding.UTF8.GetString(result.Buffer);
            BklInspectionTaskResult[] createRequest = JsonSerializer.Deserialize<BklInspectionTaskResult[]>(str, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            try
            {
                var lis = context.BklInspectionTaskDetail.Where(s => s.FactoryId == factoryId && s.FacilityId == facilityId && s.TaskId == taskId).ToList();
                foreach (var item in createRequest)
                {
                    var detail = lis.First(s => s.RemoteImagePath == item.DamageDescription);
                    item.FacilityId = facilityId;
                    item.FacilityName = detail.FacilityName;
                    item.FactoryId = factoryId;
                    item.FactoryName = detail.FactoryName;
                    item.TaskDetailId = detail.Id;
                    item.TaskId = taskId;
                    item.DamageDescription = item.DamageType + "," + item.DamageDescription;
                    item.TreatmentSuggestion = "#";
                    item.DamageLevel = "#";
                }
                context.BklInspectionTaskResult.AddRange(createRequest);
                context.SaveChanges();
                return Json(new GeneralResponse { error = 0, msg = "保存成功" });

            }
            catch (Exception ex)
            {
                return Json(new GeneralResponse { error = 1, msg = "发生错误" + ex.ToString() });
            }
        }
        [HttpGet("autogen-task-result")]
        public IActionResult GetFacilityTaskResult(
        [FromServices] LogonUser user,
             [FromServices] IRedisClient redis,
        [FromQuery] long taskId,
        [FromQuery] long facilityId,
        [FromQuery] long factoryId,
        [FromQuery] string position = null
    )
        {
            bool isnull = position.Empty();
            var results = (from task in context.BklInspectionTaskResult
                           join detail in context.BklInspectionTaskDetail on task.TaskDetailId equals detail.Id
                           where task.TaskId == taskId
                           && (facilityId == 0 || task.FacilityId == facilityId)
                           && task.FactoryId == factoryId
                           && (isnull || task.Position == position)
                           select new { task, detail })
                         .ToList()
                         .Select(s =>
                         {
                             s.task.DamageDescription = s.detail.RemoteImagePath;
                             return s.task;
                         })
                    .ToList();
            bool cansave = results.Count == 0;
            if (results.Count == 0)
            {
                results = InspectionHelper.GetResult(redis, taskId, facilityId, factoryId).ToList();
            }
            var errors = redis.GetValuesFromHash($"FuseTaskResult:Tid.{taskId}.Faid.{facilityId}")
                .Select(s => new { facilityId = facilityId, title = s.Key, path = JsonSerializer.Deserialize<FuseImagesResponse>((string)s.Value).save_path });
            List<object> fuzeResult = new List<object>();

            var content = JsonSerializer.Serialize(new DataResponse<object>
            {
                error = 0,
                msg = "ok",
                data = new
                {
                    longPics = errors,
                    errors = results,
                    needSave = cansave
                }
            }, new JsonSerializerOptions
            {
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
            return Content(content, "application/json");
        }
        [HttpGet("start-{dtype}-task")]
        public async Task<IActionResult> StartDetectTask(
            [FromServices] BklConfig config,
            [FromServices] BklDbContext context,
            [FromServices] IRedisClient redis,

            long facilityId,
            long taskId,
               [FromRoute] string dtype = "detect")
        {
            IBackgroundQueueQueue taskInfoQueue = serviceProvider.GetService<IBackgroundTaskQueue<DetectTaskInfo>>();
            string type = "Detect";
            if (dtype == "detect")
            {
                type = "Detect";
            }
            if (dtype == "seg")
            {
                type = "Seg";
                taskInfoQueue = serviceProvider.GetService<IBackgroundTaskQueue<SegTaskInfo>>();
            }
            if (dtype == "fuse")
            {
                type = "Fuse";
                taskInfoQueue = serviceProvider.GetService<IBackgroundTaskQueue<FuseTaskInfo>>();
            }
            var allValues = redis.GetValuesFromHash($"{type}Task:Tid.{taskId}");
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);

            var details = context.BklInspectionTaskDetail
              .Where(s => s.TaskId == taskId && (facilityId == 0 || s.FacilityId == facilityId))
              .GroupBy(s => s.FacilityId)
              .Select(s => new { faid = s.Key, count = s.Count() })
              .ToList()
              .Select(s => new DetectTaskInfo { Total = s.count, FacilityId = s.faid, TaskId = taskId })
              .ToList();
            Dictionary<string, RedisValue> taskDictInfo = new Dictionary<string, RedisValue>();
            StringBuilder sb = new StringBuilder();
            foreach (var item in details)
            {
                if (redis.SetEntryInHashIfNotExists($"{type}Task:Tid.{taskId}", item.FacilityId.ToString(), JsonSerializer.Serialize(item)))
                {
                    await taskInfoQueue.EnqueueAsync(item);
                    logger.LogInformation($"StartDetectTask {type} {taskId} {item.FacilityId}");
                }
                else
                {
                    var jsonStr = redis.GetValueFromHash($"{type}Task:Tid.{taskId}", item.FacilityId.ToString());
                    var oldTask = JsonSerializer.Deserialize<DetectTaskInfo>(jsonStr);
                    if (DateTime.Now.Subtract(oldTask.LastTime).TotalMinutes > 5)
                    {
                        oldTask.LastTime = DateTime.Now;
                        redis.Remove($"DetectTaskResult:Tid.{taskId}.Faid.{item.FacilityId}");
                        redis.SetEntryInHash($"{type}Task:Tid.{taskId}", item.FacilityId.ToString(), JsonSerializer.Serialize(oldTask));
                        await taskInfoQueue.EnqueueAsync(item);
                        logger.LogInformation($"ReStartDetectTask {type} {taskId} {item.FacilityId}");
                    }
                    else
                    {
                        sb.Append($"{item.FacilityId} 正在运行,请{(5 - DateTime.Now.Subtract(oldTask.LastTime).TotalMinutes).ToString("F2")}分钟后再试。");
                    }
                }
            }
            if (sb.Length == 0)
                return Json(new GeneralResponse { error = 0, success = true });
            return Json(new GeneralResponse { error = 1, msg = sb.ToString() });
        }


        [HttpGet("task-result")]
        public IActionResult GetTaskResult(
            [FromServices] LogonUser user,
            [FromQuery] long taskId,
            [FromQuery] long taskDetailId = 0
)
        {
            var where = context.BklInspectionTaskResult.Where(p => p.TaskId == taskId);
            if (taskDetailId > 0)
            {
                where = where.Where(s => s.TaskDetailId == taskDetailId);
            }
            var list = where.ToList();
            return Json(list);
        }

        [HttpDelete("delete-task-result")]
        public async Task<IActionResult> DeleteResult(long id)
        {
            var item = context.BklInspectionTaskResult.FirstOrDefault(s => s.Id == id);
            var detail = context.BklInspectionTaskDetail.FirstOrDefault(
                s => s.Id == item.TaskDetailId
            );
            item.Deleted = true;
            detail.Error -= 1;
            context.Remove(item);
            await context.SaveChangesAsync();
            return Json(item);
        }

        [HttpPost("edit-task-result")]
        public async Task<IActionResult> EditTaskResult(
            [FromServices] LogonUser user,
            [FromBody] CreateTaskResultRequest request
        )
        {
            var detail = context.BklInspectionTaskDetail.FirstOrDefault(
                q => q.Id == request.TaskDetailId
            );
            var ids = request.TaskResults.Select(q => q.Id).ToArray();
            var models = context.BklInspectionTaskResult.Where(s => ids.Contains(s.Id)).ToList();
            foreach (var item in models)
            {
                var p = request.TaskResults.FirstOrDefault(s => s.Id == item.Id);
                item.TreatmentSuggestion = p.TreatmentSuggestion;
                item.Position = detail.Position;
                item.DamageSize = p.DamageSize;
                item.DamagePosition = p.DamagePosition;
                item.DamageDescription = p.DamageDescription;
                item.DamageLevel = p.DamageLevel;
                item.DamageType = p.DamageType;
                item.DamageX = p.X.ToString();
                item.DamageY = p.Y.ToString();
                item.DamageHeight = p.Height.ToString();
                item.DamageWidth = p.Width.ToString();
            }
            await context.SaveChangesAsync();
            return Json(models);
        }

        [HttpPost("create-task-result")]
        public async Task<IActionResult> CreateTaskResult(
            [FromServices] LogonUser user,
            [FromBody] CreateTaskResultRequest request
        )
        {
            var detail = context.BklInspectionTaskDetail.FirstOrDefault(
                q => q.Id == request.TaskDetailId
            );
            var inserts = request.TaskResults
                .Select(
                    p =>
                        new BklInspectionTaskResult
                        {
                            TaskDetailId = request.TaskDetailId,
                            TaskId = request.TaskId,
                            TreatmentSuggestion = p.TreatmentSuggestion,
                            Position = detail.Position,
                            DamageSize = p.DamageSize,
                            DamagePosition = p.DamagePosition,
                            DamageDescription = p.DamageDescription,
                            DamageLevel = p.DamageLevel,
                            DamageType = p.DamageType,
                            DamageX = p.X,
                            DamageY = p.Y,
                            DamageHeight = p.Height,
                            DamageWidth = p.Width,
                            FacilityId = detail.FacilityId,
                            FacilityName = detail.FacilityName,
                            FactoryId = detail.FactoryId,
                            FactoryName = detail.FactoryName,
                            Createtime = DateTime.Now,
                        }
                )
                .ToList();
            detail.Error += inserts.Count;
            await context.BklInspectionTaskResult.AddRangeAsync(inserts);
            await context.SaveChangesAsync();
            return Json(inserts);
        }

        [HttpGet("list-task-result")]
        public IActionResult GetTaskResult(
            [FromServices] LogonUser user,
            [FromQuery] long taskId,
            [FromQuery] long taskDetailId = 0,
            [FromQuery] long facilityId = 0
        )
        {
            IQueryable<BklInspectionTaskResult> query = context.BklInspectionTaskResult.Where(
                s => s.Deleted == false && s.TaskId == taskId
            );
            if (taskDetailId > 0)
            {
                query = query.Where(p => p.TaskDetailId == taskDetailId);
            }
            if (facilityId > 0)
            {
                query = query.Where(p => p.FacilityId == facilityId);
            }
            return Json(query.ToList());
        }



        [HttpGet("search-task-detail")]
        public IActionResult GetSearchTaskDetail(
            [FromServices] LogonUser user,
            [FromQuery] long factoryId,
            [FromQuery] long taskId,
            [FromQuery] long facilityId = 0,
            [FromQuery] long taskDetailId = 0,
            [FromQuery] string damageType = "",
            [FromQuery] int page = 0, int pagesize = 0
            )
        {
            IQueryable<BklInspectionTaskDetail> query = context.BklInspectionTaskDetail;
            if (taskId > 0)
            {
                query = query.Where(p => p.TaskId == taskId);
            }
            if (factoryId > 0)
            {
                query = query.Where(p => p.FactoryId == factoryId);
            }
            if (facilityId > 0)
            {
                query = query.Where(p => p.FacilityId == facilityId);
            }
            if (taskDetailId > 0)
            {
                query = query.Where(p => p.Id == taskDetailId);
            }


            List<BklInspectionTaskDetail> details = null;

            //if (page > -1 && pagesize > 0)
            //    details = query.Skip((page - 1) * pagesize).Take(pagesize).ToList();
            //else
            //    details = query.ToList();
            IQueryable<BklInspectionTaskResult> result = context.BklInspectionTaskResult;
            if (taskId > 0)
            {
                result = result.Where(p => p.TaskId == taskId);
            }
            if (factoryId > 0)
            {
                result = result.Where(p => p.FactoryId == factoryId);
            }
            if (facilityId > 0)
            {
                result = result.Where(p => p.FacilityId == facilityId);
            }
            if (!string.IsNullOrEmpty(damageType))
                result = result.Where(s => s.DamageType == damageType);
            var errorDetailIds = (from detail in query
                                  join error in result on detail.Id equals error.TaskDetailId
                                  join task in context.BklInspectionTask on detail.TaskId equals task.Id
                                  where task.TaskType != "dataset"
                                  orderby detail.Id descending
                                  select detail.Id);
            List<object> dic = null;
            if (page > -1 && pagesize > 0)
            {
                details = (from d in context.BklInspectionTaskDetail
                           where errorDetailIds.Contains(d.Id)
                           orderby d.Id descending
                           select d).Skip((page - 1) * pagesize).Take(pagesize).ToList();
                dic = (from p in result
                       where errorDetailIds.Contains(p.TaskDetailId)
                       group p by p.DamageType into gps
                       select new { type = gps.Key, count = gps.Count() }).ToList().Cast<object>().ToList();
            }
            else
            {
                details = (from d in context.BklInspectionTaskDetail
                           where errorDetailIds.Contains(d.Id)
                           orderby d.Id descending
                           select d).ToList();
                dic = (from p in result
                       where errorDetailIds.Contains(p.TaskDetailId)
                       group p by p.DamageType into gps
                       select new { type = gps.Key, count = gps.Count() }).ToList().Cast<object>().ToList();
            }
            var taskDetailIds = details.Select(d => d.Id).ToArray();
            var errors = result.Where(p => taskDetailIds.Contains(p.TaskDetailId)).ToList();
            var results = details.Select(s => new { detail = s, errors = errors.Where(q => q.TaskDetailId == s.Id).ToArray() });
            return Json(new
            {
                data = results,
                detailCount = taskDetailIds.Length,
                statistic = dic,
                pagesize = pagesize,
                page = page,
                total = errorDetailIds.Count(),
                error = 0
            });
        }

        [HttpGet("list-task-detail")]
        public IActionResult ListFacilityTaskDetail(
            [FromServices] LogonUser user,
            [FromServices] IRedisClient redis,
            [FromQuery] long taskId,
            [FromQuery] long facilityId = 0
        )
        {
            Dictionary<string, RedisValue> allValues = redis.GetValuesFromHash($"DetectTask:Tid.{taskId}");
            var infos = allValues.Values.Select(s => JsonSerializer.Deserialize<DetectTaskInfo>((string)s)).ToList();

            IQueryable<BklInspectionTaskDetail> query = context.BklInspectionTaskDetail.Where(
                p => p.TaskId == taskId
            );
            if (facilityId > 0)
            {
                query = query.Where(p => p.FacilityId == facilityId);
            }

            var lis = query.ToList();
            var lisType = lis.Select(s => s.FacilityId).Distinct().ToArray();
            var resultGroupCount = context.BklInspectionTaskResult
                .Where(s => lisType.Contains(s.FacilityId))
                .ToList()
                .GroupBy(q => q.FacilityId)
                .Select(m => new { facilityId = m.Key, group = m.GroupBy(s => s.DamageType).Select(r => new { key = r.Key, count = r.Count() }).ToArray() }
                ).ToList();
            var resultList = new List<Object>();
            foreach (var s in lis.GroupBy(s => s.FacilityId))
            {
                var first = s.First();
                var obj = infos.FirstOrDefault(s => s.FacilityId == first.FacilityId);
                resultList.Add(new
                {
                    first.FactoryName,
                    first.FacilityId,
                    first.TaskId,
                    first.FactoryId,
                    number = s.Count(),
                    procced = obj == null ? 0 : obj.Procced,
                    autoError = obj == null ? 0 : obj.Error,
                    checkedError = resultGroupCount.Where(q => q.facilityId == first.FacilityId).Sum(s => s.group.Sum(n => n.count)),
                    facilityName = first.FacilityName,
                    createtime = getImageDate(first.RemoteImagePath),
                    remoteImagePath = first.RemoteImagePath,
                    errorGroup = resultGroupCount
                                .Where(q => q.facilityId == first.FacilityId)
                                .Select(q => q.group)
                                .FirstOrDefault()
                });

            }
            DateTime getImageDate(string RemoteImagePath)
            {
                try
                {
                    var filename = RemoteImagePath.Split('/')[1];
                    var date = filename.Split("_")[1];
                    //20220526095849
                    return DateTime.ParseExact(date, "yyyyMMddHHmmss", null);
                }
                catch
                {
                    return DateTime.MinValue;
                }
            }
            return Json(resultList);
        }
        [HttpGet("task-detail")]
        public IActionResult GetTaskDetail(
            [FromServices] LogonUser user,
            [FromQuery] long taskId,
            [FromQuery] long facilityId = 0,
            [FromQuery] long taskDetailId = 0,
            [FromQuery] int page = 1,
            [FromQuery] int pagesize = 500
        )
        {
            IQueryable<BklInspectionTaskDetail> query = context.BklInspectionTaskDetail.Where(
                        p => p.TaskId == taskId
                    );
            if (facilityId > 0)
            {
                query = query.Where(p => p.FacilityId == facilityId);
            }
            if (taskDetailId > 0)
            {
                query = query.Where(p => p.Id == taskDetailId);
            }
            var lis = query.Skip((page - 1) * pagesize).Take(pagesize).ToList();
            if (pagesize == 500)
                return Json(lis);
            return Json(new { total = query.Count(), page = page, pagesize = pagesize, data = lis });
        }
        [HttpGet("task-detail-and-error")]
        public IActionResult GetTaskDetailAndError(
                    [FromServices] LogonUser user,
                    [FromServices] IRedisClient redis,
                    [FromQuery] long taskId,
                    [FromQuery] long facilityId = 0,
                    [FromQuery] long taskDetailId = 0,
                    [FromQuery] int page = 1,
                    [FromQuery] int pagesize = 1500
                )
        {
            var query = (from p in context.BklInspectionTaskDetail
                         join q in context.BklInspectionTaskResult on p.Id equals q.TaskDetailId
                         into qs
                         from r in qs.DefaultIfEmpty()
                         where p.TaskId == taskId && (facilityId == 0 || p.FacilityId == facilityId) &&
                         (taskDetailId == 0 || p.Id == taskDetailId)
                         select new { detailId = p.Id, detail = p, error = r });
            var joinResult = query.Skip((page - 1) * pagesize)
                              .Take(pagesize)
                              .ToList();
            var results = (from s in joinResult
                           group s by s.detail into gp1
                           select new { detail = gp1.First().detail, errors = gp1.Where(q => q.error != null).Select(r => r.error) }).ToList();

            var filterDict = redis.GetValuesFromHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}.Filterd")
                                    .ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<YoloResult[]>(s.Value.ToString()));

            var EmptyArray = new YoloResult[0] { };
            var ret = results.Select(result =>
            {
                dynamic obj = new ExtensionDynamicObject(result.detail);
                obj.errors = result.errors;
                obj.yoloResult = filterDict.TryGetValue(result.detail.RemoteImagePath, out var re) ? re : EmptyArray;
                return obj;
            }).ToArray();
            if (pagesize == 1500)
            {
                var content = JsonSerializer.Serialize(ret, new JsonSerializerOptions
                {
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return new ContentResult()
                {
                    Content = content,
                    ContentType = "application/json"
                };
            }
            else
            {
                var content = JsonSerializer.Serialize(new
                {
                    page = page,
                    pagesize = pagesize,
                    total = query.Count(),
                    data = ret
                }, new JsonSerializerOptions
                {
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return new ContentResult()
                {
                    Content = content,
                    ContentType = "application/json"
                };
            }

        }




        [HttpGet("facilities")]
        public IActionResult ListFacilities([FromServices] LogonUser user, [FromServices] IRedisClient redis, [FromQuery] long factoryId = 0)
        {
            var facid = factoryId == 0 ? user.factoryId : factoryId;
            var arr = context.BklFactoryFacility.Where(p => p.FactoryId == facid).ToList();
            var faids = arr.Select(s => s.Id).ToList();
            if (faids.Count > 0)
            {
                var query = context.BklInspectionTaskDetail.Where(s => s.FactoryId == facid && s.FacilityId == faids[0]).OrderBy(s => s.Id).Take(1);
                for (int i = 1; i < faids.Count; i++)
                {
                    if (i < faids.Count)
                    {
                        long aa = faids[i];
                        query = query.Union(context.BklInspectionTaskDetail.Where(s => s.FactoryId == facid && s.FacilityId == aa).OrderBy(s => s.Id).Take(1));
                    }
                }
                var piclIst = query.Select(s => new { faid = s.FacilityId, pic = s.RemoteImagePath }).ToList();
                //var piclIst = context.BklInspectionTaskDetail.Where(s => s.FactoryId == fid && faids.Contains(s.FacilityId))
                //	.OrderBy(s => s.Id)
                //	.GroupBy(s => s.FacilityId)
                //	.Select(s => new { faid = s.Key, pic = s.Max(q => q.RemoteImagePath) })
                //	.ToList();
                var rets = arr.Select(s =>
                {
                    var dic = redis.GetValuesFromHash($"FacilityMeta:{s.Id}").ToDictionary(s => s.Key, s => (string)s.Value);
                    dynamic doo = new ExtensionDynamicObject(s);

                    foreach (var item in dic)
                    {
                        doo[item.Key] = item.Value;
                    }
                    doo.Add("defaultPic", piclIst.FirstOrDefault(q => q.faid == s.Id)?.pic);
                    return doo;
                });
                var content = JsonSerializer.Serialize(rets, new JsonSerializerOptions
                {
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return new ContentResult()
                {
                    Content = content,
                    ContentType = "application/json"
                };
            }
            else
            {
                return Json(new int[0]);
            }

        }
        public class LevelRule
        {
            public string type;
            public LevelMatchRule rule;
            public string level;

        }
        public class LevelMatchRule
        {
            public string area; public string op; public string maxLength; public string minLength;
        }
        LevelRule[] levelmatch = new LevelRule[] {
            new LevelRule { type="表面缺陷-胶衣脱落", rule=new LevelMatchRule { area="0.04", op="<=" }, level="LV1" },
            new LevelRule { type="表面缺陷-胶衣脱落", rule=new LevelMatchRule { area="0.04", op=">" }, level="LV2" },
            new LevelRule { type="表面缺陷-航标漆脱落", rule=null, level="LV1" },
            new LevelRule { type="表面缺陷-表面油污", rule=null, level="LV0" },
            new LevelRule { type="表面缺陷-表面污渍", rule=null, level="LV0" },
            new LevelRule { type="表面缺陷-蒙皮脱落", rule=null, level="LV3" },
            new LevelRule { type="表面缺陷-玻纤损伤", rule=new LevelMatchRule { area="0.04", op="<=" }, level="LV2" },
            new LevelRule { type="表面缺陷-玻纤损伤", rule=new LevelMatchRule { area="0.04", op=">" }, level="LV3" },
            new LevelRule { type="表面缺陷-修补痕迹", rule=null, level="TBD" },
            new LevelRule { type="表面缺陷-胶衣腐蚀", rule=null, level="LV1" },
            new LevelRule { type="表面缺陷-鼓包", rule=null, level="TBD" },
            new LevelRule { type="表面缺陷-凹陷", rule=null, level="TBD" },
            new LevelRule { type="表面缺陷-表面划痕", rule=null, level="LV1" },
            new LevelRule { type="前缘腐蚀-胶衣腐蚀", rule=null, level="LV1" },
            new LevelRule { type="前缘腐蚀-玻纤腐蚀", rule=null, level="LV3" },
            new LevelRule { type="前缘腐蚀-保护膜破损", rule=null, level="LV1" },
            new LevelRule { type="表面裂纹-胶衣裂纹", rule=new LevelMatchRule { maxLength="0.2", op="<=" }, level="LV2" },
            new LevelRule { type="表面裂纹-胶衣裂纹", rule=new LevelMatchRule { maxLength="0.2", op=">" }, level="LV3" },
            new LevelRule { type="表面裂纹-后缘弦向裂纹", rule=null, level="LV3" },
            new LevelRule { type="附件脱落损伤-接闪器", rule=null, level="LV2" },
            new LevelRule { type="附件脱落损伤-防雨罩", rule=null, level="LV3" },
            new LevelRule { type="附件脱落损伤-导雷条", rule=null, level="LV2" },
            new LevelRule { type="附件脱落损伤-扰流条", rule=null, level="LV2" },
            new LevelRule { type="附件脱落损伤-锯齿尾缘", rule=null, level="LV1" },
            new LevelRule { type="附件脱落损伤-涡流板", rule=null, level="LV1" },
            new LevelRule { type="附件脱落损伤-排水孔", rule=null, level="LV2" },
            new LevelRule { type="附件脱落损伤-铝叶尖", rule=null, level="LV4" },
            new LevelRule { type="结构损伤-叶尖开裂", rule=null, level="LV3" },
            new LevelRule { type="结构损伤-纤维分层", rule=new LevelMatchRule { area="0.05", op="<=" }, level="LV3" },
            new LevelRule { type="结构损伤-纤维分层", rule=new LevelMatchRule { area="0.05", op=">" }, level="LV4" },
            new LevelRule { type="结构损伤-前缘开裂", rule=new LevelMatchRule { maxLength="0.5", op="<=" }, level="LV3" },
            new LevelRule { type="结构损伤-前缘开裂", rule=new LevelMatchRule { maxLength="2", minLength="0.5", op="range" }, level="LV4" },
            new LevelRule { type="结构损伤-前缘开裂", rule=new LevelMatchRule { maxLength="2", op=">" }, level="LV5" },

            new LevelRule { type="结构损伤-后缘开裂", rule=new LevelMatchRule { maxLength="0.5", op="<=" }, level="LV3" },
            new LevelRule { type="结构损伤-后缘开裂", rule=new LevelMatchRule { maxLength="2", minLength="0.5", op="range" }, level="LV4" },
            new LevelRule { type="结构损伤-后缘开裂", rule=new LevelMatchRule { maxLength="2", op=">" }, level="LV5" },

            new LevelRule { type="雷击损伤-玻纤损伤", rule=new LevelMatchRule { area="0.04", op="<=" }, level="LV3" },
            new LevelRule { type="雷击损伤-玻纤损伤", rule=new LevelMatchRule { area="0.04", op=">" }, level="LV4" },
            new LevelRule { type="雷击损伤-爆炸性开裂", rule=null, level="LV5" },
        };
        [HttpPost("change-level")]
        public IActionResult ChangeLevelConfirm([FromServices] LogonUser user, [FromServices] BklDbContext context, [FromQuery] long factoryId, [FromQuery] long facilityId)
        {
            var results = context.BklInspectionTaskResult.Where(s => s.FacilityId == facilityId && s.FactoryId == factoryId).ToList();
            results.ForEach(changeLevelByRule);
            try
            {
                context.SaveChanges();
                return Ok();
            }
            catch
            {
                return BadRequest();
            } 
        }

        private void changeLevelByRule(BklInspectionTaskResult result)
        {
            var sz = result.DamageSize.Split('×').Select(t => double.Parse(t.TrimEnd('m'))).ToArray();
            var lis = levelmatch.Where(s => s.type == result.DamageType).ToList();
            foreach (var rule in lis)
            {
                if (rule.rule == null)
                {
                    result.DamageLevel = rule.level;
                    break;
                }
                if (rule.rule.area != null)
                {
                    var area = sz[0] * sz[1];
                    var limitArea = double.Parse(rule.rule.area);
                    if (rule.rule.op == "<=" && area <= limitArea || rule.rule.op == ">" && area > limitArea)
                    {
                        result.DamageLevel = rule.level;
                        break;
                    }
                }
                if (rule.rule.maxLength != null && rule.rule.minLength == null)
                {
                    result.DamageLevel = rule.level;
                    var limit = double.Parse(rule.rule.maxLength);
                    var max = Math.Max(sz[0], sz[1]);
                    if (rule.rule.op == "<=" && max <= limit || rule.rule.op == ">" && max > limit)
                    {
                        result.DamageLevel = rule.level;
                        break;
                    }
                }
                if (rule.rule.maxLength != null && rule.rule.minLength != null && rule.rule.op == "range")
                {
                    var max = Math.Max(sz[0], sz[1]);
                    var limitl = double.Parse(rule.rule.minLength);
                    var limith = double.Parse(rule.rule.maxLength);
                    if (max <= limith && max > limitl)
                    {
                        result.DamageLevel = rule.level;
                        break;
                    }
                }
            }
        }

        [HttpGet("change-level")]
        public IActionResult ChangeLevel([FromServices] LogonUser user, [FromServices] BklDbContext context, [FromQuery] long factoryId, [FromQuery] long facilityId)
        {
            var results = context.BklInspectionTaskResult.Where(s => s.FacilityId == facilityId && s.FactoryId == factoryId).ToList();
            results.ForEach(changeLevelByRule);
            //context.SaveChanges();
            return Json(results.Select(s => new { s.DamageType, s.DamageSize, s.DamageLevel }));
        }



        [HttpPost("commit-validation-result")]
        public IActionResult CommitValidationResult([FromServices] LogonUser user, [FromServices] BklDbContext context, [FromBody] ChangeErrorResult[] results)
        {
            var ids = results.Select(s => s.resultId).ToArray();
            var resultsDb = context.BklInspectionTaskResult.Where(s => ids.Contains(s.Id)).ToList();
            foreach (var item in resultsDb)
            {
                var change = results.Where(s => s.resultId == item.Id).First();
                if (change.type == "changePos")
                {
                    item.DamagePosition = change.newInfo;
                }
                if (change.type == "changeSize")
                {
                    item.DamageSize = change.newInfo;
                }
            }
            int ret = context.SaveChanges();
            return Json(new { error = ret == 0 ? 1 : 0, msg = $"commit {ret} records" });
        }
        [HttpGet("get-validation-result")]
        public IActionResult ValidateResult([FromServices] LogonUser user,
                    [FromServices] BklDbContext context,
                    [FromServices] IRedisClient redis,
                    [FromQuery] long factoryId,
                    [FromQuery] long facilityId = 0,
                    [FromQuery] long taskId = 0)
        {
            var allresults = context.BklInspectionTaskResult.Where(s => s.TaskId == taskId && s.FactoryId == factoryId && (facilityId == 0 || s.FacilityId == facilityId)).ToList();
            //float len = float.Parse((string)redis.GetValueFromHash($"FacilityMeta:{facilityId}", "叶片长度"));
            List<object> ret = new List<object>();
            foreach (var re in allresults)
            {
                try
                {
                    float len = float.Parse((string)redis.GetValueFromHash($"FacilityMeta:{re.FacilityId}", "叶片长度"));
                    var errors = validateLength(re, len).ToArray();
                    ret.AddRange(errors);
                }
                catch (Exception ex)
                {
                    ret.Add(new ChangeErrorResult { facilityId = re.FacilityId, facilityName = re.FacilityName, error = $"叶片长度：{redis.GetValueFromHash($"FacilityMeta:{facilityId} ", "叶片长度")}" });
                    logger.LogError(ex.ToString());
                }
            }
            return Json(ret);
        }
        static IEnumerable<ChangeErrorResult> validateLength(BklInspectionTaskResult result, float len)
        {
            List<ChangeErrorResult> errorInfo = new List<ChangeErrorResult>();
            //4.15m×0.00m
            var sizeinfo = result.DamageSize.Split('×').Select(s => s.TrimEnd('m')).Select(s => (int.TryParse(s, out var a1) ? a1 : -1)).ToArray();
            //距离叶根部约40.74m
            var posinfo = float.TryParse(result.DamagePosition.Substring(6).TrimEnd('m'), out var q1) ? q1 : -1;
            var posinfoNew = posinfo;
            if (posinfoNew > len)
            {
                posinfoNew = len;
            }
            var min = Math.Min(sizeinfo[0], sizeinfo[1]);
            var max = Math.Max(sizeinfo[0], sizeinfo[1]);
            var minNew = min;
            var maxNew = max;
            if (minNew > 5)
            {
                minNew = 5;
            }
            if (maxNew > 5)
            {
                maxNew = 5;
            }
            if ((minNew + posinfoNew) > len && (maxNew + posinfoNew) > len)
            {
                posinfoNew = len - maxNew;
            }
            else if ((minNew + posinfoNew) > len)
            {
                posinfoNew = len - minNew;
            }
            else if ((maxNew + posinfoNew) > len)
            {
                posinfoNew = len - maxNew;
            }
            if (posinfoNew != posinfo)
                yield return new ChangeErrorResult
                {
                    resultId = result.Id,
                    facilityId = result.FacilityId,
                    facilityName = result.FacilityName,
                    damageType = result.DamageType,
                    taskId = result.TaskId,
                    taskDetailId = result.TaskDetailId,
                    position = result.Position,
                    type = "changePos",
                    oldInfo = result.DamagePosition,
                    newInfo = $"距离叶根部约{posinfoNew.ToString("#0.00")}m"
                };
            if (min != minNew || maxNew != max)
                yield return new ChangeErrorResult
                {
                    resultId = result.Id,
                    facilityId = result.FacilityId,
                    facilityName = result.FacilityName,
                    damageType = result.DamageType,
                    taskId = result.TaskId,
                    taskDetailId = result.TaskDetailId,
                    position = result.Position,
                    type = "changeSize",
                    oldInfo = result.DamageSize,
                    newInfo = $"{minNew.ToString("#0.00")}m×{maxNew.ToString("#0.00")}m"
                };
        }

        [HttpGet("list-task")]
        public IActionResult ListTask(
            [FromServices] LogonUser user,
            [FromQuery] long factoryId,
            [FromQuery] long taskId = 0,
            [FromQuery] DateTime? startTime = null,
            [FromQuery] string taskType = "all",
            [FromQuery] string taskStatus = "all"
        )
        {
            IQueryable<BklInspectionTask> query = query = context.BklInspectionTask; ;
            //不是管理员 就判断权限
            if (!user.IsAdmin())
            {
                var taskIds = user.GetPermittedId(context, "task");
                if (taskIds.Length == 0)
                    return Forbid();
                if (taskIds.All(s => s != 0))
                    query = query.Where(s => taskIds.Contains(s.Id));
            }
            if (factoryId > 0)
            {
                query = query.Where(p => p.FactoryId == factoryId);
            }
            if (taskId > 0)
            {
                query = query.Where(t => t.Id == taskId);
            }

            if (startTime != null)
            {
                DateTime dt = startTime.Value;
                query = query.Where(s => s.Createtime > dt);
            }
            if (taskType != "all" && !string.IsNullOrEmpty(taskType))
            {
                query = query.Where(s => s.TaskType == taskType);
            }
            if (taskStatus != "all" && !string.IsNullOrEmpty(taskStatus))
            {
                query = query.Where(s => s.TaskStatus == taskStatus);
            }
            var lis = query.ToList();
            return Json(lis);
        }

        [HttpPost("create-task")]
        public async Task<IActionResult> CreateTask(
            [FromServices] LogonUser user,
            [FromBody] CreateInspectionTaskRequest request
        )
        {
            var item = new BklInspectionTask
            {
                CreatorId = user.userId,
                FactoryId = request.FactoryId,
                FactoryName = request.FactoryName,
                TaskDescription = request.Description,
                TaskName = request.TaskName,
                TaskType = request.TaskType,
                TaskStatus = "init",
                TotalNumber = 0,
                Updatetime = System.DateTime.Now,
                Createtime = System.DateTime.Now,
            };
            context.BklInspectionTask.Add(item);
            await context.SaveChangesAsync();
            return Json(item);
        }

        [HttpPost("create-task-detail")]
        public async Task<IActionResult> CreateTaskDetail(
            [FromServices] LogonUser user,
            [FromServices] IRedisClient redis,
            [FromBody] CreateInspectionTaskDetailRequest request
        )
        {
            redis.SetEntryInHash(
                $"Task.{request.TaskId}:Facility.{request.FacilityId}",
                request.Position,
                JsonSerializer.Serialize(
                    new
                    {
                        StartIndex = request.StartIndex,
                        EndIndex = request.EndIndex,
                        OverLap = request.OverLap,
                    }
                )
            );

            var details = request.PictureList.Select(s =>
                        new BklInspectionTaskDetail
                        {
                            FacilityId = request.FacilityId,
                            FacilityName = request.FacilityName,
                            FactoryId = request.FactoryId,
                            FactoryName = request.FactoryName,
                            Position = request.Position,
                            TaskId = request.TaskId,
                            ImageHeight = s.Height.ToString(),
                            ImageWidth = s.Width.ToString(),
                            ImageType = s.Type == null ? "image/jpeg" : s.Type,
                            LocalImagePath = "#",
                            RemoteImagePath = s.Name,
                            Createtime = DateTime.Now
                        }
                )
                .ToArray();
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == request.TaskId);
            task.TotalNumber += details.Count();
            context.BklInspectionTaskDetail.AddRange(details);
            await context.SaveChangesAsync();
            return Json(details);
        }

        [HttpDelete("delete-task-detail")]
        public IActionResult Delete(long taskDetailId)
        {
            using (var tran = context.Database.BeginTransaction())
            {
                try
                {
                    var td = context.BklInspectionTaskDetail
                        .Where(s => s.Id == taskDetailId)
                        .FirstOrDefault();
                    context.BklInspectionTaskDetail.Remove(td);
                    var tdError = context.BklInspectionTaskResult
                        .Where(s => s.TaskDetailId == taskDetailId)
                        .AsNoTracking()
                        .ToList();
                    context.BklInspectionTaskResult.RemoveRange(tdError);
                    context.SaveChanges();
                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return Json(new { error = 1, errorMsg = ex.ToString() });
                }
            }
            return Json(new { error = 0, });
        }

        [HttpPost("add-to-dataset")]
        public IActionResult AddToDataset([FromQuery(Name = "taskIds")] long[] taskDetailIds, [FromQuery] long datasetid)
        {
            if (datasetid == 0)
                return Json(new { error = 1, errorMsg = "错误的数据集" });
            var info1 = context.BklInspectionTaskDetail.Where(s => s.TaskId != datasetid && taskDetailIds.Contains(s.Id))
            .Select(s => new { id = s.Id, path = s.RemoteImagePath }).ToList();
            var paths = info1.Select(s => s.path).ToArray();
            var exitsPath = context.BklInspectionTaskDetail
                    .Where(s => s.TaskId == datasetid && paths.Contains(s.RemoteImagePath)).Select(s => s.RemoteImagePath).ToArray();
            var exceptIds = exitsPath.Intersect(paths).Join(info1, p => p, q => q.path, (p, q) => q).Select(s => s.id);
            taskDetailIds = taskDetailIds.Except(exceptIds).ToArray();

            using (var tran = context.Database.BeginTransaction())
            {
                try
                {

                    foreach (var taskDetailId in taskDetailIds)
                    {
                        var td = context.BklInspectionTaskDetail
                            .Where(s => s.Id == taskDetailId)
                            .AsNoTracking()
                            .FirstOrDefault();
                        var tdError = context.BklInspectionTaskResult
                            .Where(s => s.TaskDetailId == taskDetailId)
                            .AsNoTracking()
                            .ToList();
                        td.Id = 0;
                        td.TaskId = datasetid;
                        context.BklInspectionTaskDetail.Add(td);
                        context.SaveChanges();
                        tdError.ForEach(item =>
                        {
                            item.Id = 0;
                            item.TaskId = datasetid;
                            item.TaskDetailId = td.Id;
                        });
                        context.BklInspectionTaskResult.AddRange(tdError);
                        context.SaveChanges();
                    }
                    tran.Commit();
                }
                catch (Exception ex)
                {
                    tran.Rollback();
                    return Json(new { error = 1, errorMsg = ex.ToString() });
                }
            }
            return Json(new { error = 0, data = new { exceptIds, taskDetailIds } });
        }
        [HttpPost("change-position")]
        public async Task<IActionResult> ChangePosition([FromServices] LogonUser user, [FromServices] IRedisClient redis, [FromServices] BklDbContext context, [FromBody] ChangePositionRequest request)
        {
            var taskDetails = context.BklInspectionTaskDetail.Where(s => request.ChangeTaskDetailIds.Contains(s.Id)).ToList();
            taskDetails.ForEach(item =>
            {
                item.Position = request.NewPosition;
            });
            var results = context.BklInspectionTaskResult.Where(s => request.ChangeTaskDetailIds.Contains(s.TaskDetailId)).ToList();
            results.ForEach(item => item.Position = request.NewPosition);
            await context.SaveChangesAsync();
            return Json(new { error = 0 });
        }

        [HttpGet("group-by-position")]
        public IActionResult GroupByPosition([FromQuery] long factoryId = 0)
        {
            var groupBy = context.BklInspectionTaskDetail.Where(s => (factoryId == 0 || s.FactoryId == factoryId))
            .GroupBy(s => new { pos = s.Position, facId = s.FactoryId })
            .Select(q => new { position = q.Key.pos, factoryId = q.Key.facId, count = q.Count() })
            .ToList();
            return Json(groupBy);
        }
        [HttpGet("get-config")]
        public IActionResult GetConfig([FromServices] IRedisClient redis, [FromQuery] string hashKey)
        {
            try
            {
                var val = redis.GetValuesFromHash($"Config:{hashKey}").ToDictionary(s => s.Key, s => (string)s.Value);
                return Json(new { error = (val == null || val.Count == 0) ? 1 : 0, data = val });
            }
            catch
            {
                return Json(new { error = 1 });
            }
        }
        [HttpPost("set-config")]
        public IActionResult SetConfig([FromServices] IRedisClient redis, [FromQuery] string hashKey, [FromBody] Dictionary<string, object> post)
        {
            redis.SetRangeInHash($"Config:{hashKey}", post.ToList().Select(s => new KeyValuePair<string, RedisValue>(s.Key, s.Value.ToString())));
            return Json(new { error = 0 });
        }
    }
}
