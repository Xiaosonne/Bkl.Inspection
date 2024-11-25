using System;
using System.Collections.Generic;
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
using StackExchange.Redis;

public class SegImageService : BackgroundService
{
	private IServiceProvider _serviceProvider;
	private BklConfig _config;
	private IServiceScope _scope;
	private IRedisClient _redisClient;
	private ILogger<DetectImageService> _logger;
	private IBackgroundTaskQueue<SegTaskInfo> _segQueue;
	private IBackgroundTaskQueue<FuseTaskInfo> _fuseQueue;

	public SegImageService(
		BklConfig config,
		IServiceProvider serviceProvider,
		ILogger<DetectImageService> logger,
		IBackgroundTaskQueue<SegTaskInfo> queue,
		IBackgroundTaskQueue<FuseTaskInfo> fuseQueue
		)
	{
		_serviceProvider = serviceProvider;
		_config = config;
		_scope = serviceProvider.CreateScope();

		_redisClient = _scope.ServiceProvider.GetService<IRedisClient>();
		_logger = logger;
		_segQueue = queue;
		_fuseQueue = fuseQueue;
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
			var taskitem = await _segQueue.DequeueAsync(stoppingToken);
			if (taskitem == null)
			{
				Thread.Sleep(1000);
			}

			try
			{
				_logger.LogInformation("StartSegService " + $"SegTask:Tid.{taskitem.TaskId}"); ;
				var jsonStr = _redisClient.GetValueFromHash($"SegTask:Tid.{taskitem.TaskId}", taskitem.FacilityId.ToString());
				var taskInfo = JsonSerializer.Deserialize<DetectTaskInfo>(jsonStr);
				var paths = context.BklInspectionTaskDetail.Where(s => s.TaskId == taskitem.TaskId && s.FacilityId == taskInfo.FacilityId).Select(s => s.RemoteImagePath).ToList();
				var savedPaths = _redisClient.GetKeysFromHash($"SegTaskResult:Tid.{taskInfo.TaskId}.Faid.{taskInfo.FacilityId}");
				var notSavedPath = paths.Except(paths.Where(savedPaths.Contains)).ToList();
				int i = 1;
				var allPath = notSavedPath.Take(20).ToList();
				var resultDic = notSavedPath.ToDictionary(s => s, s => (int[][])null);
				while (allPath.Count > 0)
				{
					Parallel.ForEach(allPath, (path1) =>
					{
						try
						{
							var result = DetectHelper.Seg(_config, taskInfo.TaskId, taskInfo.FacilityId, path1).GetAwaiter().GetResult();
							resultDic[path1] = result;

						}
						catch (Exception ex)
						{
							_logger.LogError($"error for {path1} {ex.ToString()}");
						}
					});
					allPath = notSavedPath.Skip(i * 20).Take(20).ToList();
					i++;
					_redisClient.Set($"SegTaskRunning:Tid.{taskitem.TaskId}.Faid.{taskitem.FacilityId}", DateTime.Now.ToString(), 5 * 3600);
					_logger.LogInformation($"segtask {taskitem.TaskId} {taskitem.FacilityId} total:{resultDic.Where(s => s.Value != null && s.Value.Count() > 0).Count()}");

					taskInfo.LastTime = DateTime.Now;
					taskInfo.Total = paths.Count;
					taskInfo.Procced = savedPaths.Count + (i + 1) * 20;
					taskInfo.Error = paths.Count - savedPaths.Count + resultDic.Count;
					_redisClient.SetEntryInHash($"SegTask:Tid.{taskitem.TaskId}", taskInfo.FacilityId.ToString(), JsonSerializer.Serialize(taskInfo));
				}

				if (resultDic.Count > 0)
				{
					taskInfo.LastTime = DateTime.Now;
					taskInfo.Total = paths.Count;
					taskInfo.Procced = savedPaths.Count + resultDic.Count;
					taskInfo.Error = paths.Count - savedPaths.Count + resultDic.Count;

					Dictionary<string, RedisValue> values = resultDic.ToDictionary(s => s.Key, s => (RedisValue)(s.Value == null ? "[]" : JsonSerializer.Serialize(s.Value)));
					_redisClient.SetRangeInHash($"SegTaskResult:Tid.{taskInfo.TaskId}.Faid.{taskInfo.FacilityId}", values);
					_redisClient.SetEntryInHash($"SegTask:Tid.{taskitem.TaskId}", taskInfo.FacilityId.ToString(), JsonSerializer.Serialize(taskInfo));
				}
				if (taskInfo.Procced >= taskInfo.Total)
				{
					await _fuseQueue.EnqueueAsync(new FuseTaskInfo { TaskId = taskInfo.TaskId, FacilityId = taskInfo.FacilityId });
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
