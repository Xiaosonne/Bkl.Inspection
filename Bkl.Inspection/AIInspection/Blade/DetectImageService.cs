using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bkl.ESPS;
using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
public class DetectImageService : BackgroundService
{
    private BklConfig _config;
    private IServiceScope _scope;
    private IRedisClient _redisClient;
    private ILogger<DetectImageService> _logger;
    IBackgroundTaskQueue<DetectTaskInfo> _detectQueue;
    private IBackgroundTaskQueue<SegTaskInfo> _segQueue;

    public DetectImageService(BklConfig config, IServiceProvider serviceProvider, ILogger<DetectImageService> logger,
    IBackgroundTaskQueue<DetectTaskInfo> queue, IBackgroundTaskQueue<SegTaskInfo> segqueue)
    {
        _config = config;
        _scope = serviceProvider.CreateScope();
        _redisClient = _scope.ServiceProvider.GetService<IRedisClient>();
        _logger = logger;
        _detectQueue = queue;
        _segQueue = segqueue;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.Wait
        };
        var context = _scope.ServiceProvider.GetService<BklDbContext>();

        while (!stoppingToken.IsCancellationRequested)
        {
            var taskitem = await _detectQueue.DequeueAsync(stoppingToken);
            if (taskitem == null)
            {
                Thread.Sleep(1000);
            }

            try
            {
                var jsonStr = _redisClient.GetValueFromHash($"DetectTask:Tid.{taskitem.TaskId}", taskitem.FacilityId.ToString());
                var taskInfo = JsonSerializer.Deserialize<DetectTaskInfo>(jsonStr);
                var dbPaths = context.BklInspectionTaskDetail.Where(s => s.TaskId == taskitem.TaskId && s.FacilityId == taskInfo.FacilityId)
                     .Select(s => s.RemoteImagePath).ToList();
                var redisPaths = _redisClient.GetKeysFromHash($"DetectTaskResult:Tid.{taskInfo.TaskId}.Faid.{taskInfo.FacilityId}");

                var dbLeftPaths = dbPaths.Except(dbPaths.Where(redisPaths.Contains)).ToList();
                int i = 1;
                var sendToDefectPaths = dbLeftPaths.Take(20).ToList();

                var resultDic = dbLeftPaths.ToDictionary(s => s, s => (YoloResult[])null);

                while (sendToDefectPaths.Count > 0)
                {
                    Parallel.ForEach(sendToDefectPaths, (path1) =>
                    {
                        try
                        {
                            var result = DetectHelper.PathDetect(_config, taskInfo.TaskId, taskInfo.FacilityId, path1).GetAwaiter().GetResult();
                            resultDic[path1] = result;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError($"error for {path1} {ex.ToString()}");
                        }
                    });
                    sendToDefectPaths = dbPaths.Skip(i * 20).Take(20).ToList();
                    i++;

                    taskInfo.LastTime = DateTime.Now;
                    taskInfo.Procced = redisPaths.Count + (i + 1) * 20;

                    _redisClient.SetEntryInHash($"DetectTask:Tid.{taskitem.TaskId}", taskInfo.FacilityId.ToString(), JsonSerializer.Serialize(taskInfo));
                    _redisClient.Set($"DetectTaskRunning:Tid.{taskitem.TaskId}.Faid.{taskitem.FacilityId}", DateTime.Now.ToString(), 5 * 3600);
                    _logger.LogInformation($"task {taskitem.TaskId} {taskitem.FacilityId} result for {sendToDefectPaths.Count} total:{dbPaths.Count} cur:{i * 20}");
                }
                if (resultDic.Count > 0)
                {
                    var notNullResultDic = resultDic.Where(s => s.Value != null && s.Value.Count() > 0).ToDictionary(s => s.Key, s => s.Value);
                    taskInfo.Error += notNullResultDic.Count;
                    _redisClient.SetEntryInHash($"DetectTask:Tid.{taskitem.TaskId}", taskInfo.FacilityId.ToString(), JsonSerializer.Serialize(taskInfo));

                    _redisClient.SetRangeInHash($"DetectTaskResult:Tid.{taskInfo.TaskId}.Faid.{taskInfo.FacilityId}", resultDic.ToDictionary(s => s.Key, s => (RedisValue)JsonSerializer.Serialize(s.Value)));
                    var orderPath = _redisClient.GetValuesFromHash($"Task.{taskInfo.TaskId}:Facility.{taskInfo.FacilityId}").ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<BladeExtraInfo>((string)s.Value));
                    var filterResults = InspectionHelper.FilterResult(notNullResultDic, orderPath);
                    _redisClient.SetRangeInHash($"DetectTaskResult:Tid.{taskInfo.TaskId}.Faid.{taskInfo.FacilityId}.Filterd", filterResults.ToDictionary(s => s.Key, s => (RedisValue)(JsonSerializer.Serialize(s.Value))));

                    var keys = _redisClient.GetKeysFromHash($"DetectTaskResult:Tid.{taskInfo.TaskId}.Faid.{taskInfo.FacilityId}.Filterd");
                    taskInfo.Error = keys.Count;
                    _redisClient.SetEntryInHash($"DetectTask:Tid.{taskitem.TaskId}", taskInfo.FacilityId.ToString(), JsonSerializer.Serialize(taskInfo));
                }
                if (taskInfo.Procced == taskInfo.Total)
                {
                    var task = new SegTaskInfo
                    {
                        TaskId = taskitem.TaskId,
                        FacilityId = taskitem.FacilityId,
                        LastTime = DateTime.Now,
                    };
                    _redisClient.SetEntryInHashIfNotExists($"SegTask:Tid.{taskitem.TaskId}", taskitem.FacilityId.ToString(), JsonSerializer.Serialize(task));
                    _logger.LogInformation($"task {taskitem.TaskId} {taskitem.FacilityId} over ");
                    await _segQueue.EnqueueAsync(new SegTaskInfo { TaskId = taskInfo.TaskId, FacilityId = taskInfo.FacilityId });
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
