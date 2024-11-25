using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Minio;
using MySql.Data.MySqlClient;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bkl.Inspection
{
    public class ImageDirectory
    {
        public string Name { get; set; }
        public long ImageDirId { get; set; }

    }

    public class PowerLineCreateTower
    {
        public string TowerName { get; set; }
        public string KmzName { get; set; }
        public string DirName { get; set; }
        public string LineName { get; set; }

        public double TowerLat { get; set; }

        public double TowerLon { get; set; }

        public string[] Files { get; set; }

    }
    public record LevelConfig(string @class, string name, string level);

    [ApiController]
    [Route("[controller]")]
    public partial class PLInspectionController : Controller
    {
        public PLInspectionController(ILogger<PLInspectionController> logger) { _logger = logger; }

        string[] savingTags = new string[]
              {
                            "GpsLatitude",
                            "GpsLongitude",
                            "AbsoluteAltitude",
                            "RelativeAltitude",
                            "GimbalRollDegree",
                            "GimbalYawDegree",
                            "GimbalPitchDegree",
                            "FlightRollDegree",
                            "FlightYawDegree",
                            "FlightPitchDegree",
              };
        private ILogger<PLInspectionController> _logger;



        [HttpGet("thermal")]
        public async Task<JsonResult> GetThermalMeasure([FromServices] BklConfig config, [FromServices] IRedisClient redis, string filename, string cxy1, string cxy2)
        {
            var minio = new MinioClient()
                    .WithEndpoint(config.MinioConfig.EndPoint)
                    .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                    .WithRegion(config.MinioConfig.Region)
                    .Build();
            var tempFile = Guid.NewGuid().ToString();
            var exists = await minio.ObjectExists("power-line-small", filename + ".raw");


            if (!exists)
            {
                var pt1 = Path.Combine(config.FileBasePath, "TempFiles");
                if (!Directory.Exists(pt1))
                {
                    Directory.CreateDirectory(pt1);
                }

                var dstRaw = Path.Combine(config.FileBasePath, "TempFiles", tempFile + ".raw");
                var dstPic = Path.Combine(config.FileBasePath, "TempFiles", tempFile + ".JPG");

                using (var fs = new FileStream(dstPic, FileMode.CreateNew))
                {
                    await minio.ReadStream(filename, "power-line", stream => { stream.CopyTo(fs); return fs; });
                    fs.Flush();
                }

                //await minio.GetObjectAsync(new GetObjectArgs().WithBucket("power-line").WithObject(filename)
                //        .WithCallbackStream(stream =>
                //        {
                //            using (var fs = new FileStream(dstPic, FileMode.CreateNew))
                //            {
                //                stream.CopyTo(fs);
                //            }
                //        }));

                var exe = Path.Combine(config.DJIThermalTooPath ?? Directory.GetCurrentDirectory(), config.DJIThermalTooExe);

                DjiThermalMeasureTool.ParseThermalRaw(exe, dstPic, dstRaw);

                if (System.IO.File.Exists(dstRaw))
                {
                    await minio.UploadFile(dstRaw, filename + ".raw", "power-line-small");
                }
            }
            var stream = await minio.ReadStream(filename + ".raw", "power-line-small");
            if (stream == null)
            {
                return Json(new { error = 1 });
            }
            stream.Seek(0, SeekOrigin.Begin);
            float[][] data = DjiThermalMeasureTool.ReadThermal(stream, 640, 512);
            var p1 = cxy1.Split(",");
            var p2 = cxy2.Split(",");
            var x1 = float.Parse(p1[0]) * 640;
            var y1 = float.Parse(p1[1]) * 512;
            var x2 = float.Parse(p2[0]) * 640;
            var y2 = float.Parse(p2[1]) * 512;
            var k = (y2 - y1) / (x2 - x1);
            var b = y1 - k * x1;
            var y = (float x) => x * k + b;
            var minx = Math.Min(x1, x2);
            var maxx = Math.Max(x1, x2);
            List<float[]> lis = new List<float[]>();
            for (var x = minx; x <= maxx; x = x + 1)
            {
                var xm = (int)x; var ym = (int)y(x);
                lis.Add(new float[] { xm / 640f, ym / 512f, data[xm][ym] });
            }
            return Json(new
            {
                temp = lis,
                max = lis.OrderByDescending(s => s[2]).First(),
                min = lis.OrderBy(s => s[2]).First(),
                aveTemp = lis.Sum(s => s[2]) / lis.Count
            });
        }


        [AllowAnonymous]
        [DisableRequestSizeLimit]
        [HttpPost("upload-dirs")]
        public async Task<JsonResult> PostFiles(
           [FromServices] BklConfig config,
           [FromServices] IRedisClient redis,
           [FromForm(Name = "file")] List<IFormFile> files,
           [FromQuery] string bucketName = "power-line")
        {
            var minio = new MinioClient()
                       .WithEndpoint(config.MinioConfig.EndPoint)
                       .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                       .WithRegion(config.MinioConfig.Region)
                       .Build();

            await minio.CreateBucket(bucketName);
            await minio.CreateBucket(bucketName + "-small");
            Dictionary<string, Dictionary<string, string>> dics = new Dictionary<string, Dictionary<string, string>>();
            foreach (var f in files)
            {
                var exists = await minio.ObjectExists(bucketName, f.FileName);
                var existsSmall = await minio.ObjectExists(bucketName + "-small", f.FileName);

                //redis.SetIfNotExists($"ImageIdCache:{f.FileName}", SnowId.NextId().ToString());

                if (exists && existsSmall)
                {
                    continue;
                }
                try
                {

                    if (!exists)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            await f.CopyToAsync(ms);
                            ms.Seek(0, SeekOrigin.Begin);
                            var imgAttrs = GetImageMeta(ms);
                            ms.Seek(0, SeekOrigin.Begin);

                            //Version,ImageSource,GpsStatus,AltitudeType,GpsLatitude,GpsLongitude,AbsoluteAltitude,RelativeAltitude,GimbalRollDegree,GimbalYawDegree,GimbalPitchDegree,FlightRollDegree,FlightYawDegree,FlightPitchDegree,FlightXSpeed,FlightYSpeed,FlightZSpeed,CamReverse,GimbalReverse,SelfData,RtkFlag,RtkStdLon,RtkStdLat,RtkStdHgt,RtkDiffAge,NTRIPMountPoint,NTRIPPort,NTRIPHost,SurveyingMode,UTCAtExposure,CameraSerialNumber,DroneModel,DroneSerialNumber,LRFStatus,LRFTargetDistance,LRFTargetLon,LRFTargetLat,LRFTargetAlt,LRFTargetAbsAlt,PictureQuality,lat,lon,alt
                            var putArgs = new PutObjectArgs()
                                    .WithObject(f.FileName)
                                    .WithObjectSize(ms.Length)
                                    .WithBucket(bucketName)
                                    .WithStreamData(ms);
                            if (imgAttrs != null && imgAttrs.Count > 0)
                            {
                                putArgs.WithTagging(new Minio.DataModel.Tags.Tagging(imgAttrs.Where(s => savingTags.Contains(s.Key))
                                    .ToDictionary(s => s.Key, s => s.Value), true));
                            }
                            await minio.PutObjectAsync(putArgs);
                            redis.SetRangeInHash($"PowerLine:{f.FileName}", imgAttrs.ToDictionary(s => s.Key, s => (RedisValue)s.Value));
                            dics.Add(f.FileName, imgAttrs);
                        }


                    }

                    if (!existsSmall)
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            await f.CopyToAsync(ms);
                            ms.Seek(0, SeekOrigin.Begin);


                            using (var mssmall = new MemoryStream())
                            {
                                using (var img = Image.Load(ms))
                                {
                                    img.Mutate(ctx =>
                                    {
                                        ctx.Resize(new Size(200, 150));
                                    });
                                    img.SaveAsJpeg(mssmall, new JpegEncoder() { Quality = 90 });
                                }
                                mssmall.Seek(0, SeekOrigin.Begin);
                                var argssmall = new PutObjectArgs()
                                       .WithObject(f.FileName)
                                       .WithObjectSize(mssmall.Length)
                                       .WithBucket(bucketName + "-small")
                                       .WithStreamData(mssmall);

                                await minio.PutObjectAsync(argssmall);
                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "上传出错");
                }


            }
            return (Json(dics));
        }


        [HttpPost("pre-import")]
        public async Task<IActionResult> PreCreateTower(
                    [FromServices] BklConfig config,
                    [FromServices] IRedisClient redis,
                    [FromServices] BklDbContext context,
                    [FromServices] LogonUser user,
                    [FromQuery] long factoryId,
                    [FromQuery] long taskId,
                    [FromBody] PowerLineCreateTower[] lineCreateTowers)
        {
            var task = context.BklInspectionTask.Where(s => s.FactoryId == factoryId && s.Id == taskId).FirstOrDefault();
            if (task == null)
            {
                return BadRequest("找不到巡检任务");
            }
            var dbFacilities = context.BklFactoryFacility.Where(s => s.FactoryId == factoryId).ToList();
            var notindbfaci = lineCreateTowers.Where(s => dbFacilities.All(q => q.Name != s.TowerName)).ToList();
            List<BklFactoryFacility> items = new List<BklFactoryFacility>();
            StringBuilder sb = new StringBuilder();

            foreach (var faci in notindbfaci)
            {
                var id = SnowId.NextId();
                if (false == redis.SetIfNotExists($"FacilityIdCache:{factoryId}:{faci.TowerName}", id.ToString()))
                {
                    id = long.Parse(redis.Get($"FacilityIdCache:{factoryId}:{faci.TowerName}"));
                }
                var ffa = new BklFactoryFacility
                {
                    Id = id,
                    Createtime = DateTime.Now,
                    CreatorId = user.userId,
                    CreatorName = user.name,
                    FacilityType = "Tower",
                    FactoryId = factoryId,
                    FactoryName = task.FactoryName,
                    Name = faci.TowerName,
                    GPSLocation = $"[{faci.TowerLat},{faci.TowerLon}]",
                };
                items.Add(ffa);
            }
            List<string> facilities = new List<string>();
            facilities = items.Aggregate(facilities, (pre, cur) =>
            {
                pre.Add($"{cur.Id}\t" +
                    $"{cur.Name}\t" +
                    $"{cur.FacilityType}\t" +
                    $"{cur.FactoryId}\t" +
                    $"{cur.FactoryName}\t" +
                    $"{cur.CreatorId}\t" +
                    $"{cur.CreatorName}\t" +
                    $"{cur.GPSLocation}\t" +
                    $"{cur.Createtime}");
                return pre;
            });

            items.AddRange(dbFacilities);
            List<string> taskDetails = new List<string>();
            foreach (var faci in lineCreateTowers)
            {
                var ffa = items.Where(s => s.Name == faci.TowerName).FirstOrDefault();
                taskDetails = faci.Files.Aggregate(taskDetails, (rets, filename) =>
                  {
                      //var id = redis.Get($"ImageIdCache:{filename}").ToString();
                      var id = SnowId.NextId().ToString();
                      rets.Add($"{id},{taskId},{factoryId},{task.FactoryName},{ffa.Id},{ffa.Name},#,#,0,{filename},image/jpeg,4000,3000,{DateTime.Now}");
                      return rets;
                  });
            }
            var col1 = new string[] {
                "Id",
                "TaskId",
                "FactoryId",
                "FactoryName",
                "FacilityId",
                "FacilityName",
                "Position",
                "LocalImagePath",
                "Error",
                "RemoteImagePath",
                "ImageType",
                "ImageWidth",
                "ImageHeight",
                "Createtime"
            };
            var col2 = new string[] {
                "Id",
                "Name",
                "FacilityType",
                "FactoryId",
                "FactoryName",
                "CreatorId",
                "CreatorName",
                "GPSLocation",
                "Createtime"
            };

            return Json(new
            {
                facilityHeaders = col2,
                facilityData = facilities,
                detailHeaders = col1,
                detailData = taskDetails
            });
        }
        public class ImportFacilityRequest
        {
            public string name { get; set; }
            public double lat { get; set; }
            public double lon { get; set; }
        }
        [HttpPost("import-facilities")]
        public async Task<IActionResult> ImportFacilities([FromServices] BklDbContext context, [FromServices] LogonUser user, [FromQuery] long factoryId, [FromBody] ImportFacilityRequest[] requests)
        {
            var fac = context.BklFactory.Where(s => s.Id == factoryId).FirstOrDefault();
            var facis = context.BklFactoryFacility.Where(s => s.FactoryId == factoryId).ToList();
            foreach (var item in requests)
            {
                var faci = facis.FirstOrDefault(s => s.Name == item.name);
                if (faci == null)
                {
                    context.BklFactoryFacility.Add(new BklFactoryFacility
                    {
                        CreatorId = user.userId,
                        FactoryId = factoryId,
                        FactoryName = fac.FactoryName,
                        FacilityType = "Tower",
                        Name = item.name,
                        GPSLocation = $"[{item.lat},{item.lon}]",
                        Createtime = DateTime.Now,
                        CreatorName = user.name,
                        Id = SnowId.NextId(),
                    });
                }
                else
                {
                    faci.GPSLocation = $"[{item.lat},{item.lon}]";
                }
            }
            await context.SaveChangesAsync();
            return Json(new GeneralResponse { error = 0 });
        }

        [HttpPost("import")]
        public async Task<IActionResult> CreateTowers([FromServices] BklConfig config,
                    [FromServices] IRedisClient redis,
                    [FromServices] BklDbContext context,
                    [FromServices] LogonUser user,
                    [FromQuery] long factoryId,
                    [FromQuery] long taskId,
                    [FromBody] PowerLineCreateTower[] lineCreateTowers)
        {

            var task = context.BklInspectionTask.Where(s => s.FactoryId == factoryId && s.Id == taskId).FirstOrDefault();
            if (task == null)
            {
                return BadRequest("找不到巡检任务");
            }

            var minio = new MinioClient()
                    .WithEndpoint(config.MinioConfig.EndPoint)
                    .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                    .WithRegion(config.MinioConfig.Region)
                    .Build();


            var dbFacilities = context.BklFactoryFacility.Where(s => s.FactoryId == factoryId).ToList();
            var notindbfaci = lineCreateTowers.Where(s => dbFacilities.All(q => q.Name != s.TowerName)).ToList();
            List<BklFactoryFacility> items = new List<BklFactoryFacility>();
            StringBuilder sb = new StringBuilder();
            foreach (var indbFac in dbFacilities.Where(s => lineCreateTowers.Any(q => q.TowerName == s.Name)))
            {
                var updateInfo = lineCreateTowers.FirstOrDefault(r => r.TowerName == indbFac.Name); ;
                indbFac.GPSLocation = $"[{updateInfo.TowerLat},{updateInfo.TowerLon}]";
            }
            foreach (var faci in notindbfaci)
            {
                var id = SnowId.NextId();
                if (false == redis.SetIfNotExists($"FacilityIdCache:{factoryId}:{faci.TowerName}", id.ToString()))
                {
                    id = long.Parse(redis.Get($"FacilityIdCache:{factoryId}:{faci.TowerName}"));
                }
                if (items.All(q => q.Id != id))
                {
                    var ffa = new BklFactoryFacility
                    {
                        Id = id,
                        Createtime = DateTime.Now,
                        CreatorId = user.userId,
                        CreatorName = user.name,
                        FacilityType = "Tower",
                        FactoryId = factoryId,
                        FactoryName = task.FactoryName,
                        Name = faci.TowerName,
                        GPSLocation = $"[{faci.TowerLat},{faci.TowerLon}]",
                    };
                    items.Add(ffa);
                }
                redis.SetEntryInHash($"FacilityMeta:{id}", "kmzName", faci.KmzName);
                redis.SetEntryInHash($"FacilityMeta:{id}", "gps", $"[{faci.TowerLat},{faci.TowerLon}]");

            }

            context.BklFactoryFacility.AddRange(items);
            int faciInsert = await context.SaveChangesAsync();
            dbFacilities.AddRange(items);

            foreach (var faci in lineCreateTowers)
            {
                var dbFacility = dbFacilities.Where(s => s.Name == faci.TowerName).FirstOrDefault();
                //绑定facilityId信息
                await minio.SetObjectTagsAsync(new SetObjectTagsArgs()
                    .WithBucket($"power-waylines")
                    .WithObject($"fac{dbFacility.FactoryId}task{taskId}/{faci.KmzName}")
                    .WithTagging(new Minio.DataModel.Tags.Tagging(new Dictionary<string, string> {
                        {"facilityId",dbFacility.Id.ToString() },
                        {"towerName",dbFacility.Name },
                    }, false)));

                using var kmzstream = await minio.ReadStream($"fac{factoryId}task{taskId}/{faci.KmzName}", "power-waylines");
                kmzstream.Seek(0, SeekOrigin.Begin);

                var names = faci.Files.Where(s => s.EndsWith("V.JPG") || s.EndsWith("T.JPG") || s.EndsWith("Z.JPG"))
                    .Select(t => new DJIImageName(t)).ToList();
                List<DJIWayPoint> marks = LoadKmz(kmzstream);
                names.NamesFillPoint(marks);
                names.OrderBy(s => s.Order)
                     .Aggregate(sb, (pre, cur) =>
                     {
                         var pos = cur.PointName;
                         var path = cur.Raw;
                         var id = SnowId.NextId().ToString();
                         pre.Append($"{id},{taskId},{factoryId},{task.FactoryName},{dbFacility.Id},{dbFacility.Name},{pos},{faci.KmzName},0,{path},image/jpeg,4000,3000,{DateTime.Now}\n");
                         return pre;
                     });
            }
            var total = lineCreateTowers.Sum(s => s.Files.Length);

            using MemoryStream ms = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
            MySqlBulkLoader loader = new MySqlBulkLoader(new MySqlConnection(config.DatabaseConfig.GetConnectionString() + ";AllowLoadLocalInFile=true"))
            {
                LineTerminator = "\\n",
                FieldTerminator = ",",
                TableName = "bkl_inspection_task_detail",
                NumberOfLinesToSkip = 0,
                FileName = "",
            };
            loader.Columns.AddRange(new string[] {
                "Id",
                "TaskId",
                "FactoryId",
                "FactoryName",
                "FacilityId",
                "FacilityName",
                "Position",
                "LocalImagePath",
                "Error",
                "RemoteImagePath",
                "ImageType",
                "ImageWidth",
                "ImageHeight",
                "Createtime"
            });

            int retAll = 0;
            try
            {
                retAll = loader.Load(ms);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
            return Json(new { allcount = total, detailcount = retAll, allfacicount = items.Count, facicount = faciInsert });
            // INSERT INTO test.bkl_inspection_task_detail
            // (Id, TaskId, FactoryId, FactoryName, FacilityId, FacilityName, `Position`, LocalImagePath, Error, RemoteImagePath, ImageType, ImageWidth, ImageHeight, Createtime)
            // VALUES(0, 0, 0, '', 0, '', '', '', 0, '', '', '', '', '');


        }



        [HttpGet("task-detail-and-error")]
        public IActionResult GetTaskDetailAndError(
                    [FromServices] LogonUser user,
                    [FromServices] IRedisClient redis,
                    [FromServices] BklDbContext context,
                    [FromQuery] long taskId,
                    [FromQuery] long facilityId = 0,
                    [FromQuery] long taskDetailId = 0,
                    [FromQuery] int page = 1,
                    [FromQuery] int pagesize = 1500)
        {
            var query = (from p in context.BklInspectionTaskDetail
                         join q in context.BklInspectionTaskResult on p.Id equals q.TaskDetailId
                         into qs
                         from r in qs.DefaultIfEmpty()
                         where p.TaskId == taskId && (facilityId == 0 || p.FacilityId == facilityId) &&
                         (taskDetailId == 0 || p.Id == taskDetailId)
                         select new { detailId = p.Id, detail = p, error = r });
            var joinResult = query.Skip((page - 1) * pagesize)
                              .Take(pagesize)
                              .ToList();
            var results = (from s in joinResult
                           group s by s.detailId into gp1
                           select new { detail = gp1.First().detail, errors = gp1.Where(q => q.error != null).Select(r => r.error).ToArray() }).ToList();

            var filterDict = redis.GetValuesFromHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}.Filterd")
                                    .ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<YoloResult[]>(s.Value.ToString()));

            var EmptyArray = new YoloResult[0] { };
            var ret = results.Select(result =>
            {
                //redis.GetValuesFromHash("PowerLine:");
                dynamic obj = new ExtensionDynamicObject(result.detail);
                obj.errors = result.errors;
                obj.yoloResult = filterDict.TryGetValue(result.detail.RemoteImagePath, out var re) ? re : EmptyArray;
                return obj;
            }).ToArray();
            if (pagesize == 1500)
            {
                var content = JsonSerializer.Serialize(ret, new JsonSerializerOptions
                {
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return new ContentResult()
                {
                    Content = content,
                    ContentType = "application/json"
                };
            }
            else
            {
                var content = JsonSerializer.Serialize(new
                {
                    page = page,
                    pagesize = pagesize,
                    total = query.Count(),
                    data = ret
                }, new JsonSerializerOptions
                {
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return new ContentResult()
                {
                    Content = content,
                    ContentType = "application/json"
                };
            }

        }
        [HttpDelete("facility")]
        public async Task<IActionResult> DeleteFacility([FromServices] IRedisClient redis,
                 [FromServices] BklDbContext context, [FromQuery] long id, [FromQuery] long taskId)
        {
            var faci = context.BklFactoryFacility.Where(s => s.Id == id).FirstOrDefault();
            if (faci == null)
                return NotFound();
            using (var tran = await context.Database.BeginTransactionAsync())
            {
                try
                {
                    var details = context.BklInspectionTaskDetail.Where(s => s.TaskId == taskId && s.FacilityId == id).ToList();
                    var errors = context.BklInspectionTaskResult.Where(s => s.TaskId == taskId && s.FacilityId == id).ToList();

                    context.BklInspectionTaskDetail.RemoveRange(details);
                    context.BklInspectionTaskResult.RemoveRange(errors);
                    if (context.BklInspectionTaskDetail.Count(s => s.FacilityId == id) == 0)
                    {
                        context.BklFactoryFacility.Remove(faci);
                    }
                    await context.SaveChangesAsync();
                    await tran.CommitAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                    tran.Rollback();
                }
            }

            return Ok();
        }

        [HttpGet("facilities")]
        public IActionResult ListFacilities([FromServices] BklDbContext context, [FromServices] LogonUser user, [FromServices] IRedisClient redis, [FromQuery] long factoryId = 0, [FromQuery] long taskId = 0)
        {
            var facid = factoryId == 0 ? user.factoryId : factoryId;
            var arr = context.BklFactoryFacility.Where(p => p.FacilityType == "Tower" && p.FactoryId == facid).ToList();

            List<long> faids = arr.Select(s => s.Id).ToList();
            if (taskId != 0)
            {
                var facilis = context.BklInspectionTaskDetail.Where(s => s.FactoryId == facid && s.TaskId == taskId)
                .GroupBy(s => s.FacilityId)
                .Select(s => s.Max(q => q.FacilityId))
                .ToList();
                faids = arr.Select(s => s.Id).ToList().Intersect(facilis).ToList();
            }
            if (faids.Count > 0)
            {
                var rets = arr.Where(s => faids.Contains(s.Id)).Select(s =>
                {
                    var dic = redis.GetValuesFromHash($"FacilityMeta:{s.Id}").ToDictionary(s => s.Key, s => (string)s.Value);
                    dynamic doo = new ExtensionDynamicObject(s);

                    foreach (var item in dic)
                    {
                        doo[item.Key] = item.Value;
                    }
                    return doo;
                });
                var content = JsonSerializer.Serialize(rets, new JsonSerializerOptions
                {
                    DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                return new ContentResult()
                {
                    Content = content,
                    ContentType = "application/json"
                };
            }
            else
            {
                return Json(new int[0]);
            }

        }


    }
}