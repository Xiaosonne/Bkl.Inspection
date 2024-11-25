using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Minio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace Bkl.Inspection
{
    [ApiController]
    [AllowAnonymous]
    [Route("[controller]")]
    public class AudioInsController : Controller
    {
        public AudioInsController() { }

        [HttpGet("init")]
        public async Task<JsonResult> GetInitDir([FromServices] BklConfig config, [FromServices] BklDbContext context, [FromServices] IRedisClient redis, [FromQuery] long factoryId)
        {
            var bucketname = "audiosource-" + factoryId.ToString();
            var minio = new MinioClient()
                               .WithEndpoint(config.MinioConfig.EndPoint)
                               .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                               .WithRegion(config.MinioConfig.Region)
                               .Build();
            redis.SetEntryInHash("AudioBucketMap", factoryId.ToString(), bucketname);
            await minio.CreateBucket(bucketname);
            var arr = context.BklFactoryFacility.Where(s => s.FactoryId == factoryId).Select(s => new { s.Id, s.Name }).ToList();
            foreach (var item in arr)
            {
                await minio.WriteObject(DateTime.Now.ToString(), $"{item.Name}/init", bucketname);
            }

            return Json("");
        }
        [AllowAnonymous]
        [HttpGet("dir/{factoryId}/{facilityId}")]
        public async Task<JsonResult> GetAudio(
            [FromServices] BklConfig config,
            [FromRoute] long factoryId,
            [FromRoute] long facilityId,
            [FromQuery] string prefix)
        {

            var minio = new MinioClient()
                  .WithEndpoint(config.MinioConfig.EndPoint)
                  .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                  .WithRegion(config.MinioConfig.Region)
                  .Build();
            var obj = minio.ListObjectsAsync(new ListObjectsArgs().WithBucket("work-data").WithPrefix($"audio/").WithRecursive(false));
            List<object> tags = new List<object>();
            TaskCompletionSource<List<object>> tccc = new TaskCompletionSource<List<object>>();
            obj.Subscribe(item =>
            {
                if (item.IsDir)
                {
                    tags.Add(new
                    {
                        isDir = item.IsDir,
                        name = item.Key,
                    });
                }
            }, () =>
            {
                tccc.SetResult(tags);
            });
            await tccc.Task;
            return Json(tags);
        }

        [AllowAnonymous]
        [HttpGet("audio")]
        public async Task<JsonResult> GetAudio([FromServices] BklConfig config, [FromQuery] long factoryId)
        {

            var minio = new MinioClient()
                  .WithEndpoint(config.MinioConfig.EndPoint)
                  .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                  .WithRegion(config.MinioConfig.Region)
                  .Build();
            var obj = minio.ListObjectsAsync(new ListObjectsArgs()
                .WithBucket("work-data")
                .WithPrefix("audio/")
                .WithRecursive(false));
            List<string> tags = new List<string>();
            TaskCompletionSource<List<string>> tccc = new TaskCompletionSource<List<string>>();
            obj.Subscribe(item =>
            {
                if (item.IsDir)
                {
                    tags.Add(item.Key);
                }
            }, () =>
            {
                tccc.SetResult(tags);
            });
            await tccc.Task;
            return Json(tags);
        }
        [AllowAnonymous]
        [HttpGet("audio/{date}")]
        public async Task<JsonResult> GetAudio([FromServices] BklConfig config, [FromRoute] string date, [FromQuery] long factoryId)
        {
            var minio = new MinioClient()
                  .WithEndpoint(config.MinioConfig.EndPoint)
                  .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                  .WithRegion(config.MinioConfig.Region)
                  .Build();
            var obj = minio.ListObjectsAsync(new ListObjectsArgs().WithBucket("work-data").WithPrefix($"audio/{date}").WithRecursive(true));
            List<string> tags = new List<string>();
            TaskCompletionSource<List<string>> tccc = new TaskCompletionSource<List<string>>();
            obj.Subscribe(item =>
            {
                tags.Add(item.Key);
            }, () =>
            {
                tccc.SetResult(tags);
            });
            await tccc.Task;
            return Json(tags);
        }
    }

}