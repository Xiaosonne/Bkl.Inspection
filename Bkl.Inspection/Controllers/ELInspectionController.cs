using Bkl.Infrastructure;
using Bkl.Models;
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI.Common;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bkl.Inspection
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class ELInspectionController : Controller
    {
        private IServiceProvider serviceProvider;
        BklDbContext context;
        private ILogger<ELInspectionController> logger;
        private IBackgroundTaskQueue<GenerateAllTaskRequest> taskQueue;

        public ELInspectionController(BklDbContext context, IServiceProvider serviceProvider,
         ILogger<ELInspectionController> logger)
        {
            this.serviceProvider = serviceProvider;
            this.context = context;
            this.logger = logger;
        }
        [HttpGet("manufactory")]
        public IActionResult GetElFactories([FromServices] IRedisClient redis)
        {
            var elFactory = redis.GetValuesFromHash("FactoryMeta")
                .Where(s => s.Key.EndsWith("-ELFactory"))
                .Select(s => s.Value.ToString())
                .Distinct();
            return Json(elFactory);
        }
        [HttpPost("batch-task-detail")]
        public IActionResult BatchCreate([FromBody] BatchCreateELTaskDetailRequest request)
        {
            var files = Directory.EnumerateFiles(request.Path);
            return Json(files);
        }
        [HttpGet("detect")]
        public async Task<IActionResult> DetectError([FromServices] BklConfig config,
            [FromServices] ELDetectHelper elhelper, [FromQuery] long taskid,
            [FromQuery] string path)
        {
            var result = await elhelper.PathDetect(config, path);
            return Json(result);
        }

        [HttpGet("report")]
        public IActionResult GetSearchTaskDetail(
            [FromServices] LogonUser user,
            [FromQuery] long factoryId,
            [FromQuery] long taskId)
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



            List<BklInspectionTaskDetail> details = query.ToList();

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

            var errors = result.ToList();
            var results = details.Select(s => new { detail = s, errors = errors.Where(q => q.TaskDetailId == s.Id).ToArray() });
            return Json(new
            {
                data = results,
                totalError = errors.Count(),
                totalDetail = details.Count,
                error = 0
            });
        }

        [HttpGet("{dact}-{dtype}-task")]
        public async Task<IActionResult> StartDetectTask(
            [FromServices] BklConfig config,
            [FromServices] BklDbContext context,
            [FromServices] IRedisClient redis,
            long taskId,
            long factoryId,
             [FromRoute] string dact = "start",
            [FromRoute] string dtype = "detect")
        {
            if (dact == "reset")
            {
                redis.RemoveEntryFromHash($"ELSegTaskResult:Fid.{factoryId}.Tid.{taskId}", "running");
            }
            if (dtype == "seg")
            {
                string running = redis.GetValueFromHash($"ELSegTaskResult:Fid.{factoryId}.Tid.{taskId}", "running");
                var details = context.BklInspectionTaskDetail.Where(s => s.FactoryId == factoryId && s.TaskId == taskId).Select(s => new { s.Id, s.RemoteImagePath }).ToList();
                var xor = details.Select(s => s.Id).Aggregate(0l, (pre, cur) => pre ^ cur);
                if (string.IsNullOrEmpty(running))
                {
                    if (redis.SetEntryInHashIfNotExists($"ELSegTaskResult:Fid.{factoryId}.Tid.{taskId}", "running", xor.ToString()))
                    {
                        IBackgroundTaskQueue<ELSegImage> queue = serviceProvider.GetService<IBackgroundTaskQueue<ELSegImage>>();
                        foreach(var item in details)
                        {
                            await queue.EnqueueAsync(new ELSegImage(item.Id.ToString(), taskId.ToString(), factoryId.ToString(), item.RemoteImagePath));
                        }
                    }
                   
                    return Json(new DataResponse<object> { error = 0, data = new { total = details.Count, proceed = 0, start = running } });
                }
                else
                {
                    var xorstr = redis.GetValueFromHash($"ELSegTaskResult:Fid.{factoryId}.Tid.{taskId}", "running");
                    var resultIds = redis.GetKeysFromHash($"ELSegTaskResult:Fid.{factoryId}.Tid.{taskId}").Except(new string[] {"running"}).Select(long.Parse).ToArray();
                    if (xorstr != xor)
                    {
                        redis.SetEntryInHash($"ELSegTaskResult:Fid.{factoryId}.Tid.{taskId}", "running", xor.ToString());
                        var arr = details.Select(s => s.Id).Except(resultIds).ToArray();
                        var insert = details.Where(s => arr.Contains(s.Id)).ToArray();
                        IBackgroundTaskQueue<ELSegImage> queue = serviceProvider.GetService<IBackgroundTaskQueue<ELSegImage>>();
                        foreach (var item in insert)
                        {
                            await queue.EnqueueAsync(new ELSegImage(item.Id.ToString(), taskId.ToString(), factoryId.ToString(), item.RemoteImagePath));
                        }
                    }
                    return Json(new DataResponse<object> { error = 0, data = new { total = details.Count, proceed = resultIds.Count(), start = running } });
                }
            }
            if (dtype == "detect")
            {
                IBackgroundQueueQueue taskInfoQueue = serviceProvider.GetService<IBackgroundTaskQueue<ELDetectTaskInfo>>();
                string type = "ELDetect";
                var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);

                var detailCount = context.BklInspectionTaskDetail
                  .Where(s => s.TaskId == taskId)
                  .Count();

                var taskinfo = new ELDetectTaskInfo { TaskType = task.TaskType, Total = detailCount, TaskId = taskId };

                Dictionary<string, RedisValue> taskDictInfo = new Dictionary<string, RedisValue>();
                StringBuilder sb = new StringBuilder();
                if (redis.SetIfNotExists($"{type}Task:Tid.{taskId}", JsonSerializer.Serialize(taskinfo)))
                {
                    await taskInfoQueue.EnqueueAsync(taskinfo);
                    logger.LogInformation($"StartDetectTask {type} {taskId} {taskinfo.FacilityId}");
                }
                else
                {
                    string jsonStr = redis.Get($"{type}Task:Tid.{taskId}");
                    var oldTask = JsonSerializer.Deserialize<ELDetectTaskInfo>(jsonStr);
                    if (DateTime.Now.Subtract(oldTask.LastTime).TotalMinutes > 5)
                    {
                        oldTask.LastTime = DateTime.Now;
                        redis.Set($"{type}Task:Tid.{taskId}", JsonSerializer.Serialize(oldTask));
                        await taskInfoQueue.EnqueueAsync(taskinfo);
                        logger.LogInformation($"ReStartDetectTask {type} {taskId} {taskinfo.FacilityId}");
                    }
                    else
                    {
                        sb.Append($"{taskinfo.FacilityId} 正在运行,请{(5 - DateTime.Now.Subtract(oldTask.LastTime).TotalMinutes).ToString("F2")}分钟后再试。");
                    }
                }
                if (sb.Length == 0)
                    return Json(new GeneralResponse { error = 0, success = true });
                return Json(new GeneralResponse { error = 1, msg = sb.ToString() });

            }
            return Json(new GeneralResponse { error = 1,msg="任务类型出错" });
        }
        [HttpGet("geo-statistics")]
        public JsonResult GeoStatistics([FromServices] BklDbContext context)
        {
            var picCount = context.BklInspectionTaskDetail.GroupBy(s => s.FactoryId)
                .Select(s => new { key = s.Key, count = s.Count() }).ToList();


            var picErrorCount = context.BklInspectionTaskResult.GroupBy(s => s.FactoryId)
               .Select(s => new { key = s.Key, count = s.Count() }).ToList();
            var facs = context.BklFactory.ToList();
            var dicCityCount = new Dictionary<string, string>();
            var dicCityErrorCount = new Dictionary<string, string>();
            var dicProvinceCount = new Dictionary<string, string>();
            foreach (var sameProvince in facs.GroupBy(s => s.Province))
            {
                var allCount = 0;
                foreach (var sameCity in sameProvince.GroupBy(s => s.City))
                {
                    var fids = sameCity.Select(s => s.Id).ToList();
                    var count = picCount.Where(s => fids.Contains(s.key)).Sum(s => s.count);
                    var countError = picErrorCount.Where(s => fids.Contains(s.key)).Sum(s => s.count);
                    allCount += count;
                    dicCityCount.Add(sameProvince.Key + "-" + sameCity.Key, count.ToString());
                    dicCityErrorCount.Add(sameProvince.Key + "-" + sameCity.Key, countError.ToString());
                }
                dicProvinceCount.Add(sameProvince.Key, allCount.ToString());
            }
            var last = DateTime.Now.Subtract(TimeSpan.FromDays(30));
            var uploadTrends = context.BklInspectionTaskDetail.Where(s => s.Createtime < last)
                .Select(s => new { s.Createtime, s.Id })
                .ToList()
                .GroupBy(s => s.Createtime.ToString("yyyy-MM-dd"))
                .Select(s => new { key = s.Key, count = s.Count() })
                .ToList();

            var elFacPercent = context.BklInspectionTaskDetail.GroupBy(s => s.Position)
                .Select(s => new { key = s.Key, count = s.Count() })
                .ToList();

            var errorTrends = context.BklInspectionTaskResult.Where(s => s.Createtime < last)
               .Select(s => new { s.Createtime, s.Id })
               .ToList()
               .GroupBy(s => s.Createtime.ToString("yyyy-MM-dd"))
               .Select(s => new { key = s.Key, count = s.Count() })
               .ToList();

            var errorPercent = (from error in context.BklInspectionTaskResult
                                join task in context.BklInspectionTaskDetail on error.TaskDetailId equals task.Id
                                group task by task.Position into tts
                                select new { key = tts.Key, count = tts.Count() }).ToList();
            var count1 = new
            {
                totalDetail = context.BklInspectionTaskDetail.Count(),
                totalError = context.BklInspectionTaskResult.Count(),
                totalFactory = context.BklFactory.Count(),
            };

            return Json(new
            {
                statisticCount = count1,
                errorPercent,
                errorTrends,
                elFacPercent,
                uploadTrends,
                city = dicCityCount,
                cityError = dicCityErrorCount,
                province = dicProvinceCount,
                fac = picCount
            });
        }
        [HttpPost("task-detail")]
        public async Task<IActionResult> CreateELImage([FromServices] LogonUser user, [FromServices] IRedisClient redis, [FromBody] CreateELTaskDetailRequest request)
        {
            var factory = context.BklFactory.FirstOrDefault(s => s.Id == request.FactoryId);
            if (factory == null)
            {
                throw new Exception($"no factory id {request.FactoryId}");
            }
            var facilityNames = request.PictureList.Where(s => s.Name.NotEmpty()).Select(s =>
            {
                return s.Name.Split('/')[1].Split('.')[0];
            }).ToList();
            List<BklFactoryFacility> facilityList = new List<BklFactoryFacility>();
            foreach (var facilityName in facilityNames)
            {
                var facility = context.BklFactoryFacility.Where(s => s.FactoryId==request.FactoryId && s.Name == facilityName).FirstOrDefault();
                if (facility == null)
                {
                    facility = new BklFactoryFacility
                    {
                        Name = facilityName,
                        FactoryName = factory.FactoryName,
                        FacilityType = "EL",
                        GPSLocation = "[]",
                        FactoryId = factory.Id,
                        CreatorId = user.userId,
                        Createtime = DateTime.Now,
                        CreatorName = "",
                    };
                    context.BklFactoryFacility.Add(facility);
                }
                facilityList.Add(facility);
            }
            var details = request.PictureList.Select(pic =>
                    {
                        var faci = facilityList.FirstOrDefault(q => pic.Name.Contains(q.Name));
                        var detail = new BklInspectionTaskDetail
                        {
                            FacilityId = faci.Id,
                            FacilityName = faci.Name,
                            FactoryId = factory.Id,
                            FactoryName = factory.FactoryName,
                            Position = request.Position,
                            TaskId = request.TaskId,
                            ImageHeight = pic.Height.ToString(),
                            ImageWidth = pic.Width.ToString(),
                            ImageType = pic.Type == null ? "image/jpeg" : pic.Type,
                            LocalImagePath = "#",
                            RemoteImagePath = pic.Name,
                            Createtime = !string.IsNullOrEmpty(pic.CreateTime) && DateTime.TryParseExact(pic.CreateTime, default(string), null, System.Globalization.DateTimeStyles.None, out var parsedTime) ? parsedTime : DateTime.Now
                        };
                        return detail;
                    }
                )
                .ToArray();

            context.BklInspectionTaskDetail.AddRange(details);
            await context.SaveChangesAsync();
            return Json(details);
        }
        [HttpGet("download")]
        public async Task<IActionResult> DownloadReport([FromServices] BklDbContext context,
            [FromServices] BklConfig config,
            [FromServices] IRedisClient redis,
            long factoryId,
            long taskId)
        {
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);
            var details = context.BklInspectionTaskDetail.Where(s => s.FactoryId == factoryId && s.TaskId == taskId).ToList();
            var results = context.BklInspectionTaskResult.Where(s => s.FactoryId == factoryId && s.TaskId == taskId).ToList();
            var ret = await ReportHelper.GenerateELReport(config, redis, task, details, results);
            var fs = new FileStream(Path.Combine(config.MinioDataPath, ret.Location), FileMode.Open);
            return File(fs, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", ret.FileName);
        }

        [HttpGet("statistics")]
        public JsonResult Statistics([FromServices] BklDbContext context,
            long factoryId,
            long taskId,
            string type,
            string starttime,
            string endtime)
        {
            var query = context.BklInspectionTaskResult.Where(s => 1 == 1);
            if (factoryId > 0)
                query = query.Where(s => s.FactoryId == factoryId);
            if (taskId > 0)
                query = query.Where(s => s.TaskId == taskId);
            var allErrors = query.ToList();
            IEnumerable<IGrouping<string, BklInspectionTaskResult>> grouping = null;
            switch (type)
            {
                case "company":
                    grouping = allErrors.GroupBy(s => s.Position);
                    break;
                case "error":
                    grouping = allErrors.GroupBy(s => s.DamageType);
                    break;
                default:
                    break;
            }
            List<object> lis = new List<object>();
            if (grouping == null)
                return Json("");
            foreach (var gp in grouping)
            {
                var errorCount = gp.GroupBy(s => s.DamageType)
                    .Select(s => new { key = s.Key, count = s.Count() })
                    .ToList();
                var errorCompCount = gp.GroupBy(s => s.DamageType)
                    .Select(s => new { key = s.Key, count = s.Select(q => q.TaskDetailId).Distinct().Count() })
                    .ToList();

                lis.Add(new { key = gp.Key, errorCount = errorCount, errorCompCount = errorCompCount });

            }
            return Json(lis);
        }
        [HttpGet("date-filter")]
        public IActionResult GetDateFilter([FromServices] LogonUser user,
                    [FromServices] IRedisClient redis,
                    [FromQuery] long taskId,
                     [FromQuery] long factoryId)
        {
            var lis = redis.GetAllItemsFromSet($"ELDateFilter:Task-{taskId}").OrderBy(s => s).ToList();
            return Json(lis);
        }
        [HttpGet("task-detail")]
        public IActionResult GetTaskDetailAndError(
                    [FromServices] LogonUser user,
                    [FromServices] IRedisClient redis,
                    [FromQuery] long taskId,
                    [FromQuery] long factoryId=0,
                    [FromQuery] long facilityId = 0,
                    [FromQuery] long taskDetailId = 0,
                    [FromQuery] string errorFilter = "all",
                    [FromQuery] int page = 1,
                    [FromQuery] int pagesize = 1500,
                    [FromQuery] string dateFilter = "")
        {
            IQueryable<BklInspectionTaskDetail> query = context.BklInspectionTaskDetail.Where(s => 1 == 1);

            if (taskId > 0)
                query = query.Where(s => s.TaskId == taskId);
            if (factoryId > 0)
                query = query.Where(s => s.FactoryId == factoryId);
            if (facilityId > 0)
                query = query.Where(s => s.FacilityId == facilityId);
            if (taskDetailId > 0)
                query = query.Where(s => s.Id == taskDetailId);
            if (dateFilter != "" && dateFilter != "all" && dateFilter != "undefined")
            {
                var arr = dateFilter.Split('-');
                var year = int.Parse(arr[0]);
                var month = int.Parse(arr[1]);
                var day = int.Parse(arr[2]);
                query = query.Where(s => s.Createtime.Year == year && s.Createtime.Month == month && s.Createtime.Day == day);
            }
            if (errorFilter != "all" && errorFilter != "undefined" && errorFilter != "")
            {
                switch (errorFilter)
                {
                    case "onlyError":
                        query = query.Where(s => context.BklInspectionTaskResult.Any(e => e.TaskDetailId == s.Id));
                        break;
                    default:
                        query = query.Where(s => context.BklInspectionTaskResult.Any(e => e.DamageType == errorFilter && e.TaskDetailId == s.Id));
                        break;
                }
            }


            var pagedData = query.Skip((page - 1) * pagesize)
                              .Take(pagesize)
                              .ToList();
            //pagedData.Select(s => (s.Createtime.ToString("yyyy-MM-dd"),s.Createtime.Year*10000+s.Createtime.Month*100+s.Createtime.Day))
            pagedData.Select(s => s.Createtime.ToString("yyyy-MM-dd"))
                .Distinct()
                .ToList()
                .ForEach(s =>
                {
                    redis.AddItemToSet($"ELDateFilter:Task-{taskId}", s);
                });

            var ids = pagedData.Select(s => s.Id).ToArray();
            var errors = context.BklInspectionTaskResult.Where(s => ids.Contains(s.TaskDetailId)).ToList();


            var rets = pagedData.Select(s =>
              {
                  dynamic obj = new ExtensionDynamicObject(s);
                  obj.errors = errors.Where(k => k.TaskDetailId == s.Id).ToList();
                  return obj;
              }).ToArray();

            var content = JsonSerializer.Serialize(new
            {
                page = page,
                pagesize = pagesize,
                total = query.Count(),
                data = rets
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
}
