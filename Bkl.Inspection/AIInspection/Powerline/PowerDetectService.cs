using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bkl.Infrastructure;
using Bkl.Inspection;
using Bkl.Models;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Minio;
using static Org.BouncyCastle.Math.EC.ECCurve;

public class PowerDetectService : BackgroundService
{
    private BklConfig _config;
    private IServiceScope _scope;
    private IRedisClient _redisClient;
    private ILogger<DetectImageService> _logger;
    private Channel<PowerTask> _channel;

    public class PowerTask
    {
        public long TaskId { get; set; }
        public long FacilityId { get; set; }
        public double Threshold { get; set; }

    }
    public PowerDetectService(BklConfig config, Channel<PowerTask> powertask, IServiceProvider serviceProvider, ILogger<DetectImageService> logger)
    {
        _config = config;
        _scope = serviceProvider.CreateScope();
        _redisClient = _scope.ServiceProvider.GetService<IRedisClient>();
        _logger = logger;
        _channel = powertask;
    }
    public class YoloResultPower
    {
        public string name { get; set; }
        public float conf { get; set; }
        public float x { get; set; }
        public float y { get; set; }
        public float w { get; set; }
        public float h { get; set; }
    }
    private async Task ProcessTask(PowerTask taskitem)
    {
        var minio = new MinioClient()
        .WithEndpoint(_config.MinioConfig.EndPoint)
        .WithCredentials(_config.MinioConfig.Key, _config.MinioConfig.Secret)
        .WithRegion(_config.MinioConfig.Region)
        .Build();

        var levels = await minio.ReadObject<LevelConfig[]>("power-class.txt", "sysconfig");

        var names = levels.Where(s => s.level != "normal").Select(q => q.@class).ToArray();
        try
        {

            using var context = new BklDbContext(new DbContextOptionsBuilder<BklDbContext>().UseMySQL(_config.MySqlString).Options);
            var task = context.BklInspectionTask.Where(s => s.Id == taskitem.TaskId).FirstOrDefault();
            var dbPaths = context.BklInspectionTaskDetail
                    .Where(s => s.TaskId == taskitem.TaskId && s.FacilityId == taskitem.FacilityId)
                  .Select(s => new { s.Id, s.FacilityId, s.RemoteImagePath })
                  .ToList()
                  .Where(s => s.RemoteImagePath.EndsWith("Z.JPG") || s.RemoteImagePath.EndsWith("V.JPG"))
                  .ToList();
            _logger.LogInformation($"PowerTask dbPaths:{dbPaths.Count}");
            _redisClient.SetEntryInHash($"PowerTaskProgress:{taskitem.TaskId}", $"{taskitem.FacilityId}.total", dbPaths.Count);
            _redisClient.SetEntryInHash($"PowerTaskProgress:{taskitem.TaskId}", $"{taskitem.FacilityId}.progress", 0);

            List<BklInspectionTaskResult> results = new List<BklInspectionTaskResult>();

            int page = 0;
            int left = 10;

            while (left != 0)
            {
                var tasks = dbPaths.Skip(page * 16).Take(16).ToArray();
                left = tasks.Length;
                if (left > 0)
                {
                    object[] objs = new object[tasks.Length];
                    Parallel.For(1, tasks.Length, index =>
                    {
                        var s = tasks[index];
                        var rets = DetectOneImage(taskitem, task, s, names);
                        objs[index - 1] = rets;
                    });
                    for (var i = 0; i < objs.Length; i++)
                    {
                        var item = objs[i] as BklInspectionTaskResult[];
                        var detail = tasks[i];
                        _redisClient.SetEntryInHash($"PowerTask:{taskitem.TaskId}:{taskitem.FacilityId}", detail.Id.ToString(), i);
                        _redisClient.IncrementValueInHash($"PowerTaskProgress:{taskitem.TaskId}", $"{taskitem.FacilityId}.progress", 1);
                        BklInspectionTaskResult[] rs = item as BklInspectionTaskResult[];
                        if (rs != null)
                        {
                            results.AddRange(rs);
                        }
                    }
                }

                //Task.WaitAll(tasks);
                //tasks.Where(s => s.Result != null)
                //    .Select(s => s.Result)
                //    .ToList()
                //    .ForEach(q => results.AddRange(q));
                page = page + 1;
            }
            using (var tran = context.Database.BeginTransaction())
            {
                try
                {
                    context.BklInspectionTaskResult.AddRange(results);
                    context.SaveChanges();
                    context.Database.CommitTransaction();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "saveDb");
                }
            }

        }
        catch (Exception ex1)
        {
            _logger.LogError(ex1, "saveDb");
        }
    }
    HttpClient client = new HttpClient();
    private BklInspectionTaskResult[] DetectOneImage(PowerTask taskitem, BklInspectionTask task, dynamic detail, string[] levels)
    {

        try
        {
            if (_redisClient.SetEntryInHashIfNotExists($"PowerTask:{taskitem.TaskId}:{taskitem.FacilityId}", detail.Id.ToString(), ""))
            {
                try
                {
                    var resp = client.GetAsync($"{_config.PowerDetectService}?path=http://{_config.MinioConfig.EndPoint}/power-line/{detail.RemoteImagePath}")
                        .GetAwaiter().GetResult();
                    var respstr = resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    _logger.LogWarning((string)detail.RemoteImagePath + " " + respstr);
                    //_redisClient.SetEntryInHash($"PowerTask:{taskitem.TaskId}:{taskitem.FacilityId}", detail.Id.ToString(), respstr);
                    //_redisClient.IncrementValueInHash($"PowerTaskProgress:{taskitem.TaskId}", $"{taskitem.FacilityId}.progress", 1);
                    if (respstr != "null")
                    {
                        var result = JsonSerializer.Deserialize<YoloResultPower[]>(respstr);

                        return result.Where(s => taskitem.Threshold == 0 || s.conf >= taskitem.Threshold)
                            .Where(s => levels.Contains(s.name))
                            .Select(s =>
                            {

                                var x = s.x - s.w / 2;
                                var y = s.y - s.h / 2;
                                var h = s.h;
                                var w = s.w;

                                return new BklInspectionTaskResult
                                {
                                    Id = SnowId.NextId(),
                                    DamageDescription = "systemgen",
                                    DamageX = x.ToString(),
                                    DamageY = y.ToString(),
                                    DamageHeight = h.ToString(),
                                    DamageWidth = w.ToString(),
                                    DamageType = s.name,

                                    Createtime = DateTime.Now,
                                    DamageLevel = s.conf.ToString(),
                                    DamagePosition = "",
                                    DamageSize = "",
                                    Deleted = false,
                                    FacilityId = detail.FacilityId,
                                    TaskId = taskitem.TaskId,
                                    TaskDetailId = detail.Id,
                                    FactoryId = task.FactoryId,
                                    FacilityName = "",
                                    FactoryName = task.FactoryName,
                                    Position = "",
                                    TreatmentSuggestion = ""
                                };
                            }).ToArray();
                    }

                }
                catch (Exception ex)
                {
                    _redisClient.RemoveEntryFromHash($"PowerTask:{taskitem.TaskId}:{taskitem.FacilityId}", detail.Id.ToString());
                    _logger.LogError(ex, "1");
                }
            }
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "2");
            return null;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var taskitem = await _channel.Reader.ReadAsync();
            _logger.LogInformation("PowerTask " + taskitem.TaskId + " " + taskitem.FacilityId + " " + taskitem.Threshold);
            try
            {
                await ProcessTask(taskitem);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
        }
    }
}
