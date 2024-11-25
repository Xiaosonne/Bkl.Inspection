using Bkl.Infrastructure;
using Bkl.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Minio;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using ImageMagick;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Text;

namespace Bkl.Inspection
{

    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class ImageController : Controller
    {


        //url -L "https://m.earthol.me/map.jpg?lyrs=y&gl=cn&x=27327&y=12726&z=15" -o "300000891.jpg" 
        //--create-dirs -H 
        // "Accept: image/*,*/*;q=0.8" -H 
        //"Connection: keep-alive" -H 
        //"Accept-Encoding: gzip, deflate, sdch" 
        //-H "Referer: https://www.earthol.com/g/" 
        //-H "Accept-Language: zh-CN,en,en-GB,en-US;q=0.8" -H 
        //"User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/107.0.0.0 Safari/537.36 Edg/107.0.1418.26" 
        static HttpClient client = new HttpClient(new HttpClientHandler()
        {
            UseProxy = false,
            Proxy = null,
            //CookieContainer = new System.Net.CookieContainer()
        });

        private IServiceProvider _serviceProvider;
        private ILogger<ImageController> _logger;
        private BklDbContext _context;
        public ImageController(BklDbContext context, IServiceProvider serviceProvider,
            IBackgroundTaskQueue<GenerateAllTaskRequest> taskqueue,
            IBackgroundTaskQueue<DetectTaskInfo> taskInfoQueue,
            ILogger<ImageController> logger)
        {
            this._serviceProvider = serviceProvider;
            this._context = context;
            this._logger = logger;
        }

        static ImageController()
        {
            // client.DefaultRequestHeaders.Referrer = new Uri("https://www.earthol.com/g/");
            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (iPad; U; CPU OS 4_3_3 like Mac OS X; en-us) AppleWebKit/533.17.9 (KHTML, like Gecko) Version/5.0.2 Mobile/8J2 Safari/6533.18.5");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encodings", "zh-CN,en,en-GB,en-US;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "image/*,*/*;");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://www.earthol.com/g/");
        }

        [HttpGet("proxy-google-tile")]
        [AllowAnonymous]
        public async Task<IActionResult> GetGoogleTile([FromServices] BklConfig config, [FromQuery] int x,
            [FromQuery] int y, [FromQuery] int z)
        {
            // HttpRequestMessage req = new HttpRequestMessage(HttpMethod.Get, $"https://m.earthol.me/map.jpg?lyrs=y&gl=cn&x={x}&y={y}&z={x}")
            // {
            // 	Version = new Version(2, 0)
            // };

            var dir = Path.Combine(config.MinioDataPath, "Google", z.ToString());
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var fpath = Path.Combine(dir, $"{x}_{y}.jpg");
            if (System.IO.File.Exists(fpath))
            {
                _logger.LogInformation("sending cacheing file " + fpath);
                return new PhysicalFileResult(fpath, "image/jpeg");
            }

            using (FileStream fs = new FileStream(fpath, FileMode.OpenOrCreate))
            {
                var msg = await client.GetAsync($"https://m.earthol.me/map.jpg?lyrs=y&gl=cn&x={x}&y={y}&z={z}");
                _logger.LogInformation(msg.ToString());
                var bts = await msg.Content.ReadAsByteArrayAsync();
                await fs.WriteAsync(bts, 0, bts.Length);
                await fs.FlushAsync();
            }

            return new PhysicalFileResult(fpath, "image/jpeg");
        }

        //从原始照片中提取gps信息 
        [HttpGet("get-facilities-gps")]
        public async Task<IActionResult> GetGPSList([FromServices] BklDbContext context, [FromServices] BklConfig config, [FromServices] IRedisClient redis,
            [FromQuery] long factoryId,
            [FromQuery] string baseDir = null,
            [FromQuery] string format = null)
        {

            var minio = new MinioClient()
                .WithEndpoint(config.MinioConfig.EndPoint)
                .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                .WithRegion(config.MinioConfig.Region)
                .Build();
            var dbFacilities = context.BklFactoryFacility.Where(s => s.FactoryId == factoryId)
                .Select(s => s).ToList();
            if (baseDir == null)
            {
                string bucket = redis.GetValueFromHash($"FacilityMeta:{dbFacilities[0].Id}", "bucket");
                baseDir = bucket.Split("-")[1];
            }

            List<FacilityGPS> rets = new List<FacilityGPS>();
            string[] faciDirs = null;
            try
            {
                faciDirs = Directory.GetDirectories("/app/rawpic/" + baseDir);
                _logger.LogInformation($"faciDirs {JsonSerializer.Serialize(faciDirs)}");
            }
            catch
            {
            }

            foreach (var dbFacility in dbFacilities)
            {
                var first = context.BklInspectionTaskDetail.Where(s => s.FacilityId == dbFacility.Id && s.FactoryId == dbFacility.FactoryId).FirstOrDefault();

                //if (!string.IsNullOrEmpty(dbFacility.GPSLocation))
                //{
                //    rets.Add(new FacilityGPS
                //    {
                //        name = dbFacility.Name,
                //        id = dbFacility.Id,
                //        defaultPic = first.RemoteImagePath,
                //        gps = JsonSerializer.Deserialize<double[]>(dbFacility.GPSLocation)
                //    });
                //    continue;
                //}

                //string gps = redis.GetValueFromHash($"FacilityMeta:{dbFacility.Id}", "GPS");
                //if (!gps.Empty())
                //{
                //    dbFacility.GPSLocation = gps;
                //    rets.Add(new FacilityGPS
                //    {
                //        name = dbFacility.Name,
                //        id = dbFacility.Id,
                //        defaultPic = first.RemoteImagePath,
                //        gps = JsonSerializer.Deserialize<double[]>(gps)
                //    });
                //    continue;
                //}
                var arr = first.RemoteImagePath.Split('/');

                try
                {
                    var stat = await minio.StatObjectAsync(new StatObjectArgs().WithBucket(arr[0]).WithObject(arr[1]));


                    var getobj = new GetObjectArgs()
                        .WithBucket(arr[0])
                        .WithObject(arr[1])
                        .WithCallbackStream(stream =>
                        {
                            var img = new MagickImage(stream);
                            using (img)
                            {
                                var profile = img.GetExifProfile();
                                var lat = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLatitude);
                                var lon = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLongitude);
                                var tslat = lat.GetValue() as ImageMagick.Rational[];
                                var tslon = lon.GetValue() as ImageMagick.Rational[];
                                double retLat = 0, retLon = 0;
                                for (int i = 0; i < tslat.Length; i++)
                                {
                                    retLat += tslat[i].ToDouble() / Math.Pow(60, i);
                                }

                                for (int i = 0; i < tslon.Length; i++)
                                {
                                    retLon += tslon[i].ToDouble() / Math.Pow(60, i);
                                }

                                rets.Add(new FacilityGPS
                                {
                                    name = dbFacility.Name,
                                    id = dbFacility.Id,
                                    defaultPic = first.RemoteImagePath,
                                    gps = new double[] { retLat, retLon }
                                });
                                dbFacility.GPSLocation = JsonSerializer.Serialize(new double[] { retLat, retLon });
                                redis.SetEntryInHash($"FacilityMeta:{dbFacility.Id}", "GPS", dbFacility.GPSLocation);
                            }
                        });
                    await minio.GetObjectAsync(getobj);
                    //var faciPicDir = faciDirs.FirstOrDefault(s => s.EndsWith("_" + dbFacility.Name));
                    //_logger.LogInformation($"facility name {dbFacility.Name} faciPicDir {faciPicDir}");
                    //if (faciPicDir != null)
                    //{
                    //    var files = Directory.GetFiles(faciPicDir);

                    //    string jpg = files.FirstOrDefault(s => s.EndsWith("JPG"));
                    //    if (jpg == null)
                    //    {
                    //        var dir = Directory.GetDirectories(faciPicDir);
                    //        foreach (var ddd in dir)
                    //        {
                    //            jpg = Directory.GetFiles(ddd).FirstOrDefault(s => s.EndsWith("JPG"));
                    //            if (jpg != null)
                    //                break;
                    //        }
                    //    }

                    //    _logger.LogInformation(jpg);
                    //    var img = new MagickImage(jpg);
                    //    var profile = img.GetExifProfile();
                    //    var lat = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLatitude);
                    //    var lon = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLongitude);
                    //    var tslat = lat.GetValue() as ImageMagick.Rational[];
                    //    var tslon = lon.GetValue() as ImageMagick.Rational[];
                    //    double retLat = 0, retLon = 0;
                    //    for (int i = 0; i < tslat.Length; i++)
                    //    {
                    //        retLat += tslat[i].ToDouble() / Math.Pow(60, i);
                    //    }

                    //    for (int i = 0; i < tslon.Length; i++)
                    //    {
                    //        retLon += tslon[i].ToDouble() / Math.Pow(60, i);
                    //    }

                    //    rets.Add(new
                    //    { name = dbFacility.Name, id = dbFacility.Id, gps = new double[] { retLat, retLon } });
                    //    dbFacility.GPSLocation = JsonSerializer.Serialize(new double[] { retLat, retLon });
                    //    redis.SetEntryInHash($"FacilityMeta:{dbFacility.Id}", "GPS", dbFacility.GPSLocation);
                    //    _logger.LogInformation(
                    //        $"{faciPicDir.Substring(faciPicDir.LastIndexOf("_") + 1)} lat:{retLat} lon:{retLon}");
                    //}
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }

            try
            {
                context.SaveChanges();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            if (format == null)
                return (IActionResult)Json(rets);
            return (IActionResult)Content(string.Join("", rets.Select(s => $"{s.gps[0]}\t{s.gps[1]}\t{s.name}\r\n")));
        }


        [HttpGet("image-detect")]
        public IActionResult ImageDetect([FromServices] BklConfig config, [FromServices] IRedisClient redis, int taskId,
            long facilityId, string path)
        {
            string val = redis.GetValueFromHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}", path);
            if (string.IsNullOrEmpty(val))
            {
                //var result1 = await DetectHelper.Detect(config, taskId, facilityId, path);
                return Json(new YoloResult[] { });
            }
            else
            {
                return Json(JsonSerializer.Deserialize<YoloResult[]>(val));
            }
        }
        // [HttpGet("image-fuse")]
        // public async Task<IActionResult> ImageStich([FromServices] BklConfig config, [FromServices] IRedisClient redis, int taskId, long facilityId, DetectHelper.FuseRequest request)
        // {
        // 	string key = $"{path1}-{path2}";
        // 	string val = redis.GetValueFromHash($"StichTaskResult:Tid.{taskId}.Faid.{facilityId}", key);
        // 	if (readCache != 1)
        // 		val = string.Empty;
        // 	if (string.IsNullOrEmpty(val))
        // 	{
        // 		var dataArr = await DetectHelper.Stitch(config, taskId, facilityId, path1, path2, rotate90);

        // 		redis.SetEntryInHash($"SegTaskResult:Tid.{taskId}.Faid.{facilityId}", key, JsonSerializer.Serialize(dataArr));
        // 		return Json(dataArr);
        // 	}
        // 	else
        // 	{
        // 		return Json(JsonSerializer.Deserialize<DetectHelper.StitchToFuse[]>(val));
        // 	}
        // }
        [HttpGet("image-stitch-optimize")]
        public IActionResult ImageStichOptimize([FromServices] BklConfig config, [FromServices] IRedisClient redis,
            int taskId, long facilityId, string path1, string path2, string rotate90, int readCache = 1)
        {
            string key = $"{path1}-{path2}";
            string val = redis.GetValueFromHash($"StitchTaskResult:Tid.{taskId}.Faid.{facilityId}", key);
            var fuse = JsonSerializer.Deserialize<StitchToFuse[]>(val);
            Func<double, double, double> fxy = (x, y) =>
            {
                double sum = 0;
                for (int i = 0; i < fuse[0].kp.GetLongLength(0); i++)
                {
                    var lp = fuse[0].kp[i];
                    var rp = fuse[1].kp[i];
                    sum = Math.Sqrt(Math.Pow(lp[0] - rp[0] + x, 2) + Math.Pow(lp[1] - rp[1] + y, 2));
                }

                return sum;
            };
            Func<int, int, double, double> dfx = (x, y, dx) => (fxy(x + dx, y) - fxy(x, y)) / dx;
            Func<int, int, double, double> dfy = (x, y, dy) => (fxy(x, y + dy) - fxy(x, y)) / dy;
            double sum = 1000000;
            int minx = 0, miny = 0;
            for (int x = -500; x <= 500; x++)
            {
                for (int y = -500; y <= 500; y++)
                {
                    var sumtemp = fxy(x, y);
                    if (sumtemp < sum)
                    {
                        sum = sumtemp;
                        minx = x;
                        miny = y;
                    }
                }
            }

            return Json(new { x = minx, y = miny });
        }

        [HttpPost("images-fuse")]
        public async Task<IActionResult> ImagesFuse([FromServices] BklConfig config, [FromServices] IRedisClient redis, [FromQuery] long taskId, [FromQuery] long facilityId, [FromBody] string[] images)
        {
            var md5 = SecurityHelper.Get32MD5(string.Join("", images));

            string val = redis.GetValueFromHash($"FuseImagesResult:Tid.{taskId}.Faid.{facilityId}", md5);
            if (string.IsNullOrEmpty(val))
            {
                var result =
                    await DetectHelper.FuseImages(taskId, facilityId, $"{taskId}_{facilityId}_{md5}.jpg", images);
                if (result != null && result.state == null)
                    redis.SetEntryInHash($"FuseImagesResult:Tid.{taskId}.Faid.{facilityId}", md5,
                        JsonSerializer.Serialize
                            (result));
                return Json(result);
            }
            else
            {
                return Content(val, "application/json");
            }
        }

        [HttpGet("image-stitch")]
        public async Task<IActionResult> ImageStich([FromServices] BklConfig config, [FromServices] IRedisClient redis,
            int taskId, long facilityId, string path1, string path2, string rotate90, int readCache = 1)
        {
            string key = $"{path1}-{path2}";
            string val = redis.GetValueFromHash($"StitchTaskResult:Tid.{taskId}.Faid.{facilityId}", key);
            if (readCache != 1)
                val = string.Empty;
            if (string.IsNullOrEmpty(val))
            {
                var dataArr = await DetectHelper.Stitch(config, taskId, facilityId, path1, path2, rotate90);

                redis.SetEntryInHash($"StitchTaskResult:Tid.{taskId}.Faid.{facilityId}", key,
                    JsonSerializer.Serialize(dataArr));
                return Json(dataArr);
            }
            else
            {
                return Json(JsonSerializer.Deserialize<StitchToFuse[]>(val));
            }
        }

        [HttpGet("image-seg")]
        public async Task<IActionResult> ImageSeg([FromServices] BklConfig config, [FromServices] IRedisClient redis,
            int taskId, long facilityId, string path)
        {
            string val = redis.GetValueFromHash($"SegTaskResult:Tid.{taskId}.Faid.{facilityId}", path);
            if (string.IsNullOrEmpty(val))
            {
                var dataArr = await DetectHelper.Seg(config, taskId, facilityId, path);

                redis.SetEntryInHash($"SegTaskResult:Tid.{taskId}.Faid.{facilityId}", path,
                    JsonSerializer.Serialize(dataArr));
                return Json(dataArr);
            }
            else
            {
                return Json(JsonSerializer.Deserialize<int[][]>(val));
            }
        }


        //[HttpPost("barcode")]
        //public async Task<IActionResult> RecorgnizeBarCode([FromServices] BklConfig config,[FromForm(Name = "file")] IFormFile formFile)
        //{
        //    var pt = System.IO.Path.Combine(config.FileBasePath, "UploadFiles");
        //    using (var stream = new FileStream(System.IO.Path.Combine(pt, formFile.FileName), FileMode.OpenOrCreate))
        //    {
        //        await formFile.CopyToAsync(stream);
        //        stream.Seek(0, SeekOrigin.Begin);
        //        using (var mat = Mat.FromStream(stream,ImreadModes.Color))
        //        {
        //            var reader = new ZXing.OpenCV.BarcodeReader();
        //            reader.Options.PureBarcode=false;
        //            reader.AutoRotate=true;
        //            reader.Options.TryHarder=true;
        //            reader.Options.TryInverted=true;
        //            var result = reader.Decode(mat);
        //            if(result==null)
        //                return Json(new { error = 1 });
        //            logger.LogInformation($"{formFile.FileName} {formFile.Length} {result.Text} {result.BarcodeFormat.ToString()}");
        //            return Json(new { error = 0, text = result.Text, type = result.BarcodeFormat.ToString() });
        //        }
        //    }
        //    return Json(new { error = 1 });
        //}
        [AllowAnonymous]
        [HttpPost("/minio_notification")]
        public Task MinioPostNotificationAsync([FromBody] MinioBucketNotification bucketNotification)
        {
            Console.WriteLine(bucketNotification.Records[0].s3.bucketObject.key);
            return Task.CompletedTask;
        }

        [HttpPost("upload-pic")]
        public async Task<IActionResult> PostFileAsync(
            [FromServices] BklConfig config,
            [FromForm(Name = "file")] IFormFile formFile,
            [FromQuery] string bucketName = "",
            [FromQuery] string fileName = "",
            [FromQuery] string seqId = "",
            [FromQuery] int quality = 80
        )
        {

            var minio = new MinioClient()
                .WithEndpoint(config.MinioConfig.EndPoint)
                .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                .WithRegion(config.MinioConfig.Region)
                .Build();
            var pt = System.IO.Path.Combine(config.FileBasePath, "UploadFiles");
            string fname = fileName.Empty() ? formFile.FileName : fileName;
            Directory.CreateDirectory(pt);
            try
            {
                string url = string.Empty;
                string urlSmall = string.Empty;
                int width = 0;
                int height = 0;
                string createtime = "2020-01-01";
                using (var stream = new FileStream(System.IO.Path.Combine(pt, fname), FileMode.OpenOrCreate))
                {
                    await formFile.CopyToAsync(stream);

                    stream.Seek(0, SeekOrigin.Begin);



                }

                string filename = System.IO.Path.Combine(pt, fname);
                string filenameSmall = System.IO.Path.Combine(pt, "small-" + fname);

                using (var img = Image.Load(filename))
                {
                    width = img.Width;
                    height = img.Height;

                    //img.Save(filenameSmall);
                    img.SaveAsJpeg(filename, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder() { Quality = quality });
                    img.Mutate(ctx => ctx.Resize(200, 150));
                    img.SaveAsJpeg(filenameSmall);
                }


                try
                {
                    using (var img = new MagickImage(filename))
                    {
                        var profile = img.GetExifProfile();
                        createtime = profile?.GetValue(ExifTag.DateTimeOriginal)?.Value;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError("GetExifError " + ex.ToString());
                }
                if (createtime.Empty())
                {
                    createtime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }

                await minio.CreateBucket(bucketName);

                await minio.CreateBucket(bucketName + "-small");

                url = await minio.UploadFile(System.IO.Path.Combine(pt, fname), fname, bucketName);

                urlSmall = await minio.UploadFile(System.IO.Path.Combine(pt, "small-" + fname), fname, bucketName + "-small");


                System.IO.File.Delete(System.IO.Path.Combine(pt, fname));

                System.IO.File.Delete(System.IO.Path.Combine(pt, "small-" + fname));

                return new JsonResult(new Dictionary<string, string>
                {
                    { "code", "0" },
                    { "name", $"{bucketName}/{fname}" },
                    { "fname", $"{fname}" },
                    { "path",  $"{config.MinioConfig.PublicEndPoint}/{bucketName}/{fname}" },

                    { "width", width.ToString() },
                    { "height", height.ToString() },

                    { "createTime",createtime },
                    { "picLocalUrl", $"{config.MinioConfig.PublicEndPoint}/{bucketName}-small/{fname}" }
                });
            }
            catch (Exception ex)
            {
                return new JsonResult(new Dictionary<string, string> { { "code", "1" }, { "error", ex.ToString() } });
            }
        }
    }
}