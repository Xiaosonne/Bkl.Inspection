using Bkl.Infrastructure;
using Bkl.Models;
using ImageMagick;
using Microsoft.AspNetCore.Mvc;
using Minio;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using static Bkl.Inspection.PLImageController.ImageDirectoryWithLocation;

namespace Bkl.Inspection
{
    [ApiController]
    [Route("[controller]")]
    public class PLImageController : Controller
    {
        public class ImageDirectoryWithLocation : ImageDirectory
        {
            public class ImageLocationGroup
            {
                public ImageLocation Location { get; set; }
                public ImageLocation[] Items { get; set; }
            }
            public class ImageLocation
            {
                public string Name { get; set; }
                public double Lat { get; set; }
                public double Lon { get; set; }
            }

            public ImageLocationGroup[] LocationGroups { get; set; }
        }

        [HttpPost("image-dir-init")]
        public async Task<IActionResult> InitImageDir([FromServices] BklConfig config)
        {
            var minio = new MinioClient()
                       .WithEndpoint(config.MinioConfig.EndPoint)
                       .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                       .WithRegion(config.MinioConfig.Region)
                       .Build();
            await minio.CreateBucket("image-dir");
            return Json(new { error = false });
        }

        [HttpGet("image-dir")]
        public async Task<IActionResult> ListImageDir([FromServices] BklConfig config)
        {
            var minio = new MinioClient()
                         .WithEndpoint(config.MinioConfig.EndPoint)
                         .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                         .WithRegion(config.MinioConfig.Region)
                         .Build();
            var imageDirId = SnowId.NextId();

            var lis = minio.ListObjectsAsync(new ListObjectsArgs().WithBucket("image-dir"));
            var strs = new List<ImageDirectory>();
            foreach (var obj in lis)
            {
                var obj1 = await minio.ReadObject<ImageDirectory>(obj.Key, "image-dir");
                strs.Add(obj1);
            }
            return Json(strs);
        }





        [HttpPost("image-dir")]
        public async Task<IActionResult> CreateImageDir([FromBody] ImageDirectory request, [FromServices] BklConfig config)
        {
            var minio = new MinioClient()
                         .WithEndpoint(config.MinioConfig.EndPoint)
                         .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                         .WithRegion(config.MinioConfig.Region)
                         .Build();
            var imageDirId = SnowId.NextId();
            await minio.CreateBucket(request.Name);
            await minio.WriteObject(new ImageDirectory { Name = request.Name, ImageDirId = imageDirId }, imageDirId.ToString(), "image-dir");
            return Json(request);
        }

        [HttpGet("date-group-view")]
        public async Task<IActionResult> GetImageDateView([FromServices] BklConfig config, long imageDirId, int minTotal, int maxTotal)
        {
            var minio = new MinioClient()
                       .WithEndpoint(config.MinioConfig.EndPoint)
                       .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                       .WithRegion(config.MinioConfig.Region)
                       .Build();
            var data = await minio.ReadObject<ImageDirectory>(imageDirId.ToString(), "image-dir");
            var lis = minio.ListObjectsAsync(new ListObjectsArgs().WithBucket(data.Name));
            var strs = new List<string>();
            foreach (var obj in lis)
            {
                strs.Add(obj.Key);
            }
            var date = strs.Select(s => new { name = s, date = DateTime.ParseExact(s.Split('_')[1], "yyyyMMddHHmmss", null) }).ToList();

            List<object> lis1 = new List<object>();
            date = date.OrderBy(s => s.name).ToList();
            var date0 = date.First().date;
            List<string> gp = new List<string>();
            foreach (var item in date)
            {
                if (item.date.Subtract(date0).TotalSeconds <= maxTotal)
                {
                    gp.Add(item.name);
                }
                else
                {

                    lis1.Add(new { min = gp.FirstOrDefault(), max = gp.Last(), items = gp, total = gp.Count });
                    gp = new List<string>();
                    gp.Add(item.name);
                }
                date0 = item.date;
            }
            List<double> lis2 = new List<double>();
            date0 = date.First().date;
            foreach (var item in date)
            {
                lis2.Add(item.date.Subtract(date0).TotalSeconds);
                date0 = item.date;
            }
            return Json(new { images = lis1, sub = lis2 });
        }
        [HttpGet("load-gps")]
        public async Task<IActionResult> SetImageLocationView([FromServices] BklConfig config, long imageDirId)
        {
            var minio = new MinioClient()
                    .WithEndpoint(config.MinioConfig.EndPoint)
                    .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                    .WithRegion(config.MinioConfig.Region)
                    .Build();
            var data = await minio.ReadObject<ImageDirectory>(imageDirId.ToString(), "image-dir");
            var lis = minio.ListObjectsAsync(new ListObjectsArgs().WithBucket(data.Name));
            List<dynamic> lis1 = new List<dynamic>();
            foreach (var obj in lis)
            {
                var tag = await minio.GetObjectTagsAsync(new GetObjectTagsArgs().WithBucket(data.Name).WithObject(obj.Key));
                if (tag.TaggingSet.Tag.Count != 0)
                {
                    continue;
                }
                Action<Stream> act = async stream =>
                {
                    double retLat = 0, retLon = 0;
                    using (var img = new MagickImage(stream))
                    {
                        var profile = img.GetExifProfile();
                        var lat = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLatitude);
                        var lon = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLongitude);
                        var tslat = lat.GetValue() as ImageMagick.Rational[];
                        var tslon = lon.GetValue() as ImageMagick.Rational[];
                        for (int i = 0; i < tslat.Length; i++)
                        {
                            retLat += tslat[i].ToDouble() / Math.Pow(60, i);
                        }

                        for (int i = 0; i < tslon.Length; i++)
                        {
                            retLon += tslon[i].ToDouble() / Math.Pow(60, i);
                        }
                    }
                    await minio.SetObjectTagsAsync(new SetObjectTagsArgs().WithObject(obj.Key).WithBucket(data.Name).WithTagging(new Minio.DataModel.Tags.Tagging(new Dictionary<string, string>
                    {
                        {"lat",retLat.ToString() },
                        {"lon",retLon.ToString() },
                    }, false)));
                };
            }
            return Json(new { error = 0 });
        }
        [HttpGet("coords")]
        public async Task<IActionResult> ProxyGetCoords([FromServices] BklConfig config, string x, string y)
        {
            HttpClient client = new HttpClient(new HttpClientHandler());

            client.DefaultRequestHeaders.UserAgent.Clear();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (iPad; U; CPU OS 4_3_3 like Mac OS X; en-us) AppleWebKit/533.17.9 (KHTML, like Gecko) Version/5.0.2 Mobile/8J2 Safari/6533.18.5");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Encodings", "zh-CN,en,en-GB,en-US;q=0.8");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "image/*,*/*;");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://tool.lu/coordinate");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Origin", "https://tool.lu");


            var resp = await client.PostAsync("https://tool.lu/coordinate/ajax.html?mode=single", new StringContent($"src_type=cgcs2000&src_coordinate={x},{y}&src_band=39", Encoding.UTF8, "application/x-www-form-urlencoded"));
            return Json(await resp.Content.ReadAsStringAsync());
        }

        [HttpGet("image/{bucketName}/{imageName}")]
        public async Task<IActionResult> ProxyGetImage([FromServices] BklConfig config, [FromRoute] string bucketName, string imageName)
        {
            var minio = new MinioClient()
                  .WithEndpoint(config.MinioConfig.EndPoint)
                  .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                  .WithRegion(config.MinioConfig.Region)
                  .Build();

            MemoryStream smallms = new MemoryStream();
            await minio.CreateBucket($"{bucketName}-small");
            try
            {
                var stat = await minio.GetObjectAsync(new GetObjectArgs()
             .WithBucket($"{bucketName}-small")
             .WithObject(imageName)
             .WithCallbackStream(st1 =>
             {
                 st1.CopyTo(smallms);
             }));
                if (stat != null)
                {
                    return new FileContentResult(smallms.ToArray(), "image/jpeg");
                }
            }
            catch (Minio.Exceptions.ObjectNotFoundException exp)
            {

            }


            MemoryStream ms = new MemoryStream();
            await minio.GetObjectAsync(new GetObjectArgs()
                .WithBucket($"{bucketName}")
                .WithObject(imageName)
                .WithCallbackStream(file =>
                {
                    file.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    using (var img = SixLabors.ImageSharp.Image<Rgb24>.Load(ms))
                    {
                        img.Mutate(ctx =>
                        {
                            ctx.Resize(200, 150);
                        });
                        img.Save(smallms, new JpegEncoder());
                    }
                }));
            smallms.Seek(0, SeekOrigin.Begin);
            await minio.PutObjectAsync(new PutObjectArgs().WithBucket($"{bucketName}-small").WithObject(imageName).WithObjectSize(smallms.Length).WithStreamData(smallms));
            smallms.Seek(0, SeekOrigin.Begin);
            return new FileContentResult(smallms.ToArray(), "image/jpeg");
        }

        [HttpGet("date-location-view")]
        public async Task<IActionResult> GetImageLocationView([FromServices] BklConfig config, long imageDirId, double radius)
        {
            var minio = new MinioClient()
                     .WithEndpoint(config.MinioConfig.EndPoint)
                     .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                     .WithRegion(config.MinioConfig.Region)
                     .Build();
            var data = await minio.ReadObject<ImageDirectoryWithLocation>(imageDirId.ToString(), "image-dir");
            if (data.LocationGroups != null)
            {
                return Json(data);
            }

            var lis = minio.ListObjectsAsync(new ListObjectsArgs().WithBucket(data.Name));
            var strs = new List<string>();
            List<ImageLocation> lis1 = new List<ImageLocation>();

            foreach (var obj in lis)
            {
                var tag = await minio.GetObjectTagsAsync(new GetObjectTagsArgs().WithBucket(data.Name).WithObject(obj.Key));
                if (tag.TaggingSet.Tag.Count != 0)
                {
                    var val1 = tag.TaggingSet.Tag.Where(s => s.Key == "lat").First().Value;
                    var val2 = tag.TaggingSet.Tag.Where(s => s.Key == "lon").First().Value;
                    lis1.Add(new ImageLocation { Name = obj.Key, Lat = double.Parse(val1), Lon = double.Parse(val2) });
                    continue;
                }
            }
            dynamic first = lis1.First();
            List<List<ImageLocation>> gps = new List<List<ImageLocation>>();
            foreach (var obj in lis1)
            {
                bool added = false;
                foreach (var gp in gps)
                {
                    foreach (var ta in gp)
                    {
                        var dis2 = MapHelper.GetDistance(obj.Lat, obj.Lon, ta.Lat, ta.Lon);
                        Console.WriteLine($"{ta.Name} {obj.Name} {dis2}");

                        if (dis2 < radius)
                        {
                            gp.Add(obj);
                            added = true;
                            break;
                        };
                    }
                    if (added)
                    {
                        continue;
                    }
                }
                if (added)
                {
                    continue;
                }
                gps.Add(new List<ImageLocation>(new ImageLocation[] { obj }));
                first = obj;
            }
            data.LocationGroups = gps.Select(s => new ImageLocationGroup
            {
                Location = s.First(),
                Items = s.ToArray(),
            }).ToArray();
            await minio.WriteObject(data, imageDirId.ToString(), "image-dir");
            return Json(data);
        }

        [HttpGet("upload-url")]
        public async Task<IActionResult> GetUploadUrl([FromServices] BklConfig config, [FromQuery] string bucketName, [FromQuery] string objectName)
        {
            var minio = new MinioClient()
                        .WithEndpoint(config.MinioConfig.EndPoint)
                        .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                        .WithRegion(config.MinioConfig.Region)
                        .Build();
            await minio.CreateBucket(bucketName);
            var url = await minio.PresignedPutObjectAsync(new PresignedPutObjectArgs()
                    .WithExpiry(60)
                    .WithObject(objectName)
                 .WithBucket(bucketName));
            return Json(new { url, bucketName, objectName });
        }
    }
}