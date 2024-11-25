
using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.AspNetCore.Mvc;
using Minio;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Bkl.Inspection.Controllers
{

    [Route("[controller]")]
    public partial class PLInspectionController : Controller
    {
        [HttpGet("get-detect-progress")]
        public async Task<JsonResult> SaveReport([FromServices] IRedisClient redis,
       long factoryId, long taskId)
        {

            return Json(redis.GetValuesFromHash($"PowerTaskProgress:{taskId}").ToDictionary(s => s.Key, s => (int)s.Value));
        }
        [HttpGet("start-detect")]
        public async Task<JsonResult> SaveReport([FromServices] IRedisClient redis,
            [FromServices] BklConfig config,
            [FromServices] BklDbContext context,
            [FromServices] Channel<PowerDetectService.PowerTask> channel,
            long factoryId, long taskId, long facilityId = 0, float threshold = 0)
        {
            var facis = context.BklInspectionTaskDetail.Where(s => (facilityId == 0 || s.FacilityId == facilityId) && s.FactoryId == factoryId && s.TaskId == taskId).Select(s => s.FacilityId).Distinct().ToList();
            var errors = context.BklInspectionTaskResult.Where(s => (facilityId == 0 || s.FacilityId == facilityId) && s.FactoryId == factoryId && s.TaskId == taskId && s.DamageDescription == "systemgen");
            context.BklInspectionTaskResult.RemoveRange(errors);
            await context.SaveChangesAsync();
            foreach (var faciId in facis)
            {
                redis.Remove($"PowerTask:{taskId}:{faciId}");
                redis.SetEntryInHash($"PowerTaskProgress:{taskId}", $"{faciId}.total", 1);
                redis.SetEntryInHash($"PowerTaskProgress:{taskId}", $"{faciId}.progress", 0);

                await channel.Writer.WriteAsync(new PowerDetectService.PowerTask { Threshold = threshold, TaskId = taskId, FacilityId = faciId });
            }
            return Json(redis.GetValuesFromHash($"PowerTaskProgress:{taskId}").ToDictionary(s => s.Key, s => (int)s.Value));
        }

        [HttpGet("report-progress")]
        public IActionResult ReportProgress(
                        [FromServices] BklConfig config,
                        [FromServices] BklDbContext context,
                        [FromServices] IRedisClient redis,
                        [FromQuery] long taskId,
                        [FromQuery] long reportIndex,
                         [FromQuery] long factoryId)
        {
            if (reportIndex == 0)
            {
                var arr = redis.GetValuesFromHash($"ReportProgress:{factoryId}:{taskId}")
                      .Select(s => new { index = s.Key.Split(".")[0], type = s.Key.Split(".")[1], value = (long)s.Value })
                      .GroupBy(s => s.index)
                      .Select(s =>
                      {
                          return new
                          {
                              reportIndex = s.Key,
                              total = s.FirstOrDefault(s => s.type == "total").value,
                              progress = s.FirstOrDefault(s => s.type == "progress").value,
                              time = s.FirstOrDefault(s => s.type == "time").value,

                          };
                      })
                      .OrderByDescending(s => s.reportIndex)
                      .Take(5)
                      .ToArray();

                return Json(arr);
            }
            else
            {
                int a = (int)redis.GetValueFromHash($"ReportProgress:{factoryId}:{taskId}", $"{reportIndex}.progress");
                int b = (int)redis.GetValueFromHash($"ReportProgress:{factoryId}:{taskId}", $"{reportIndex}.total");
                int c = (int)redis.GetValueFromHash($"ReportProgress:{factoryId}:{taskId}", $"{reportIndex}.time");
                return Json(new object[] { new { reportIndex, total = b, progress = a, time = c.ToString() } });
            }

        }
        [HttpPost("report")]
        public async Task<IActionResult> GenReport(
        [FromServices] BklConfig config,
                    [FromServices] BklDbContext context,
                    [FromServices] IRedisClient redis,
                    [FromServices] Channel<PowerReportGenerateService.PowerTask> taskChannel,
                    [FromQuery] long taskId,
                    [FromQuery] long factoryId, [FromBody] long[] facilities)
        {
            var factory = context.BklFactory.FirstOrDefault(s => s.Id == factoryId);
            var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskId);
            var needfaci = facilities.Length != 0;
            if (needfaci == false)
            {
                facilities = context.BklFactoryFacility.Where(s => s.FactoryId == factoryId).Select(s => s.Id).ToArray();
                needfaci = true;
            }
            var taskDetails = context.BklInspectionTaskDetail
                .Where(s => s.FactoryId == factoryId && (needfaci == false || facilities.Contains(s.FacilityId)))
                .ToList();
            var taskResults = context.BklInspectionTaskResult
                .Where(p => p.FactoryId == factoryId && p.TaskId == taskId && (needfaci == false || facilities.Contains(p.FacilityId)))
                .ToList();
            var index = SnowId.NextId();

            await taskChannel.Writer.WriteAsync(new PowerReportGenerateService.PowerTask
            {
                Task = task,
                Factory = factory,
                TaskDetails = taskDetails,
                TaskResults = taskResults,
                ReportIndex = index,
            });

            return Json(new { reportIndex = index });
        }


        [HttpPost("set-report-template")]
        public async Task<IActionResult> SetReportConfig([FromServices] BklConfig config, [FromBody] ReportTemplate data)
        {
            var minio = new MinioClient()
            .WithEndpoint(config.MinioConfig.EndPoint)
            .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
           .WithRegion(config.MinioConfig.Region)
           .Build();
            await minio.WriteObject(data, "power-report-template.txt", "sysconfig");
            return Json(data);
        }

        [HttpPost("set-report-config")]
        public async Task<IActionResult> SetReportConfig([FromServices] BklConfig config, [FromBody] LevelConfig[] data)
        {
            var minio = new MinioClient()
            .WithEndpoint(config.MinioConfig.EndPoint)
            .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
           .WithRegion(config.MinioConfig.Region)
           .Build();
            await minio.WriteObject(data, "power-class.txt", "sysconfig");
            return Json(data);
        }

        [HttpGet("report-download")]
        public async Task<IActionResult> DownloadReport(
        [FromServices] BklConfig config,
                    [FromServices] BklDbContext context,
                    [FromServices] IRedisClient redis,
                    [FromServices] Channel<PowerReportGenerateService.PowerTask> taskChannel,
                    [FromQuery] long taskId,
                    [FromQuery] long reportIndex,
                    [FromQuery] long factoryId)
        {

            string str = redis.GetValueFromHash($"ReportResult:{factoryId}:{taskId}", reportIndex.ToString());


            using (var ms = new MemoryStream())
            {
                using (var fs = new FileStream(str, FileMode.OpenOrCreate))
                {
                    await fs.CopyToAsync(ms);
                }
                ms.Seek(0, SeekOrigin.Begin);
                return File(ms.ToArray(), "application/stream", str);
            }
        }
    }
}
