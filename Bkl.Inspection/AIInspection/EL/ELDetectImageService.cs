using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MySqlX.XDevAPI.Common;
using StackExchange.Redis;

public record ELSegImage(string TaskDetailId, string TaskId, string FactoryId, string RemotePath);
public class ELDetectImageService : BackgroundService
{
    private BklConfig _config;
    private IServiceScope _scope;
    private IRedisClient _redisClient;
    private ILogger<ELDetectImageService> _logger;
    IBackgroundTaskQueue<ELDetectTaskInfo> _detectQueue;
    public ELDetectImageService(BklConfig config,
        IServiceProvider serviceProvider,
        ILogger<ELDetectImageService> logger,
        IBackgroundTaskQueue<ELDetectTaskInfo> queue)
    {
        _config = config;
        _scope = serviceProvider.CreateScope();
        _redisClient = _scope.ServiceProvider.GetService<IRedisClient>();
        _logger = logger;
        _detectQueue = queue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        var context = _scope.ServiceProvider.GetService<BklDbContext>();
        var helper = _scope.ServiceProvider.GetService<ELDetectHelper>();
        while (!stoppingToken.IsCancellationRequested)
        {
            var taskitem = await _detectQueue.DequeueAsync(stoppingToken);
            if (taskitem == null)
            {
                Thread.Sleep(1000);
            }
            string prefix = "ELDetectTask";
            try
            {
                string jsonStr = _redisClient.Get($"{prefix}:Tid.{taskitem.TaskId}");
                var taskInfo = JsonSerializer.Deserialize<ELDetectTaskInfo>(jsonStr);
                var task = context.BklInspectionTask.FirstOrDefault(s => s.Id == taskInfo.TaskId);
                var all = context.BklInspectionTaskDetail.Where(s => s.TaskId == taskitem.TaskId)
                     .Select(s => new { s.Id, s.FacilityId, s.Position, s.FacilityName, s.RemoteImagePath }).ToList();
                var paths = all.Select(s => s.RemoteImagePath).ToList();
                var savedPaths = _redisClient.GetKeysFromHash($"{prefix}Result:Tid.{taskInfo.TaskId}");
                paths = paths.Except(paths.Where(savedPaths.Contains)).ToList();
                int i = 1;
                var allPath = paths.Take(20).ToList();
                var resultDic = paths.ToDictionary(s => s, s => (YoloResult[])null);
                while (allPath.Count > 0)
                {
                    Parallel.ForEach(allPath, (path1) =>
                    {
                        try
                        {
                            var result = helper.PathDetect(_config, path1).GetAwaiter().GetResult();
                            resultDic[path1] = result;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"error for {path1} {ex.ToString()}");
                        }
                    });
                    allPath = paths.Skip(i * 20).Take(20).ToList();
                    i++;
                    var notNullResultDic = resultDic.Where(s => s.Value != null && s.Value.Count() > 0).ToDictionary(s => s.Key, s => s.Value);
                    taskInfo.LastTime = DateTime.Now;
                    taskInfo.Procced = savedPaths.Count + (i + 1) * 20;
                    taskInfo.Error += notNullResultDic.Count;
                    _redisClient.Set($"{prefix}:Tid.{taskitem.TaskId}", JsonSerializer.Serialize(taskInfo));
                    _redisClient.Set($"{prefix}Running:Tid.{taskitem.TaskId}", DateTime.Now.ToString(), 5 * 3600);
                    _logger.LogInformation($"task {taskitem.TaskId} {taskitem.FacilityId} result for {allPath.Count} total:{paths.Count} cur:{i * 20}");
                }
                if (resultDic.Count > 0)
                {
                    var notNullResultDic = resultDic.Where(s => s.Value != null && s.Value.Count() > 0).ToDictionary(s => s.Key, s => s.Value);
                    taskInfo.LastTime = DateTime.Now;
                    taskInfo.Procced = savedPaths.Count + resultDic.Count;
                    taskInfo.Error += notNullResultDic.Count;
                    _redisClient.Set($"{prefix}:Tid.{taskitem.TaskId}", JsonSerializer.Serialize(taskInfo));
                    _redisClient.SetRangeInHash($"{prefix}Result:Tid.{taskInfo.TaskId}", resultDic.ToDictionary(s => s.Key, s => (RedisValue)JsonSerializer.Serialize(s.Value)));

                    foreach (var item in resultDic)
                    {
                        var taskdetail = all.Where(s => s.RemoteImagePath == item.Key).FirstOrDefault();
                        if (taskdetail != null)
                        {
                            var datas = item.Value.Select(s => new BklInspectionTaskResult
                            {
                                Id = s.id,
                                DamageX = s.xmin.ToString("0.00"),
                                DamageY = s.ymin.ToString("0.00"),
                                DamageWidth = (s.xmax - s.xmin).ToString("0.00"),
                                DamageHeight = (s.ymax - s.ymin).ToString("0.00"),
                                DamageDescription = "systemgen",
                                DamageLevel = "",
                                DamagePosition = "",
                                DamageSize = "",
                                DamageType = s.name,
                                TreatmentSuggestion = "",
                                Createtime = DateTime.Now,
                                FacilityId = taskdetail.FacilityId,
                                FacilityName = taskdetail.FacilityName,
                                FactoryId = task.FactoryId,
                                FactoryName = task.FactoryName,
                                TaskId = task.Id,
                                TaskDetailId = taskdetail.Id,
                                Position = taskdetail.Position,
                            });
                            context.BklInspectionTaskResult.AddRange(datas);
                        }
                    }
                    context.SaveChanges();
                }

            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }

            await Task.Delay(5);
        }
    }
}
