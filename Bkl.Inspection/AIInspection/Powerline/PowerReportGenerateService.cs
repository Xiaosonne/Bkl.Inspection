using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Bkl.Infrastructure;
using Bkl.Models;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class PowerReportGenerateService : BackgroundService
{
    public class PowerTask
    {
        public BklInspectionTask Task { get; set; }
        public BklFactory Factory { get; set; }
        public List<BklInspectionTaskDetail> TaskDetails { get; set; }
        public List<BklInspectionTaskResult> TaskResults { get; set; }
        public long ReportIndex { get; set; }
    }

    Channel<PowerTask> _powerTask;
    private BklConfig _config;
    private IServiceScope _scope;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var redis = _scope.ServiceProvider.GetService<IRedisClient>();
        while (stoppingToken.IsCancellationRequested == false)
        {
            var task = await _powerTask.Reader.ReadAsync();
            var factory = task.Factory;

            var dt = DateTime.Now;
            var name = $"{factory.FactoryName}-线路巡检报告-{DateTime.Now.ToString("yyyyMMddHHmmss")}.docx";
            Console.WriteLine(name);


      
            using MemoryStream ms = new MemoryStream();
            var create = new CreateWord();
            using (WordprocessingDocument word = WordprocessingDocument.Create(ms, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var maindoc = word.AddMainDocumentPart(); 
                create.CreateMainDocumentPart(
                    maindoc,
                    new CreatePowerlineExportParagraph(_config, word, redis, task.Task, task.ReportIndex, task.TaskDetails, task.TaskResults)

                );
                word.Save();
            }
  
            ms.Seek(0, SeekOrigin.Begin);
            try
            {
                var pt = System.IO.Path.Combine(_config.FileBasePath, "GenerateReports");
                if (!Directory.Exists(pt))
                {
                    Directory.CreateDirectory(pt);
                }
                var filename = System.IO.Path.Combine(pt, name);
                using (FileStream fs = new FileStream(filename, FileMode.OpenOrCreate))
                {
                    await ms.CopyToAsync(fs, 1024 * 1024 * 10);
                }
                redis.SetEntryInHash($"ReportResult:{task.Factory.Id}:{task.Task.Id}", task.ReportIndex.ToString(), filename);
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
            Console.WriteLine(name+" now "+DateTime.Now.Subtract(dt).TotalSeconds);

        }
    }

    public PowerReportGenerateService(BklConfig config, Channel<PowerTask> powertask, IServiceProvider serviceProvider, ILogger<DetectImageService> logger)
    {
        _powerTask = powertask;
        _config = config;
        _scope = serviceProvider.CreateScope();
    }

}
