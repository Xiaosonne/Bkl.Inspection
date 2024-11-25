using Bkl.Infrastructure;
using Bkl.Models;
using ImageMagick;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Minio;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Xml;

namespace Bkl.Inspection
{


    public class DJIWayPoint
    {
        public double lat { get; set; }
        public double lon { get; set; }
        public double ellipsoidHeight { get; set; }
        public double height { get; set; }
        public List<DJIWayShootAction> shoots { get; set; }
        public int index { get; set; }
    }
    /// <summary>
    /// 维度  经度 高度 北 东 地
    /// </summary>
    public class LatLonHeight
    {
        public LatLonHeight(string arr)
        {
            if (arr == null)
                return;
            var arrs = arr.Split(",");
            lat = double.Parse(arrs[0]);
            lon = double.Parse(arrs[1]);
            height = double.Parse(arrs[2]);
        }
        public double lat { get; set; }
        public double lon { get; set; }
        public double height { get; set; }
    }
    public class DJIWayShootAction
    {
        public string aircraftYaw { get; set; }
        public string yaw { get; set; }
        public string roll { get; set; }
        public string pitch { get; set; }

        public string pic { get; set; }
        public string distance { get; set; }
        public string pointName { get; set; }
        /// <summary>
        /// 被测物体的llh
        /// </summary>
        public LatLonHeight location { get; set; }
        public string[] pics { get; set; }
    }
    public class DJIImagePointName
    {

    }
    public class DJIImageName
    {
        public string Raw { get; }

        public string Type { get; }
        public long Order { get; }
        public DateTime Time { get; }
        public string Index { get; }
        public string PointName { get; set; }
        public double Distance { get; set; }
        public LatLonHeight Latlonheight { get; set; }
        public DJIImageName(string name)
        {
            try
            {
                var arr = name.Split("_");
                Raw = name;
                Time = DateTime.ParseExact(arr[1], "yyyyMMddHHmmss", null);
                Index = arr[2];
                Order = long.Parse(arr[1]);
                Type = arr[3].Split(".")[0];
            }
            catch (Exception ex)
            {
                throw new Exception("ParseDjiNameError " + name, ex);
            }
        }
    }
    public static class DjiNameExt
    {
        public static void PointFillNames(this List<DJIWayPoint> points, List<DJIImageName> names)
        {
            var group = names.Groups();
            int i = 0;
            foreach (var item in points)
            {
                if (item.shoots.Count == 0)
                {
                    continue;
                }
                if (i >= group.Count)
                    break;
                var shoot = item.shoots.FirstOrDefault();
                var orderPic = group.ElementAt(i);
                shoot.pics = orderPic.Select(s => s.Raw).ToArray();
                shoot.pic = orderPic.FirstOrDefault(s => s.Type == "Z" || s.Type == "V")?.Raw;
                i++;
            }
        }
        public static List<List<DJIImageName>> Groups(this List<DJIImageName> names)
        {
            List<List<DJIImageName>> groups = new List<List<DJIImageName>>();
            var lis = new List<DJIImageName>();
            groups.Add(lis);
            foreach (var name in names.OrderBy(s => s.Order))
            {
                if (lis.Count == 0)
                {
                    lis.Add(name);
                }
                else
                {

                    if (lis.All(q => q.Type != name.Type))
                    {
                        if (lis.Any(s => name.Time.Subtract(s.Time).TotalSeconds < 2))
                        {
                            lis.Add(name);
                            continue;
                        }
                    }
                    lis = new List<DJIImageName> { name };
                    groups.Add(lis);
                }
            }
            return groups;
        }
        public static void NamesFillPoint(this List<DJIImageName> names, List<DJIWayPoint> points)
        {
            List<List<DJIImageName>> groups = names.Groups();
            int i = 0;
            foreach (var item in points)
            {
                if (item.shoots.Count == 0)
                {
                    continue;
                }
                if (i >= groups.Count)
                    break;
                var shoot = item.shoots.FirstOrDefault();
                var orderPic = groups.ElementAt(i);
                foreach (var pic in orderPic)
                {
                    pic.Distance = shoot.distance == null ? 0 : double.Parse(shoot.distance);
                    pic.PointName = shoot.pointName ?? i.ToString();
                }
                i++;
            }
        }
    }


    public partial class PLInspectionController : Controller
    {
        [HttpGet("waylines")]
        public async Task<IActionResult> GetWaypoint(
                       [FromServices] BklConfig config,
                       [FromQuery] string bucketName = "power-waylines",
                       long factoryId = 0,
                       long taskId = 0)
        {
            var minio = new MinioClient()
             .WithEndpoint(config.MinioConfig.EndPoint)
             .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
             .WithRegion(config.MinioConfig.Region)
             .Build();

            var lis = await minio.ListObjects(bucketName, $"fac{factoryId}task{taskId}/");
            return Json(lis.Select(s => s.Substring(s.IndexOf('/') + 1)));
        }
        [HttpGet("wayline-gps")]
        public async Task<IActionResult> GetWaypointGps(
                        [FromServices] BklConfig config,
                        [FromQuery] string bucketName = "power-waylines",
                        long factoryId = 0,
                        long taskId = 0)
        {
            var minio = new MinioClient()
             .WithEndpoint(config.MinioConfig.EndPoint)
             .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
             .WithRegion(config.MinioConfig.Region)
             .Build();

            var lis = await minio.ListObjects(bucketName, $"fac{factoryId}task{taskId}/");
            List<object> lisret = new List<object>();
            foreach (var name in lis)
            {
                try
                {
                    var tags = await minio.GetObjectTagsAsync(new GetObjectTagsArgs().WithBucket(bucketName).WithObject(name));
                    Dictionary<string, string> dic = new Dictionary<string, string>(tags.TaggingSet.Tag.ToDictionary(s => s.Key, s => s.Value));
                    dic.Add("name", name.Substring(name.IndexOf('/') + 1));
                    lisret.Add(dic); ;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }
            }
            return Json(lisret);
        }
        [HttpGet("read-wayline")]
        public async Task<IActionResult> GetWaylineDetail(
                       [FromServices] BklConfig config,
                       [FromServices] BklDbContext context,
                       [FromQuery] string bucketName = "power-waylines",
                       long factoryId = 0,
                       long taskId = 0,
                       string kmzName = "")
        {
            var minio = new MinioClient()
             .WithEndpoint(config.MinioConfig.EndPoint)
             .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
             .WithRegion(config.MinioConfig.Region)
             .Build();
            var ms = await minio.ReadStream($"fac{factoryId}task{taskId}/{kmzName}", bucketName);

            ms.Seek(0, SeekOrigin.Begin);
            long facilityId = 0;

            try
            {
                var tags = await minio.GetObjectTagsAsync(new GetObjectTagsArgs().WithBucket(bucketName).WithObject($"fac{factoryId}task{taskId}/{kmzName}"));
                facilityId = long.Parse(tags.GetTags()["facilityId"]);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }


            List<DJIWayPoint> marks = LoadKmz(ms);
            if (facilityId > 0)
            {
                var allpics = context.BklInspectionTaskDetail
                     .Where(s => s.TaskId == taskId && s.FacilityId == facilityId && s.FactoryId == factoryId)
                     .ToList()
                     .Select(s => new DJIImageName(s.RemoteImagePath))
                     .ToList();
                try
                {
                    marks.PointFillNames(allpics);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.ToString());
                }

            }

            return Json(marks);
        }




        [HttpGet("match-wayline")]
        public async Task<IActionResult> MatchWaylineImage(
               [FromServices] BklConfig config,
               [FromServices] BklDbContext context,
               [FromQuery] string bucketName = "power-waylines",
               long factoryId = 0,
               long taskId = 0,
               long facilityId = 0,
               string kmzName = "")
        {
            if (string.IsNullOrEmpty(kmzName))
                return NotFound();
            var minio = new MinioClient()
             .WithEndpoint(config.MinioConfig.EndPoint)
             .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
             .WithRegion(config.MinioConfig.Region)
             .Build();
            var ms = await minio.ReadStream($"fac{factoryId}task{taskId}/{kmzName}", bucketName);
            ms.Seek(0, SeekOrigin.Begin);

            List<DJIWayPoint> marks = LoadKmz(ms);

            var allpics = context.BklInspectionTaskDetail
                 .Where(s => s.TaskId == taskId && s.FacilityId == facilityId && s.FactoryId == factoryId)
                 .Select(s => new DJIImageName(s.RemoteImagePath))
                 .ToList();
            try
            {
                allpics.NamesFillPoint(marks);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.ToString());
            }
            return Json(allpics);

        }

        private List<DJIWayPoint> LoadKmz(Stream ms)
        {
            using var archive = new ZipArchive(ms);
            var entry = archive.Entries.First(s => s.Name.EndsWith("template.kml"));
            var kmlstream = entry.Open();
            var marks = LoadKmlPoint(kmlstream).OrderBy(s => s.index).ToList();
            return marks;
        }

        [DisableRequestSizeLimit]
        [AllowAnonymous]
        [HttpPost("open-wayline")]
        [HttpPost("open-waypoint")]
        public async Task<IActionResult> OpenWaypoint(
                    [FromServices] BklConfig config,
                    [FromQuery] string fileName,
                    [FromQuery] string bucketName = "power-waylines",
                    long factoryId = 0,
                    long taskId = 0)
        {
            if (string.IsNullOrEmpty(fileName))
                return BadRequest();

            var minio = new MinioClient()
                .WithEndpoint(config.MinioConfig.EndPoint)
                .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
                .WithRegion(config.MinioConfig.Region)
                .Build();

            await minio.CreateBucket(bucketName);

            using (MemoryStream ms = new MemoryStream())
            {
                await this.Request.BodyReader.CopyToAsync(ms);
                ms.Seek(0, SeekOrigin.Begin);
                if (ms.Length == 0)
                    return Json(new { error = 1 });
                var md5 = SecurityHelper.GetMd5(ms.ToArray());

                using ZipArchive archive = new ZipArchive(ms);
                var entry = archive.Entries.First(s => s.Name.EndsWith("template.kml"));
                var stream = entry.Open();
                var marks = LoadKmlPoint(stream);
                var objectName = $"fac{factoryId}task{taskId}/{fileName}";
                bool exists = await minio.ObjectExists(bucketName, objectName, md5);
                var tags = new Dictionary<string, string>() {
                        { "lon", marks[0].lon.ToString() },
                        { "lat", marks[0].lat.ToString() },
                        { "height", marks[0].ellipsoidHeight.ToString() },
                    };
                if (!exists)
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    await minio.UploadStream(ms, objectName, bucketName, tags: tags);
                }
                else
                {
                    await minio.SetObjectTagsAsync(new SetObjectTagsArgs().WithBucket(bucketName).WithObject(objectName).WithTagging(new Minio.DataModel.Tags.Tagging(tags, false)));
                }

                return Json(marks);
            }

        }


        [HttpGet("lidar")]
        public async Task<JsonResult> GetLidarList([FromServices] IRedisClient redis, [FromServices] BklConfig config, long factoryId)
        {
            var minio = new MinioClient()
           .WithEndpoint(config.MinioConfig.EndPoint)
           .WithCredentials(config.MinioConfig.Key, config.MinioConfig.Secret)
           .WithRegion(config.MinioConfig.Region)
           .Build();

            var obj = minio.ListObjectsAsync(new ListObjectsArgs().WithBucket("power-lidar").WithPrefix($"las").WithRecursive(false));
            TaskCompletionSource<List<string>> tccc = new TaskCompletionSource<List<string>>();
            List<string> tags = new List<string>();
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
            string sstr = redis.Get($"Lidar:Factory{factoryId}");
            var lidiarfacilist = sstr.Empty() ? new List<string>() : JsonSerializer.Deserialize<List<string>>(sstr);
            return Json(new { bindlist = lidiarfacilist, allfiles = tags });
        }

        [HttpPut("lidar")]
        public JsonResult BindLidarList([FromServices] IRedisClient redis, [FromQuery] long factoryId, [FromQuery] string path)
        {
            var lidiarfacilist = path.Split(",").ToList();
            redis.Set($"Lidar:Factory{factoryId}", JsonSerializer.Serialize(lidiarfacilist));
            return Json(lidiarfacilist);
        }


        private List<DJIWayPoint> LoadKmlPoint(Stream stream)
        {
            var doc = new XmlDocument();
            doc.Load(stream);
            XmlNamespaceManager xmm = new XmlNamespaceManager(doc.NameTable);
            xmm.AddNamespace("wpml", doc.GetElementsByTagName("wpml:index")[0].NamespaceURI);
            xmm.AddNamespace("ns", "http://www.opengis.net/kml/2.2");
            var root = doc.DocumentElement;
            var placemarks = root.SelectNodes("/ns:kml/ns:Document/ns:Folder/ns:Placemark", xmm);
            List<DJIWayPoint> lis = new List<DJIWayPoint>();
            int indexInt = 1;
            foreach (XmlNode placemark in placemarks)
            {
                var arr = placemark.SelectSingleNode("ns:Point/ns:coordinates", xmm).InnerText.Split(",");
                var ellipsoidHeight = double.Parse(placemark.SelectSingleNode("wpml:ellipsoidHeight", xmm).InnerText);
                var shoots = new List<DJIWayShootAction>();

                List<XmlNode> actions = new List<XmlNode>();

                var inspectionParam = placemark.SelectSingleNode("wpml:inspectionShootingParam", xmm);
                var index = placemark.SelectSingleNode("wpml:index", xmm);


                foreach (XmlNode item in placemark.SelectNodes("wpml:actionGroup/wpml:action", xmm))
                {
                    actions.Add(item);
                }

                var orientedShoot = actions.FirstOrDefault(nd => nd.SelectSingleNode("wpml:actionActuatorFunc", xmm)?.InnerText == "orientedShoot");
                var takePhoto = actions.FirstOrDefault(nd => nd.SelectSingleNode("wpml:actionActuatorFunc", xmm)?.InnerText == "takePhoto");
                var rotateYaw = actions.FirstOrDefault(nd => nd.SelectSingleNode("wpml:actionActuatorFunc", xmm)?.InnerText == "rotateYaw");

                var gimbalRotate = actions.FirstOrDefault(nd => nd.SelectSingleNode("wpml:actionActuatorFunc", xmm)?.InnerText == "gimbalRotate");
                if (orientedShoot != null)
                {
                    var yall = int.Parse(orientedShoot.SelectSingleNode("wpml:actionActuatorFuncParam/wpml:aircraftHeading", xmm)?.InnerText);
                    shoots.Add(new DJIWayShootAction
                    {
                        aircraftYaw = yall.ToString(),
                        yaw = orientedShoot.SelectSingleNode("wpml:actionActuatorFuncParam/wpml:gimbalYawRotateAngle", xmm)?.InnerText,
                        roll = orientedShoot.SelectSingleNode("wpml:actionActuatorFuncParam/wpml:gimbalRollRotateAngle", xmm)?.InnerText,
                        pitch = orientedShoot.SelectSingleNode("wpml:actionActuatorFuncParam/wpml:gimbalPitchRotateAngle", xmm)?.InnerText,

                        distance = inspectionParam?.SelectSingleNode("wpml:shootingDistance", xmm)?.InnerText,
                        pointName = inspectionParam?.SelectSingleNode("wpml:shootingPointName", xmm)?.InnerText,
                        location = new LatLonHeight(inspectionParam?.SelectSingleNode("wpml:shootingPointLocation", xmm)?.InnerText),
                    });
                }

                if (takePhoto != null && gimbalRotate != null && rotateYaw != null)
                {
                    int aircraftYaw = 0;
                    if (rotateYaw != null)
                    {
                        aircraftYaw = int.Parse(rotateYaw.SelectSingleNode("wpml:actionActuatorFuncParam/wpml:aircraftHeading", xmm)?.InnerText);
                        if (aircraftYaw < 0)
                        {
                            aircraftYaw = 360 + aircraftYaw;
                        }
                    }

                    shoots.Add(new DJIWayShootAction
                    {
                        aircraftYaw = aircraftYaw.ToString(),
                        yaw = gimbalRotate.SelectSingleNode("wpml:actionActuatorFuncParam/wpml:gimbalYawRotateAngle", xmm)?.InnerText,
                        roll = gimbalRotate.SelectSingleNode("wpml:actionActuatorFuncParam/wpml:gimbalRollRotateAngle", xmm)?.InnerText,
                        pitch = gimbalRotate.SelectSingleNode("wpml:actionActuatorFuncParam/wpml:gimbalPitchRotateAngle", xmm)?.InnerText,
                        distance = inspectionParam?.SelectSingleNode("wpml:shootingDistance", xmm)?.InnerText,
                        pointName = inspectionParam?.SelectSingleNode("wpml:shootingPointName", xmm)?.InnerText,
                        location = new LatLonHeight(inspectionParam?.SelectSingleNode("wpml:shootingPointLocation", xmm)?.InnerText),
                    });
                }

                var height = double.Parse(placemark.SelectSingleNode("wpml:height", xmm).InnerText);
                lis.Add(new DJIWayPoint
                {
                    lat = double.Parse(arr[1]),
                    lon = double.Parse(arr[0]),
                    ellipsoidHeight = ellipsoidHeight,
                    height = height,
                    index = int.Parse(index?.InnerText ?? (indexInt.ToString())),
                    shoots = shoots,
                });
                indexInt++;
            }
            return lis;
        }
        private static Dictionary<string, string> GetImageMeta(MemoryStream ms)
        {
            Dictionary<string, string> imgAttrs = new Dictionary<string, string>();
            try
            {
                using (var img = new MagickImage(ms))
                {
                    var profile = img.GetExifProfile();
                    var xml = img.GetXmpProfile();
                    var xdoc = xml.ToXDocument();
                    Dictionary<string, dynamic> rootElements = new Dictionary<string, dynamic>();
                    foreach (var ele in xdoc.Root.Elements())
                    {
                        Dictionary<string, dynamic> dic1 = new Dictionary<string, dynamic>();
                        foreach (var dec in ele.Elements())
                        {
                            Dictionary<string, string> ats = new Dictionary<string, string>();
                            var fj = dec.Attributes().FirstOrDefault(s => s.Name.LocalName == "drone-dji");
                            var djiname = fj?.Value ?? "http://www.dji.com/drone-dji/1.0/";

                            foreach (var attr in dec.Attributes())
                            {
                                if (attr.Name.Namespace == djiname)
                                    ats.TryAdd(attr.Name.LocalName, attr.Value);
                            }
                            dic1.Add(dec.Name.LocalName, ats);
                        }
                        rootElements.Add(ele.Name.LocalName, dic1);
                    }
                    imgAttrs = rootElements["RDF"]["Description"] as Dictionary<string, string>;


                    var lat = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLatitude);
                    var lon = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSLongitude);
                    var alt = profile.Values.FirstOrDefault(s => s.Tag == ExifTag.GPSAltitude);
                    var tslat = lat.GetValue() as ImageMagick.Rational[];
                    var tslon = lon.GetValue() as ImageMagick.Rational[];
                    var dalt = ((ImageMagick.Rational)alt.GetValue()).ToDouble();
                    var dlat = tslat[0].ToDouble() + tslat[1].ToDouble() / 60 + tslat[2].ToDouble() / 3600;
                    var dlon = tslon[0].ToDouble() + tslon[1].ToDouble() / 60 + tslon[2].ToDouble() / 3600;
                    imgAttrs.Add("lat", dlat.ToString());
                    imgAttrs.Add("lon", dlon.ToString());
                    imgAttrs.Add("alt", dalt.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }


            return imgAttrs;
        }
    }
}