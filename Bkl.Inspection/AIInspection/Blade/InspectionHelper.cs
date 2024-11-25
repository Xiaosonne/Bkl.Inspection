using Bkl.Infrastructure;
using Bkl.Models;
using System.Text.Json;
using System;
using System.Linq;
using System.Collections.Generic;
using StackExchange.Redis;
using DocumentFormat.OpenXml.Office2010.ExcelAc;

public static class InspectionHelper
{
    static Dictionary<string, string> typeEnMap = new Dictionary<string, string>{
            { "falling_off", "表面缺陷-胶衣脱落" },
            {"gel_off", "表面缺陷-胶衣脱落"},
            {"corrosion", "前缘腐蚀-胶衣腐蚀"},
            {"glass_corrosion", "前缘腐蚀-玻纤腐蚀"},
            {"crackle", "表面裂纹-胶衣裂纹"},
            {"beacon_paint_off", "表面缺陷-航标漆脱落"},
            {"lightning_strike", "表面缺陷-雷击损伤"},
            {"thunderstrike", "表面缺陷-雷击损伤"},
            {"greasy_dirt", "表面缺陷-表面油污"},
            {"lightning_receiver", "附件脱落损伤-接闪器"},

        };
    public static IEnumerable<BklInspectionTaskResult> GetResult(IRedisClient redis, long taskId, long facilityId, long factoryId)
    {
        //image and rect
        var dict = redis.GetValuesFromHash($"FuseTaskResult:Tid.{taskId}.Faid.{facilityId}")
            .ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<FuseImagesResponse>((string)s.Value));

        //var allPic = dict.Select(s =>
        //{
        //    var resp = s.Value;
        //    return resp.images.Select(ss =>
        //    {
        //        dynamic data = new ExtensionDynamicObject(ss);
        //        data.mainPath = s.Key;
        //        data.savePath = resp.save_path;
        //        return data;
        //    });
        //})
        //    .Aggregate(new List<dynamic>(), (pre, cur) =>
        //    {
        //        pre.AddRange(cur);
        //        return pre;
        //    })
        //    .ToDictionary(s => (string)s.mainPath, s => s);

        //image and error
        var errors = redis.GetValuesFromHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}.Filterd").
             ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<YoloResult[]>((string)s.Value));
        if (errors.Count == 0)
        {
            errors = redis.GetValuesFromHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}").
             ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<YoloResult[]>((string)s.Value))
             .Where(s => s.Value.Length > 0)
             .ToDictionary(s => s.Key, s => s.Value);
            redis.SetRangeInHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}.Filterd", errors.ToDictionary(s => s.Key, s => (RedisValue)JsonSerializer.Serialize(s.Value)));
        }
        int totalLen = 45;
        List<object> fuzeResult = new List<object>();
        foreach (var posAndFuseResponse in dict)
        {
            var resp = posAndFuseResponse.Value;
            var pt = resp.images[0];
            List<(int, int)> lis = new List<(int, int)>();
            List<(string path, int w, int h, int pixelLength)> lis2 = new List<(string path, int w, int h, int pixelLength)>();
            lis.Add((0, 0));
            foreach (var i in Enumerable.Range(1, resp.images.Length - 1))
            {
                lis.Add((-resp.images[i].x, -resp.images[i].y));
            }
            //这里应该加上第一个图片叶片的长宽
            lis2.Add((resp.images[0].path, resp.images[0].w, resp.images[0].h, resp.images[0].w));

            lis2.Add((resp.images[1].path, resp.images[1].x, resp.images[1].y, resp.images[0].w + Math.Abs(resp.images[1].x)));
            for (var i = 2; i < resp.images.Length; i++)
            {
                var w = -resp.images[i].x - (-resp.images[i - 1].x);
                var h = -resp.images[i].y - (-resp.images[i - 1].y);
                lis2.Add((resp.images[i].path, w, h, lis2[i - 1].pixelLength + Math.Abs(w)));
            }
            //按总长加一起计算叶片像素长度
            int totalPixelW = lis2.Sum(s => s.w) + pt.w;
            int totalPixelH = lis2.Sum(s => s.h);
            int n = 2;
            List<double> sigmas = new List<double>();
            for (int i = n; i < lis.Count; i++)
            {

                var wbar = lis.Take(i).Sum(s => s.Item1) / i;
                var wsigma = Math.Sqrt(Math.Pow(lis.Take(i).Sum(s => s.Item1 - wbar), 2) / i);
                sigmas.Add(wsigma);
            }
            Console.WriteLine(posAndFuseResponse.Key + " " + string.Join(",", sigmas));
            double totalWidth = Math.Sqrt(Math.Pow(totalPixelW, 2) + Math.Pow(totalPixelH, 2));
            //按每段像素长度加一起
            double sumPiecePic = lis2.Select(s => Math.Sqrt(Math.Pow(s.w, 2) + Math.Pow(s.h, 2))).Sum();
            foreach (var pathAndYoloResults in errors)
            {
                var sizeInfo = lis2.FirstOrDefault(s => s.path == pathAndYoloResults.Key);
                if (sizeInfo.path == null)
                    continue;
                var errSize = pathAndYoloResults.Value.Select(x => (yolo: x, cx: 0.08 * (x.xmax + x.xmin) / 2, cy: 0.08 * (x.ymax + x.ymin) / 2, w: 0.08 * (x.xmax - x.xmin), h: 0.08 * (x.ymax - x.ymin))).ToArray();
                var len = lis2.First(s => s.path == pathAndYoloResults.Key);
                foreach (var esize in errSize)
                {
                    var img = resp.images.FirstOrDefault(s => s.path == pathAndYoloResults.Key);
                    var bladeLen = totalLen * ((len.pixelLength - len.w + esize.cx) / totalPixelW * 1.0);
                    var esizeW = totalLen * (len.w * 1.0 / totalPixelW) * (esize.w / 320.0) * 100;
                    var esizeH = totalLen * (len.h * 1.0 / totalPixelH) * (esize.h / 240.0) * 100;
                    var dya = new BklInspectionTaskResult
                    {
                        FacilityId = facilityId,
                        TaskId = taskId,
                        //DamageSize = $"{(esizeW).ToString("F2")}cm×{(esizeH).ToString("F2")}cm",
                        DamageSize = $"{(esizeW).ToString("F2")}cm×{(esizeH).ToString("F2")}cm",
                        DamageType = typeEnMap.ContainsKey(esize.yolo.name) ? typeEnMap[esize.yolo.name] : esize.yolo.name,
                        DamageDescription = pathAndYoloResults.Key,
                        DamageHeight = ((int)esize.yolo.xmax - (int)esize.yolo.xmin).ToString(),
                        DamageWidth = ((int)esize.yolo.ymax - (int)esize.yolo.ymin).ToString(),
                        DamageX = $"{(int)esize.yolo.xmin},{(int)esize.yolo.xmax},{(int)esize.yolo.xmax},{(int)esize.yolo.xmin}",
                        DamageY = $"{(int)esize.yolo.ymin},{(int)esize.yolo.ymin},{(int)esize.yolo.ymax},{(int)esize.yolo.ymax}",
                        DamageLevel = "1",
                        DamagePosition = $"距离叶根部约{bladeLen.ToString("F2")}m",
                        Position = posAndFuseResponse.Key,
                        TreatmentSuggestion = JsonSerializer.Serialize(new double[]{
                                esize.yolo.xmin * 0.8 + img.x,
                                esize.yolo.ymin * 0.8 + img.y,
                                0.8 * (esize.yolo.xmax - esize.yolo.xmin),
                                0.8 * (esize.yolo.xmax - esize.yolo.xmin),
                                0,
                                0
                            })
                    }; ;
                    //dya.path = pathAndYoloResults.Key;
                    yield return dya;
                }

            }
        }
    } 
     

    /// <summary>
    /// 
    /// </summary>
    /// <param name="results"></param>
    /// <param name="orderPath"></param>
    /// <param name="saveDis"></param>
    /// <param name="picDis"></param>
    /// <param name="insideDis"></param>
    /// <returns>key path  value yoloresults</returns>
    public static Dictionary<string, YoloResult[]> FilterResult(
        Dictionary<string, YoloResult[]> results,
        Dictionary<string, BladeExtraInfo> orderPath,
        int saveDis = 2500, int picDis = 200, long insideDis = 5)
    {
        //var results = redis
        //    .GetValuesFromHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}")
        //    .ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<YoloResult[]>(s.Value.ToString()));

        //var orderPath = redis.GetValuesFromHash($"Task.{taskId}:Facility.{facilityId}").ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<BladeExtraInfo>((string)s.Value));

        CompareYoloResult[] parseDicPath(KeyValuePair<string, YoloResult[]> kvPair)
        {
            var arr = kvPair.Key.Split('/')[1].Split("_");
            int order = 0;
            var ok = arr.Length >= 3 ? int.TryParse(arr[2], out order) : false;
            var path = orderPath.Where(s => (s.Value.StartIndex >= order && s.Value.EndIndex <= order) || (s.Value.StartIndex <= order && s.Value.EndIndex >= order)).Select(s => new { gp = s.Key, index = s.Value }).FirstOrDefault();
            ok = ok && path != null;
            //var yos = JsonSerializer.Deserialize<YoloResult[]>(s.Value.ToString());
            return kvPair.Value.Select(s => new CompareYoloResult(s)
            {
                ok = ok,
                order = order,
                pic = kvPair.Key,
                path = path?.gp,
                info = path?.index
            }).ToArray();
        }
        float distance(CompareYoloResult r, CompareYoloResult l)
        {
            var val = Math.Abs(r.xmin - l.xmin) +
                Math.Abs(r.xmax - l.xmax) +
                Math.Abs(r.ymin - l.ymin) +
                Math.Abs(r.ymax - l.ymax) + Math.Abs(r.order - l.order) * picDis;
            Console.WriteLine($"{r.order} {l.order} {r.pic} {l.pic} {val}");
            return val;
        }
        float areaSize(CompareYoloResult s)
        {
            return (s.xmax - s.xmin) * (s.ymax - s.ymin);
        }
        bool inside(CompareYoloResult r, CompareYoloResult l)
        {
            return ((l.xmin - r.xmin) + (r.xmax - l.xmax)) > insideDis &&
               ((l.ymin - r.ymin) + (r.ymax - l.ymax)) > insideDis;
        }
        List<Dictionary<int, List<CompareYoloResult>>> lis = new List<Dictionary<int, List<CompareYoloResult>>>();
        foreach (var samePath in results
            .Select(parseDicPath)
            .Aggregate(new List<CompareYoloResult>(), (pre, cur) =>
            {
                pre.AddRange(cur);
                return pre;
            })
            .Where(s => s.ok)
            .GroupBy(s => s.path))
        {
            var first = samePath.First();
            //叶根>叶尖 true 顺序  false 逆序  
            var order = first.info.StartIndex > first.info.EndIndex;
            var yejianyegen = samePath.OrderBy(s => order ? s.order : -s.order).ToArray();
            var sameOrders = yejianyegen.GroupBy(s => s.order).ToArray();
            //class errors
            Dictionary<int, List<CompareYoloResult>> dict = new Dictionary<int, List<CompareYoloResult>>();
            int curIndex = sameOrders[0].First().order;
            for (int i = 0; i < sameOrders.Length; i++)
            {
                var sameOrder = sameOrders[i];
                dict = sameOrder.Where(s => !dict.ContainsKey(s.@class))
                    .Aggregate(dict, (pre, cur) =>
                    {
                        pre.Add(cur.@class, new List<CompareYoloResult>());
                        return pre;
                    });
                List<CompareYoloResult> tempLis = new List<CompareYoloResult>();
                dict = sameOrder
                    .GroupBy(s => s.@class)
                    .Select(sameGroup => sameGroup
                            //根据面积排序
                            .OrderByDescending(areaSize)
                            //按面积把最大快放进去
                            .Aggregate(new List<CompareYoloResult>(), (pre, cur) =>
                            {
                                if (pre.All(p => !inside(p, cur)))
                                    pre.Add(cur);
                                return pre;
                            }))
                    .Aggregate(new List<CompareYoloResult>(), (pre, cur) =>
                    {
                        pre.AddRange(cur);
                        return pre;
                    })
                    //未添加的根据距离计算大于saveOrderThreshold保存到结果里面
                    .Where(notIn => dict[notIn.@class].All(inList => distance(inList, notIn) > saveDis))
                    .Aggregate(dict, (pre, cur) =>
                    {
                        pre[cur.@class].Add(cur);
                        return pre;
                    });
                //foreach (var sameGroup in sameOrder.GroupBy(s => s.@class))
                //{
                //    tempLis.AddRange(sameGroup.OrderByDescending(areaSize)
                //        .Aggregate(new List<CompareYoloResult>(), (pre, cur) =>
                //        {
                //            if (pre.All(p => !inside(p, cur)))
                //                pre.Add(cur);
                //            return pre;
                //        }));
                //}
                //foreach (var item in sameOrder)
                //{
                //    var saved = dict[item.@class];
                //    if (saved.All(s => distance(s, item) > saveOrderTreshold))
                //    {
                //        saved.Add(item);
                //    }
                //}
            }
            lis.Add(dict);
        }

        return lis.Select(s => s.Values.ToArray())
            .Aggregate(new List<CompareYoloResult>(), (pre, cur) =>
            {
                cur.ToList().ForEach(s =>
                {
                    pre.AddRange(s);
                });
                //pre.AddRange(cur);
                return pre;
            })
            .GroupBy(s => s.pic)
            .ToDictionary(s => s.Key, s => s.Select(q => q.Result).ToArray()); ;
    }


    public static Dictionary<string, YoloResult[]> FilterResult(IRedisClient redis, long taskId, long facilityId, int saveDis = 2500, int picDis = 200, long insideDis = 5)
    {
        var results = redis
            .GetValuesFromHash($"DetectTaskResult:Tid.{taskId}.Faid.{facilityId}")
            .ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<YoloResult[]>(s.Value.ToString()));

        var orderPath = redis.GetValuesFromHash($"Task.{taskId}:Facility.{facilityId}").ToDictionary(s => s.Key, s => JsonSerializer.Deserialize<BladeExtraInfo>((string)s.Value));

        return FilterResult(results, orderPath, saveDis, picDis, insideDis);
    }
}
