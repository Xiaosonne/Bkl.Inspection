using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
public class BladeReportGenerateService : BackgroundService
{
	private BklConfig _config;
	IServiceScope _scope;
	public BladeReportGenerateService(IBackgroundTaskQueue<GenerateAllTaskRequest> queue, BklConfig config, IServiceProvider serviceProvider, ILogger<BladeReportGenerateService> logger)
	{
		_config = config;
		_scope = serviceProvider.CreateScope();

		_redisClient = _scope.ServiceProvider.GetService<IRedisClient>();
		_logger = logger;
		TaskQueue = queue;
	}

	private IRedisClient _redisClient;

	private ILogger<BladeReportGenerateService> _logger;

	public IBackgroundTaskQueue<GenerateAllTaskRequest> TaskQueue { get; private set; }

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		while (!stoppingToken.IsCancellationRequested)
		{
			var taskItem = await TaskQueue.DequeueAsync(stoppingToken);
			try
			{
				var result = await ReportHelper.BladeReport(_config, _redisClient, taskItem);
				_logger.LogInformation($"taskresult {JsonSerializer.Serialize(result)}");
				result.SetValue(_redisClient);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "ReportGenerateService Error");
			}
		}
	}

}