using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class FuseImageService : BackgroundService
{
	private IServiceProvider _serviceProvider;
	private BklConfig _config;
	private IServiceScope _scope;
	private IBackgroundTaskQueue<FuseTaskInfo> _taskQueue;
	private IRedisClient _redisClient;
	private ILogger<FuseImageService> _logger;
	public FuseImageService(
		BklConfig config,
		IServiceProvider serviceProvider,
		IBackgroundTaskQueue<FuseTaskInfo> taskQueue,
		ILogger<FuseImageService> logger)
	{
		_serviceProvider = serviceProvider;
		_config = config;
		_scope = serviceProvider.CreateScope();
		_taskQueue = taskQueue;
		_redisClient = _scope.ServiceProvider.GetService<IRedisClient>();
		_logger = logger;
	}

	public class BladePathOrder
	{
		public string pos { get; set; }
		public string path { get; set; }
		public int order { get; set; }
		public static BladePathOrder ParseOrder(BladePathOrder s)
		{
			var arr = s.path.Split('_');
			var ok = int.TryParse(arr.Length > 2 ? arr[2] : "0", out var order);
			s.order = ok ? order : 0;
			return s;
		}
	}
	static HttpClient client = new HttpClient();
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		BladePathOrder parseOrder(BladePathOrder s)
		{
			var arr = s.path.Split('_');
			var ok = int.TryParse(arr.Length > 2 ? arr[2] : "0", out var order);
			s.order = ok ? order : 0;
			return s;
		}
		var context = _scope.ServiceProvider.GetService<BklDbContext>();
		while (!stoppingToken.IsCancellationRequested)
		{
			await Task.Delay(1000);
			var fuseImageTask = await _taskQueue.DequeueAsync(stoppingToken);
			if (fuseImageTask == null)
				continue;

			var taskId = fuseImageTask.TaskId;
			var facilityId = fuseImageTask.FacilityId;
			_logger.LogInformation($"StartFuseTask {taskId} {facilityId}");
			try
			{


				List<BladePathOrder> dbPics = null;
				//获取所有的图片
				dbPics = context.BklInspectionTaskDetail
									.Where(s => s.FacilityId == facilityId && s.TaskId == taskId)
									.Select(s => new BladePathOrder { pos = s.Position, path = s.RemoteImagePath })
									.ToList()
									.Select(BladePathOrder.ParseOrder)
									.ToList();
				//获取已经分割的产品
				var segResults = _redisClient.GetKeysFromHash($"SegTaskResult:Tid.{taskId}.Faid.{facilityId}");
				//获取已经检测过的
				var detectResults = _redisClient.GetKeysFromHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}.Filterd");
				//可用的
				var used = dbPics.Select(s => s.path).Intersect(segResults)
					.Select(s => dbPics.First(k => k.path == s)).ToList();

				var orderPath = _redisClient.GetValuesFromHash($"Task.{taskId}:Facility.{facilityId}").ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<BladeExtraInfo>((string)s.Value));
				_logger.LogInformation($"FuseTask {taskId} {facilityId} seg:{segResults.Count} detect:{detectResults.Count} used:{used.Count}");
				foreach (var samePaths in used.GroupBy(s => s.pos))
				{
					if (!orderPath.ContainsKey(samePaths.Key))
					{
						continue;
					}
					var bladeInfo = orderPath[samePaths.Key];
					int orderreverse = bladeInfo.EndIndex > bladeInfo.StartIndex ? 1 : -1;
					//选择有缺陷结果的先加进来
					var fuseSeq = samePaths.Select(s => s.path).Intersect(segResults).Select(s => samePaths.First(k => k.path == s)).ToList().OrderBy(s => orderreverse * s.order).ToList();

					//if (fuseSeq.Count == 0)
					//{
					//    fuseSeq.Add(samePaths.First());
					//}
					//HashSet<int> hash = new HashSet<int>();

					//foreach (var item in samePaths.OrderBy(s => s.order))
					//{
					//    if (!hash.Contains(item.order))
					//    {
					//        for (int i = 0; i < fuseSeq.Count; i++)
					//        {
					//            if ( (Math.Abs(item.order - fuseSeq[i].order)%2)==0 )
					//            {
					//                hash.Add(item.order);
					//                fuseSeq.Add(item);
					//                fuseSeq = fuseSeq.OrderBy(s => s.order).ToList();
					//                break;
					//            }
					//        }
					//    }
					//}

					//fuseSeq = fuseSeq.OrderByDescending(q => orderreverse * q.order).ToList();
					fuseImageTask.LastTime = DateTime.Now;
					_redisClient.SetEntryInHash($"FuseTask:Tid.{taskId}", facilityId.ToString(), JsonSerializer.Serialize(fuseImageTask));
					try
					{
						_logger.LogInformation("Fuse Image Service " + string.Join(",", fuseSeq.Select(s => s.path)));
						var result = await DetectHelper.FuseImages(taskId, facilityId, fuseSeq.Select(s => s.path).ToArray(), samePaths.Key);
						if (result.state == null)
						{
							_redisClient.SetEntryInHash($"FuseTaskResult:Tid.{taskId}.Faid.{facilityId}", samePaths.Key, JsonSerializer.Serialize(result));
						}
						_logger.LogInformation("Fuse Image Service Result " + result.state + " " + result.save_path);
					}
					catch (Exception ex)
					{
						_logger.LogError(ex.ToString());
					}
				}
			}
			catch (Exception ex)
			{
				_logger.LogError(ex.ToString());
			}
		}
	}
}
