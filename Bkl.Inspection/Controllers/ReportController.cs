using Bkl.Infrastructure;
using Bkl.Models;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bkl.ESPS.Controllers
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class ReportController : Controller
    {
        private IServiceProvider serviceProvider;
        BklDbContext context;
        private ILogger<ReportController> logger;
        private IBackgroundTaskQueue<GenerateAllTaskRequest> taskQueue;

        public ReportController(BklDbContext context, IServiceProvider serviceProvider,
         IBackgroundTaskQueue<GenerateAllTaskRequest> taskqueue,
         IBackgroundTaskQueue<DetectTaskInfo> taskInfoQueue,
         ILogger<ReportController> logger)
        {
            this.serviceProvider = serviceProvider;
            this.context = context;
            this.logger = logger;
            this.taskQueue = taskqueue;
        }
        [HttpGet("facility-task-report")]
        public IActionResult FacilityTaskReport(
       [FromServices] LogonUser user,
       [FromQuery] long taskId,
       [FromQuery] long facilityId = 0
   )
        {
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);
            var details = context.BklInspectionTaskDetail
                .Where(t => t.TaskId == taskId && t.FacilityId == facilityId)
                .ToList();
            var results = context.BklInspectionTaskResult
                .Where(s => s.FacilityId == facilityId && s.TaskId == taskId)
                .ToList();
            Dictionary<string, object> statistic = new Dictionary<string, object>();
            statistic["taskName"] = task.TaskName;
            statistic["factoryName"] = task.FactoryName;
            statistic["facilityName"] = details.First().FacilityName;
            statistic["taskTime"] = task.Createtime.ToString("yyyy-MM-dd HH:mm");
            statistic["totalNumber"] = task.TotalNumber;
            statistic["faultNumber"] = details.Count(s => s.Error > 0);
            statistic["faultPercentOfTotal"] =
                details.Count(s => s.Error > 0) / (task.TotalNumber * 1.0);
            statistic["faultPositionPercentOfTotal"] = results
                .GroupBy(q => q.Position)
                .Select(s => new { key = s.Key, value = s.Count() / (results.Count * 1.0) });
            statistic["faultTypePercentOfTotal"] = results
                .GroupBy(q => q.DamageType)
                .Select(s => new { key = s.Key, value = s.Count() / (results.Count * 1.0) });
            statistic["faultPositionTypePercentOfTotal"] = results
                .GroupBy(q => q.Position)
                .Select(
                    s =>
                        new
                        {
                            key = s.Key,
                            value = s.GroupBy(q => q.DamageType)
                                .Select(
                                    q => new { key = q.Key, value = q.Count() / (s.Count() * 1.0) }
                                )
                        }
                );
            statistic["faultTypePositionPercentOfTotal"] = results
                .GroupBy(q => q.DamageType)
                .Select(
                    s =>
                        new
                        {
                            key = s.Key,
                            value = s.GroupBy(q => q.Position)
                                .Select(
                                    q => new { key = q.Key, value = q.Count() / (s.Count() * 1.0) }
                                )
                        }
                );
            return new JsonResult(statistic);
        }


        [HttpGet("export-check-table")]
        public async Task<IActionResult> ExportCheckTableAsync(
            [FromServices] BklConfig config,
            [FromServices] LogonUser user,
            [FromServices] IRedisClient redis,
            [FromQuery] long taskId,
            [FromQuery] long facilityId,
            [FromQuery] long factoryId
        )
        {
            var vals = redis.GetValuesFromHash($"FacilityMeta:{facilityId}");
            var factory = context.BklFactory.FirstOrDefault(s => s.Id == factoryId);
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);
            var facility = context.BklFactoryFacility
                .Where(s => s.FactoryId == factoryId && s.Id == facilityId)
                .FirstOrDefault();

            var facilities = context.BklFactoryFacility
                .Where(s => s.FactoryId == factoryId)
                .ToList();

            var taskDetails = context.BklInspectionTaskDetail
                .Where(s => s.FactoryId == factoryId && s.FacilityId == facilityId)
                .ToList();
            var taskResults = context.BklInspectionTaskResult
                .Where(
                    p =>
                        p.FactoryId == factoryId && p.FacilityId == facilityId && p.TaskId == taskId
                )
                .ToList();
            MemoryStream ms = new MemoryStream();
            var create = new CreateWord();
            using (WordprocessingDocument word = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var maindoc = word.AddMainDocumentPart();
                create.CreateMainDocumentPart(maindoc, new CreateCheckTableParagraph(taskResults, redis));
                word.Save();
            }

            ms.Seek(0, SeekOrigin.Begin);
            try
            {
                var pt = System.IO.Path.Combine(config.FileBasePath, "GenerateReports");
                if (!Directory.Exists(pt))
                {
                    Directory.CreateDirectory(pt);
                }
                var filename = System.IO.Path.Combine(
                    pt,
                    $"风机检查表-{factory.FactoryName}-{facility.Name}-{task.TaskName}-{taskDetails[0].TaskId}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx"
                );
                using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
                {
                    await ms.CopyToAsync(fs, 1024 * 1024 * 10);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            try
            {
                create.Done();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return File(ms.ToArray(), "application/stream", "file.docx");
        }

        [HttpGet("export-task-result-desc")]
        public IActionResult ExportFacilityTaskResultdescAsync(
                [FromServices] BklConfig config,
            [FromServices] LogonUser user,
            [FromServices] IRedisClient redis,
            [FromQuery] long taskId,
            [FromQuery] long facilityId,
            [FromQuery] long factoryId
        )
        {
            var taskResults = context.BklInspectionTaskResult
                .Where(
                    p =>
                        p.FactoryId == factoryId && p.FacilityId == facilityId && p.TaskId == taskId
                )
                .ToList();
            var gps = taskResults
                .GroupBy(
                    s =>
                        s.DamageType.Split("-")[1]
                                    .Replace("锯齿尾缘", "锯齿尾缘脱落损伤")
                        .Replace("接闪器", "接闪器脱落损伤")
                        .Replace("涡流板", "涡流板脱落损伤")
                        .Replace("扰流条", "扰流条脱落损伤")
                )
                .Select(
                    s =>
                        new
                        {
                            dtype = s.Key,
                            yps = s.Select(m => m.Position.Split("/")[0]).Distinct()
                        }
                )
                .ToArray();
            List<string> sb = new List<string>();
            foreach (var gps2 in gps.GroupBy(s => s.yps.Count()).OrderByDescending(s => s.Key))
            {
                if (gps2.Key == 3)
                    sb.Add("三个叶片都存在" + string.Join("，", gps2.Select(m => m.dtype)));
                if (gps2.Key == 2)
                {
                    var mm = gps2.ToList().GroupBy(q => string.Join("、", q.yps.OrderBy(q => q)));
                    foreach (var two in mm)
                    {
                        // two.Aggregate()
                        sb.Add($"{two.Key}还存在" + string.Join("、", two.Select(m => m.dtype)));
                    }
                }
                if (gps2.Key == 1)
                {
                    foreach (var one in gps2.GroupBy(q => q.yps.FirstOrDefault()))
                    {
                        sb.Add($"{one.Key}还存在{string.Join("、", one.Select(m => m.dtype))}");
                    }
                }
            }
            //tbs.Last().Append(new TableRow(new TableCell(new TableCellProperties(
            //                  new TableCellWidth() { Width = PercentWidth(50), Type = TableWidthUnitValues.Dxa },
            //                  new GridSpan { Val = 5 },
            //                  new Paragraph(new Run(new Text("检查结果：" + string.Join("现象；", sb) + "现象。")))
            //                  ))));
            return Json(new { msg = "检查结果：" + string.Join("现象；", sb) + "现象。" });
        }

        [AllowAnonymous]
        [HttpGet("export-all-task-result")]
        public async Task<IActionResult> ExportAllFacilitiesTaskResultAsync(
            [FromServices] BklConfig config,
            [FromServices] IRedisClient redis,
            [FromQuery] long taskId,
            [FromQuery] long factoryId,
            [FromQuery] string mode = "normal"
        )
        {
            var factory = context.BklFactory.FirstOrDefault(s => s.Id == factoryId);
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);
            var facilities = context.BklFactoryFacility
                .Where(s => s.FactoryId == factoryId)
                .ToList();
            GenerateAllTaskRequest req = new GenerateAllTaskRequest
            {
                taskId = taskId,
                factoryId = factoryId,
                mode = mode,
                factory = factory,
                task = task,
                facilities = facilities,
                taskDetails = context.BklInspectionTaskDetail.Where(s => s.TaskId == taskId).ToList(),
                taskResults = context.BklInspectionTaskResult.Where(s => s.TaskId == taskId).ToList(),
                SeqId = Guid.NewGuid().ToString(),
            };

            await taskQueue.EnqueueAsync(req);
            return Json(new ReportResult { SeqId = req.SeqId, Status = "generating" });
        }
        [HttpGet("get-export-progress")]
        public List<ReportResult> GetExportProgress([FromServices] IRedisClient redis, [FromServices] LogonUser user, [FromQuery] long taskId)
        {
            var results = ReportResult.LoadValues(redis, taskId);
            results = results.OrderByDescending(q => q.StartTime).Take(10).ToList();
            return results;
        }

        [HttpGet("export-task-result")]
        public async Task<IActionResult> ExportFacilityTaskResultAsync([FromServices] BklConfig config,
            [FromServices] LogonUser user,
            [FromServices] IRedisClient redis,
            [FromQuery] long taskId,
            [FromQuery] long factoryId,

            [FromQuery(Name = "faids")] long[] faids,
                [FromQuery] string mode = null
        )
        {
            if (faids == null)
                faids = new long[] { 0 };
            var factory = context.BklFactory.FirstOrDefault(s => s.Id == factoryId);
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);
            var facility = context.BklFactoryFacility
                .Where(s => s.FactoryId == factoryId && faids.Contains(s.Id))
                .FirstOrDefault();

            var facilities = context.BklFactoryFacility
                .Where(s => s.FactoryId == factoryId && faids.Contains(s.Id))
                .ToList();

            var taskDetails = context.BklInspectionTaskDetail
                .Where(s => s.FactoryId == factoryId && faids.Contains(s.FacilityId))
                .ToList();
            var taskResults = context.BklInspectionTaskResult
                .Where(p => p.FactoryId == factoryId && faids.Contains(p.FacilityId) && p.TaskId == taskId)
                .ToList();
            GenerateAllTaskRequest req = new GenerateAllTaskRequest
            {
                taskId = taskId,
                factoryId = factoryId,
                mode = mode,
                factory = factory,
                task = task,
                facilities = facilities,
                taskDetails = taskDetails,
                taskResults = taskResults,
                SeqId = Guid.NewGuid().ToString(),
            };

            await taskQueue.EnqueueAsync(req);
            return Json(new ReportResult { SeqId = req.SeqId, Status = "generating" });
        }
        [HttpPost("export-facility-time")]
        public IActionResult ExportFacilityTime([FromServices] LogonUser user, [FromServices] BklDbContext context, [FromServices] IRedisClient redis)
        {
            var facility = context.BklFactoryFacility.ToList();
            var sb = facility.Select(s =>
            {
                try
                {
                    var time = redis.GetValueFromHash($"FacilityMeta:{s.Id}", "巡检时间");
                    return $"{s.FactoryName} {s.Name} {time}";
                }
                catch
                {
                    return "";
                }
            })
            .Where(s => !string.IsNullOrEmpty(s))
            .Aggregate(new StringBuilder(), (sb, item) =>
            {
                return sb.AppendLine(item);
            });
            return Json(sb.ToString());
        }

        string[] levelStrs = new string[] { "LV0", "LV1", "LV2", "LV3", "LV4", "LV5", "TBD" };
        [HttpGet("export-error-level")]
        public async Task<IActionResult> ExportFacilityStatisticTableAsync(
           [FromServices] BklConfig config,
           [FromServices] LogonUser user,
           [FromServices] IRedisClient redis,
           [FromQuery] long taskId,
           [FromQuery] long factoryId,
           [FromQuery(Name = "faids")] long[] faids)
        {
            var factory = context.BklFactory.FirstOrDefault(s => s.Id == factoryId);
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);
            var facilitiesWhere = context.BklFactoryFacility
                .Where(s => s.FactoryId == factoryId);
            if (faids != null)
            {
                facilitiesWhere = facilitiesWhere.Where(s => faids.Contains(s.Id));
            }
            var facilities = facilitiesWhere.ToList();
            var fids = facilities.Select(s => s.Id).ToArray();
            var taskDetails = context.BklInspectionTaskDetail
                .Where(s => s.FactoryId == factoryId && fids.Contains(s.FacilityId))
                .ToList();
            var taskResults = context.BklInspectionTaskResult
                .Where(p => p.FactoryId == factoryId && p.TaskId == taskId && fids.Contains(p.FacilityId))
                .ToList()
                .Where(s => !s.DamageType.Contains("外观正常"))
                .ToList();
            List<Dictionary<string, string>> datas = new List<Dictionary<string, string>>();
            List<Dictionary<string, string>> datas2 = new List<Dictionary<string, string>>();
            foreach (var gp1 in taskResults.OrderBy(s=>s.FacilityName).GroupBy(s => s.FacilityId))
            {
                var first = gp1.FirstOrDefault();

                foreach (var gpsamePath in gp1.GroupBy(s => s.Position.Split("/")[0]).OrderBy(s => s.Key))
                {
                    var dic = new Dictionary<string, string>();
                    dic.Add("fname", first.FacilityName);
                    dic.Add("total", gpsamePath.Count().ToString());
                    dic.Add("path", gpsamePath.Key);
                    var aaa = gpsamePath.GroupBy(s => s.DamageLevel).Select(s => new { key = s.Key, count = s.Count() }).ToList();
                    foreach (var level in levelStrs)
                    {
                        var f = aaa.FirstOrDefault(s => s.key == level);
                        if (!dic.ContainsKey(level))
                            dic.Add(level, (f == null ? 0 : f.count).ToString());
                        else
                        {
                            if (dic[level] == "0" && f != null)
                                dic[level] = f.count.ToString();
                        }
                    }

                    datas.Add(dic);
                }
            }
            foreach (var gp in taskResults.GroupBy(s => s.DamageType))
            {
                var ts = gp.Key.Split('-');
                var aaa = gp.Select(s => s.FacilityName).Distinct();
                datas2.Add(new Dictionary<string, string> {
                    {"T1", ts[0]},
                    {"T2", ts[1]},
                    {"T3", string.Join("、",aaa)},
                    {"T4", aaa.Count().ToString()},
                });
            }
            var dlevellis = taskResults.GroupBy(s => s.DamageLevel).Select(s => new { key = s.Key, value = s.Count() }).ToList();
            var dtypelis = taskResults.GroupBy(s => s.DamageType).Select(s => new { key = s.Key, value = s.Count() }).ToList();

            var totalfaci = taskResults.Select(s => s.FacilityName).Distinct().Count();

            using MemoryStream ms = new MemoryStream();
            var create = new CreateWord();
            using (WordprocessingDocument word = WordprocessingDocument.Create(
                    ms,
                    DocumentFormat.OpenXml.WordprocessingDocumentType.Document
                )
            )
            {
                var maindoc = word.AddMainDocumentPart();
                create.CreateMainDocumentPart(
                    maindoc,
                    new CreateTextParagraph(
                        $"表A-1 风机缺陷类型统计汇总表",
                        fontSize: "32",
                        values: JustificationValues.Center
                    ),
                    new CreateTableParagraph(
                        datas,
                        new string[] { "fname", "total", "path", "LV0", "LV1", "LV2", "LV3", "LV4", "LV5", "TBD" },
                        new string[] { "风机号", "缺陷总数", "叶片", "LV0", "LV1", "LV2", "LV3", "LV4", "LV5", "TBD" },
                        colWith: new Dictionary<string, int>
                        {
                            {"fname",12 },
                            {"total",12},
                            {"path",12},
                            {"LV0",9},
                            {"LV1",9},
                            {"LV2",9},
                            {"LV3",9},
                            {"LV4",9},
                            {"LV5",9},
                            {"TBD",9},
                        }
                    ),
                    new CreateTextParagraph(
                        $"表A-2 各缺陷类型风机分布情况",
                        fontSize: "32",
                        values: JustificationValues.Center
                    ),
                    new CreateTableParagraph(
                        datas2,
                        new string[] { "T1", "T2", "T3", "T4" },
                        new string[] { "缺陷类型", "缺陷层级", "风机编号", "风机数" },
                        colWith: new Dictionary<string, int>
                        {
                            {"T1",20 },
                            {"T2",20},
                            {"T3",50},
                            {"T4",10}
                        }
                    ),
                    new CreateTextParagraph(
                        $"巡检结论",
                        fontSize: "32",
                        values: JustificationValues.Center
                    ),
                    new CreateTextParagraph(
                        $"本次共检查风机{facilities.Count}台，发现问题的风机{totalfaci}台。共发现缺陷{dlevellis.Sum(t => t.value)}处，{string.Join("，", dlevellis.OrderByDescending(s => s.key).Select(q => $"{q.key}缺陷{q.value}处"))}。",
                        fontSize: "24",
                        values: JustificationValues.Left
                    ), new CreateTextParagraph(
                        $"所有的缺陷中，出现频率最高的依次为{string.Join("、", dtypelis.OrderByDescending(s => s.value).Select(t => $"{t.key}{t.value}处"))}。",
                        fontSize: "24",
                        values: JustificationValues.Left
                    )
                );
                word.Save();
            }

            ms.Seek(0, SeekOrigin.Begin);
            try
            {
                var pt = System.IO.Path.Combine(config.FileBasePath, "GenerateReports");
                if (!Directory.Exists(pt))
                {
                    Directory.CreateDirectory(pt);
                }
                var filename = System.IO.Path.Combine(
                    pt,
                    $"整体统计-{factory.FactoryName}-{task.TaskName}-{taskDetails[0].TaskId}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx"
                );
                using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
                {
                    await ms.CopyToAsync(fs, 1024 * 1024 * 10);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            try
            {
                create.Done();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return File(ms.ToArray(), "application/stream", "file.docx");
        }


        [HttpGet("export-task-result-statistic")]
        public async Task<IActionResult> ExportFacilityTaskResultStatisticTableAsync(
            [FromServices] BklConfig config,
        [FromServices] LogonUser user,
        [FromServices] IRedisClient redis,
        [FromQuery] long taskId,
        [FromQuery] long factoryId,
        [FromQuery(Name = "faids")] long[] faids
    )
        {
            var factory = context.BklFactory.FirstOrDefault(s => s.Id == factoryId);
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);
            var facilitiesWhere = context.BklFactoryFacility
                .Where(s => s.FactoryId == factoryId);
            if (faids != null)
            {
                facilitiesWhere = facilitiesWhere.Where(s => faids.Contains(s.Id));
            }
            var facilities = facilitiesWhere.ToList();
            var fids = facilities.Select(s => s.Id).ToArray();
            var taskDetails = context.BklInspectionTaskDetail
                .Where(s => s.FactoryId == factoryId && fids.Contains(s.FacilityId))
                .ToList();
            var taskResults = context.BklInspectionTaskResult
                .Where(p => p.FactoryId == factoryId && p.TaskId == taskId && fids.Contains(p.FacilityId))
                .ToList()
                .Where(s => !s.DamageType.Contains("外观正常"))
                .ToList();
            MemoryStream ms = new MemoryStream();

            List<Dictionary<string, string>> allfacstatistic =
                new List<Dictionary<string, string>>();
            int i = 1;
            foreach (var gp in taskResults.GroupBy(s =>
            {

                return s.DamageType.Split('-')[1].Replace("锯齿尾缘", "锯齿尾缘脱落损伤")
                   .Replace("接闪器", "接闪器脱落损伤")
                   .Replace("涡流板", "涡流板脱落损伤")
                   .Replace("扰流条", "扰流条脱落损伤");
            })
            )
            {
                Dictionary<string, string> dic = new Dictionary<string, string>();
                var tai = gp.Select(s => s.FacilityId).Distinct();
                var info = tai.Select(s =>
                {
                    var fa = facilities.FirstOrDefault(m => m.Id == s);
                    return fa.Name;
                });
                // var info = gp.GroupBy(s => s.FacilityId).Select(q =>
                // {
                //     var fa = facilities.FirstOrDefault(m => m.Id == q.Key);
                //     return $"{fa.Name}";
                // });
                dic.Add("num", i.ToString());
                dic.Add("err", gp.Key);
                dic.Add("fa", string.Join("、", info.OrderBy(s => s).ToArray()));
                dic.Add("count", tai.Count().ToString());
                allfacstatistic.Add(dic);
                i++;
            }

            var create = new CreateWord();
            using (WordprocessingDocument word = WordprocessingDocument.Create(
                    ms,
                    DocumentFormat.OpenXml.WordprocessingDocumentType.Document
                )
            )
            {
                var maindoc = word.AddMainDocumentPart();
                create.CreateMainDocumentPart(
                    maindoc,
                    new CreateTextParagraph(
                        $"表5-1 叶片存在主要问题",
                        fontSize: "32",
                        values: JustificationValues.Center
                    ),
                    new CreateTableParagraph(
                        allfacstatistic.ToList(),
                        new string[] { "num", "err", "fa", "count" },
                        new string[] { "序号", "主要问题", "风机编号", "数量（台）" },
                        colWith: new Dictionary<string, int>
                        {
                            { "num", 10 },
                            { "err", 20 },
                            { "fa", 60 },
                            { "count", 10 },
                        }
                    )
                );
                word.Save();
            }

            ms.Seek(0, SeekOrigin.Begin);
            try
            {
                var pt = System.IO.Path.Combine(config.FileBasePath, "GenerateReports");
                if (!Directory.Exists(pt))
                {
                    Directory.CreateDirectory(pt);
                }
                var filename = System.IO.Path.Combine(
                    pt,
                    $"整体统计-{factory.FactoryName}-{task.TaskName}-{taskDetails[0].TaskId}-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx"
                );
                using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
                {
                    await ms.CopyToAsync(fs, 1024 * 1024 * 10);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            try
            {
                create.Done();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return File(ms.ToArray(), "application/stream", "file.docx");
        }


    }
}
