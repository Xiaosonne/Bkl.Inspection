using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ELSegImageService : BackgroundService
{



    private ILogger<ELSegImage> _logger;
    private IServiceScope _scope;
    private IBackgroundTaskQueue<ELSegImage> _taskQueue;
    private BklConfig _config;
    private ELDetectHelper _detect;

    public ELSegImageService(
        BklConfig config,
        IServiceProvider serviceProvider,
         IBackgroundTaskQueue<ELSegImage> queue,
        ILogger<ELSegImage> logger)
    {
        _logger = logger;
        _scope = serviceProvider.CreateScope();
        _taskQueue = queue;
        _config = config;
        _detect = _scope.ServiceProvider.GetService<ELDetectHelper>();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redis = _scope.ServiceProvider.GetService<IRedisClient>();
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1);
            var task = await _taskQueue.DequeueAsync(stoppingToken);
            var result = default(AlignmentResult);
            try
            {
                if (task == null)
                    continue;
                result = await _detect.Extract(_config, task.RemotePath);
                if (result == null)
                {
                    await _taskQueue.EnqueueAsync(task);
                    continue;
                }
            }
            catch(Exception ex)
            {
                _logger.LogError($"ProcessError {ex}");
                continue;
            }
            try
            {
                redis.SetEntryInHash($"ELSegTaskResult:Fid.{task.FactoryId}.Tid.{task.TaskId}", task.TaskDetailId.ToString(), JsonSerializer.Serialize(result));
            }
            catch (Exception ex)
            {
                _logger.LogError($"RedisError {ex}");
                continue;
            }
        }
    }
}
