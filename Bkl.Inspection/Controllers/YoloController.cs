using Bkl.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Bkl.Models.LocalContext;
using Bkl.Inspection.Bussiness;

namespace Bkl.Inspection
{
    [ApiController]
    [Authorize]
    [Route("[controller]")]
    public class YoloController : Controller
    {
        private IServiceProvider serviceProvider;
        BklDbContext context;
        private ILogger<InspectionController> logger;
        private IBackgroundTaskQueue<GenerateAllTaskRequest> taskQueue;

        public YoloController(BklDbContext context, IServiceProvider serviceProvider,
         IBackgroundTaskQueue<GenerateAllTaskRequest> taskqueue,
         IBackgroundTaskQueue<DetectTaskInfo> taskInfoQueue,
         ILogger<InspectionController> logger)
        {
            this.serviceProvider = serviceProvider;
            this.context = context;
            this.logger = logger;
            this.taskQueue = taskqueue;
        }
        [HttpGet("classes")]
        public IActionResult GetArray(string type, int page, int pagesize)
        {
            var classes = System.IO.File.ReadAllLines("classmaps.txt");
            var arr = classes.Select(s => s.Split('\t'))
                    .Where(s => s.Length >= 2)
                 .Select(s => new { name = s[0], namecn = s[2], count = s[1] })
                 .ToArray();
            return Json(arr);
        }

        [HttpGet("samples")]
        public IActionResult GetSamples(string type, int page, int pagesize)
        {
            var types = type.Split(',');
            var labels = JsonSerializer.Deserialize<List<XmlLabel>>(System.IO.File.ReadAllText("data.json"));
            var arr1 = labels.GroupBy(s => s.file)
                .Where(s => s.Any(q => types.Contains(q.name)));
               
            var filterted = arr1.Skip((page-1) * pagesize)
                .Take(pagesize)
                .ToList()
                .Select(s => new { file = s.First().file, rects = s.Select(t => new { t.name, t.xmin, t.xmax, t.ymin, t.ymax }).ToArray() });
            return Json(new { data = filterted, total = arr1.Count(), });
        }


        [HttpGet("get-yolo-path")]
        public IActionResult GetYoloPath([FromServices] BklLocalDbContext context)
        {
            var paths = context.BklLocalYoloPath.ToList();
            return Json(paths);
        }
        [HttpGet("search-yolo-dataset")]
        public IActionResult GetYoloSearchResult([FromServices] BklLocalDbContext context, [FromQuery] string yoloPath, [FromQuery(Name = "error")] string[] errors, [FromQuery] int page = 1, [FromQuery] int pagesize = 30)
        {
            IQueryable<BklLocalYoloDataSet> errorsQuery = context.BklLocalYoloDataSet.Where(s => s.DirName == yoloPath);
            if (errors != null && errors.Length > 0)
            {
                errorsQuery = errorsQuery.Where(s => errors.Contains(s.ClassName));
            }
            int total = errorsQuery.Count();
            var statistic = errorsQuery.OrderBy(s => s.Id).GroupBy(s => s.ClassName).Select(s => new { type = s.Key, count = s.Count() }).ToList();
            var data = errorsQuery.Skip((page - 1) * pagesize).Take(pagesize).ToList();
            return Json(new { total = total, page = page, statistic = statistic, data = data });

        }

        [HttpDelete("delete-yolo-data")]
        public IActionResult DeleteYoloData([FromServices] BklLocalDbContext context, int id)
        {
            var item = context.BklLocalYoloDataSet.FirstOrDefault(s => s.Id == id);
            context.BklLocalYoloDataSet.Remove(item);
            context.SaveChanges();
            return Json("");
        }
        [HttpPost("update-yolo-data")]
        public IActionResult UpdateYoloSearchResult([FromServices] BklLocalDbContext context, [FromBody] UpdateYoloDataSet request)
        {
            var dir = context.BklLocalYoloPath.FirstOrDefault(s => s.DirName == request.DirName);
            if (request.RectIds != null)
            {
                var setting = JsonSerializer.Deserialize<YoloSetting[]>(dir.YoloSetting);
                var statistic = JsonSerializer.Deserialize<Dictionary<string, int>>(dir.ClassStatistic);
                var type = setting.Where(s => s.type == request.ClassName).FirstOrDefault(); ;
                var ids = request.RectIds;
                var sets = context.BklLocalYoloDataSet.Where(s => s.DirName == request.DirName && ids.Contains(s.RectId)).ToList();
                foreach (var item in sets)
                {

                    var raw1 = JsonSerializer.Deserialize<BklRectInfo>(item.RawPoints);
                    var raw2 = JsonSerializer.Deserialize<YoloRectInfo>(item.YoloPoints);
                    raw1.ClsName = request.ClassName;
                    raw1.ClsId = type.value.ToString();
                    raw2.ClsId = type.value.ToString();
                    item.RawPoints = JsonSerializer.Serialize(raw1);
                    item.YoloPoints = JsonSerializer.Serialize(raw2);
                    item.ClassName = request.ClassName;
                }
                var lis = context.BklLocalYoloDataSet.Where(s => s.DirName == request.DirName)
                    .GroupBy(s => s.ClassName).Select(s => new { name = s.Key, value = s.Count() }).ToList(); ;
                dir.ClassStatistic = JsonSerializer.Serialize(lis.ToDictionary(q => q.name, q => q.value));
                context.SaveChanges();
            }
            return Json(dir);
        }
        [HttpPost("generate-yolo-dataset")]
        public async Task<IActionResult> GenerateYoloDataset([FromServices] BklConfig config, [FromServices] BklLocalDbContext context, [FromQuery] string dirName)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append($"rm -rf /data2/espsdata/YoloDataSet/{dirName}\n");
            sb.Append($"mkdir /data2/espsdata/YoloDataSet/{dirName}\n");
            var dirs = context.BklLocalYoloDataSet.Where(s => s.DirName == dirName).ToList();
            HashSet<string> files = new HashSet<string>();
            foreach (var item in dirs.OrderBy(s => s.TaskDetailId))
            {
                var yoloP = JsonSerializer.Deserialize<YoloRectInfo>(item.YoloPoints);
                string picName = item.Path.Split('/')[1].Split('.')[0];
                sb.Append($"echo \"{yoloP.ClsId} {yoloP.CenterX} {yoloP.CenterY} {yoloP.W} {yoloP.H}\">>/data2/espsdata/YoloDataSet/{dirName}/{picName}.txt\n");
                files.Add(item.Path);
            }
            foreach (var path in files)
            {
                var fname = path.Split('/')[1];
                sb.Append($"cp /data2/minio_data/{path} /data2/espsdata/YoloDataSet/{dirName}/{fname}\n");
            }
            string filename = Guid.NewGuid().ToString("N") + "-gen-yolo-dataset.sh";
            using (FileStream fs = new FileStream(Path.Join(config.FileBasePath, filename), FileMode.Create))
            {
                StreamWriter sw = new StreamWriter(fs);
                await sw.WriteAsync(sb);
                await fs.FlushAsync();
            }
            return File(new FileStream(Path.Join(config.FileBasePath, filename), FileMode.Open), "text/plain");
        }
        [HttpPost("save-yolo-dataset")]
        public IActionResult ExportYoloDataset(
            [FromServices] BklConfig config,
            [FromServices] BklDbContext context,
            [FromServices] BklLocalDbContext localContext,
            [FromBody] ExportYoloRequest request)
        {
            var taskDetailIds = request.TaskDetailIds;
            var taskIds = request.TaskIds;
            var facilityIds = request.FacilityIds;
            var factoryIds = request.FactoryIds;

            var errorMap = request.YoloSetting.ToDictionary(s => s.type, s => s.value.ToString());
            var errorNames = request.YoloSetting.Select(s => s.type).ToArray();
            var dirName = request.DirName;

            Func<string, int, IQueryable<BklInspectionTaskResult>> buildQuery = (type, count) =>
            {
                var query1 = context.BklInspectionTaskResult
                   .Where(s => type == s.DamageType);
                if (request.TaskIds != null && request.TaskIds.Length > 0)
                {
                    query1 = query1.Where(s => taskIds.Contains(s.TaskDetailId));
                }
                if (request.FacilityIds != null && request.FacilityIds.Length > 0)
                {
                    query1 = query1.Where(s => facilityIds.Contains(s.FacilityId));
                }
                if (request.FactoryIds != null && request.FactoryIds.Length > 0)
                {
                    query1 = query1.Where(s => factoryIds.Contains(s.FactoryId));
                }
                if (request.TaskDetailIds != null && request.TaskDetailIds.Length > 0)
                {
                    query1 = query1.Where(s => taskDetailIds.Contains(s.TaskDetailId));
                }
                query1 = query1.Join(context.BklInspectionTask, (p => p.TaskId), q => q.Id, (p, q) => p);
                return query1.OrderByDescending(s => s.Id).Take(count);
            };
            List<BklInspectionTaskResult> errors = new List<BklInspectionTaskResult>();
            foreach (var st in request.YoloSetting)
            {
                errors.AddRange(buildQuery(st.type, st.choose));
            }
            var detailsId = errors.Select(s => s.TaskDetailId).ToArray();
            var details = context.BklInspectionTaskDetail.Where(s => detailsId.Contains(s.Id)).ToArray();
            var yoloPath = localContext.BklLocalYoloPath.FirstOrDefault(s => s.DirName == request.DirName);
            Dictionary<string, int> typeStatistic = new Dictionary<string, int>();
            if (yoloPath == null)
            {
                yoloPath = new Models.LocalContext.BklLocalYoloPath
                {
                    DirName = request.DirName,
                    YoloSetting = JsonSerializer.Serialize(request.YoloSetting),
                };
                localContext.Add(yoloPath);
            }
            else
            {
                var yoloSetting = JsonSerializer.Deserialize<List<YoloSetting>>(yoloPath.YoloSetting);
                foreach (var item in request.YoloSetting)
                {
                    var aaa = yoloSetting.FirstOrDefault(s => s.type == item.type);
                    if (aaa != null)
                    {
                        aaa.choose = item.choose;
                        aaa.value = item.value;
                        aaa.total = item.total;
                    }
                    else
                    {
                        yoloSetting.Add(item);
                    }
                }
                yoloPath.YoloSetting = JsonSerializer.Serialize(yoloSetting);
                typeStatistic = JsonSerializer.Deserialize<Dictionary<string, int>>(yoloPath.ClassStatistic);
            }
            List<BklLocalYoloDataSet> yolosets = new List<BklLocalYoloDataSet>();

            var errorIds = localContext.BklLocalYoloDataSet.Where(s => s.DirName == yoloPath.DirName).Select(s => s.RectId).ToArray();

            foreach (var samePic in errors.GroupBy(q => q.TaskDetailId))
            {
                var detail = details.FirstOrDefault(q => q.Id == samePic.Key);
                if (detail == null)
                {
                    Console.WriteLine("error not found " + samePic.Key);
                    continue;
                }
                var W = double.Parse(detail.ImageWidth);
                var H = double.Parse(detail.ImageHeight);
                List<BklRectInfo> rects = new List<BklRectInfo>();
                List<YoloRectInfo> yrects = new List<YoloRectInfo>();
                foreach (var s in samePic)
                {
                    string clsNum = null;
                    if (errorMap.ContainsKey(s.DamageType))
                    {
                        clsNum = errorMap[s.DamageType];
                    }
                    if (string.IsNullOrEmpty(clsNum))
                        continue;

                    if (s.DamageX.IndexOf(",") > 0)
                    {
                        //多边形
                        var xs = s.DamageX.Split(",").Select(s => double.Parse(s)).ToList();
                        var ys = s.DamageY.Split(",").Select(s => double.Parse(s)).ToList();
                        double minx = xs.Min(), maxx = xs.Max(), miny = ys.Min(), maxy = ys.Max();
                        var w0 = maxx - minx;
                        var h0 = maxy - miny;

                        var centralX = (minx + w0 / 2) / W;
                        var centralY = (miny + h0 / 2) / H;
                        var w = w0 / W;
                        var h = h0 / H;

                        BklRectInfo rect = new BklRectInfo
                        {
                            ClsId = clsNum,
                            RectId = s.Id,
                            ClsName = s.DamageType,
                            Points = new double[xs.Count][]
                        };
                        foreach (var i in Enumerable.Range(0, xs.Count))
                        {
                            rect.Points[i] = new double[] { xs[i], ys[i] };
                        }
                        YoloRectInfo yrect = new YoloRectInfo
                        {
                            ClsId = clsNum,
                            CenterX = centralX.ToString("0.000000"),
                            CenterY = centralY.ToString("0.000000"),
                            W = w.ToString("0.000000"),
                            H = h.ToString("0.000000"),
                        };
                        rects.Add(rect);
                        yrects.Add(yrect);
                    }
                    else
                    {
                        //'falling_off' 0 ,'corrosion' 1 ,'crackle' 2, 'lightning_strike' 3, 'greasy_dirt' 4,'lightning_receiver' 5,'fujian_tuoluo' 6
                        var x = double.Parse(s.DamageX);
                        var y = double.Parse(s.DamageY);
                        var w0 = double.Parse(s.DamageWidth);
                        var h0 = double.Parse(s.DamageHeight);
                        //正方形
                        var centralX = (double.Parse(s.DamageX) + w0 / 2) / W;
                        var centralY = (double.Parse(s.DamageY) + h0 / 2) / H;
                        var w = w0 / W;
                        var h = h0 / H;
                        BklRectInfo rect = new BklRectInfo
                        {
                            ClsId = clsNum,
                            RectId = s.Id,
                            ClsName = s.DamageType,
                            Points = new double[4][]
                            {
                                new double[]{ x,y},
                                new double[]{x+ w0, y },
                                new double[]{x+ w0, y+h0, },
                                new double[]{ x,y+h0},
                            }
                        };
                        YoloRectInfo yrect = new YoloRectInfo
                        {
                            ClsId = clsNum,
                            CenterX = centralX.ToString("0.000000"),
                            CenterY = centralY.ToString("0.000000"),
                            W = w.ToString("0.000000"),
                            H = h.ToString("0.000000"),
                        };
                        rects.Add(rect);
                        yrects.Add(yrect);
                    }

                }

                foreach (var i in Enumerable.Range(0, rects.Count))
                {
                    if (!errorIds.Contains(rects[i].RectId))
                    {
                        if (typeStatistic.TryGetValue(rects[i].ClsName, out var ct))
                        {
                            ct++;
                            typeStatistic[rects[i].ClsName] = ct;
                        }
                        else
                        {
                            typeStatistic.TryAdd(rects[i].ClsName, 1);
                        }


                        yolosets.Add(new Models.LocalContext.BklLocalYoloDataSet
                        {
                            DirName = yoloPath.DirName,
                            Path = detail.RemoteImagePath,
                            FacilityId = detail.FacilityId,
                            FactoryId = detail.FactoryId,
                            TaskId = detail.TaskId,
                            TaskDetailId = detail.Id,
                            ClassName = rects[i].ClsName,
                            RectId = rects[i].RectId,
                            RawPoints = JsonSerializer.Serialize(rects[i]),
                            YoloPoints = JsonSerializer.Serialize(yrects[i]),
                        });
                    }
                }
            }
            localContext.BklLocalYoloDataSet.AddRange(yolosets);
            localContext.SaveChanges();
            var lis = localContext.BklLocalYoloDataSet.Where(s => s.DirName == request.DirName)
                  .GroupBy(s => s.ClassName).Select(s => new { name = s.Key, value = s.Count() }).ToList(); ;
            yoloPath.ClassStatistic = JsonSerializer.Serialize(lis.ToDictionary(q => q.name, q => q.value));
            localContext.SaveChanges();
            return Json(new { yoloPath = yoloPath, yoloSets = yolosets });
        }
    }
}
