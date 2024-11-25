using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Collections.Generic;
using Bkl.Models;
using Bkl.Infrastructure;
using System.Linq;
using Minio;
using System.Reactive.Linq;
using System;
using DocumentFormat.OpenXml.Packaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.Fonts;
using System.IO;

public record ReportErrorDetail(string filename, string oldfile, string damagetype, string damagedescription, string damageposition, string damagesize, string position);

public class CreateFJExportNoOpenCVParagraph : ICreateParagraph
{
    private BklConfig _config;
    public CreateFJExportNoOpenCVParagraph(
        BklConfig config,
        WordprocessingDocument doc,
        IRedisClient redis,
        List<BklInspectionTaskDetail> details,
        List<BklInspectionTaskResult> results,
        BklFactory fac,
        BklFactoryFacility faci,
        string mode = null)
    {
        _mode = mode;
        _faci = faci;
        _fac = fac;
        _results = results;
        _taskDetails = details;
        _redis = redis;
        _doc = doc;
        _tempFiles = new List<string>();
        _config = config;
    }
    const int wordWidth = 11906;
    private List<string> _tempFiles;
    private string _mode;
    private BklFactoryFacility _faci;
    private BklFactory _fac;
    private List<BklInspectionTaskResult> _results;
    private List<BklInspectionTaskDetail> _taskDetails;
    private IRedisClient _redis;
    private WordprocessingDocument _doc;
    const int emu_per_dxa = 635;
    public static string PercentWidth(int pers)
    {
        return ((pers * 8505) / 100).ToString();
    }
    public OpenXmlElement AddParagraph()
    {
        var minio = new MinioClient().WithEndpoint("192.168.31.173:9000").WithCredentials("minioadmin", "minioadmin").WithRegion("cn-ha-zz").Build();

        List<Table> tbs = new List<Table>();
        var vals = _redis.GetValuesFromHash($"FacilityMeta:{_faci.Id}");
        var table = new Table();
        TableProperties tblProp = new TableProperties(
                new TableWidth() { Width = "8505", Type = TableWidthUnitValues.Dxa },
                new TableJustification() { Val = TableRowAlignmentValues.Center },
                new TableBorders(
                        new TopBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
                        new LeftBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U },
                        new BottomBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
                        new RightBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U },
                        new InsideHorizontalBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)4U, Space = (UInt32Value)0U },
                        new InsideVerticalBorder() { Val = BorderValues.Single, Color = "auto", Size = (UInt32Value)4U, Space = (UInt32Value)0U }
                ),
                new TableCellMarginDefault(
                    new TableCellLeftMargin() { Width = 108, Type = TableWidthValues.Dxa },
                    new TableCellRightMargin() { Width = 108, Type = TableWidthValues.Dxa }),
                new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
        );

        TableGrid tableGrid1 = new TableGrid();

        // GridColumn gridColumn3 = new GridColumn() { Width = PercentWidth(50) };
        // GridColumn gridColumn4 = new GridColumn() { Width = PercentWidth(50) };

        tableGrid1.Append(new GridColumn() { Width = "498" });
        tableGrid1.Append(new GridColumn() { Width = "2669" });
        tableGrid1.Append(new GridColumn() { Width = "2669" });
        tableGrid1.Append(new GridColumn() { Width = "2669" });
        // tableGrid1.Append(new GridColumn() { Width = PercentWidth(50) });
        // tableGrid1.Append(new GridColumn() { Width = PercentWidth(50) });

        // Append the TableProperties object to the empty table.
        table.AppendChild<TableProperties>(tblProp);
        table.Append(tableGrid1);
        foreach (var sameYpErrors in _results.GroupBy(s => s.Position.Split('/')[0]).OrderBy(s => s.Key))
        {



            // Create a TableProperties object and specify its border information.


            // TableRow tr1 = new TableRow();
            // TableCell tc1 = new TableCell(new TableCellProperties(
            ////     new TableCellWidth() { Width = PercentWidth(50), Type = TableWidthUnitValues.Dxa },
            //     new GridSpan { Val = 4 },
            //     new Paragraph(new Run(new Text($"叶片{yps.Key}编号：{vals["叶片" + yps.Key + "编号"]}")))
            // ));
            // table.Append(new TableRow(
            //     new TableCell(
            //         new TableCellProperties(
            ////             new TableCellWidth() { Width = PercentWidth(50), Type = TableWidthUnitValues.Dxa },
            //             new GridSpan { Val = 3 }),
            //         new Paragraph(
            //             new ParagraphProperties(new Justification { Val = JustificationValues.Center }),
            //              new Run(new Text($"{yps.Key}编号：{vals[yps.Key + "编号"]}")))
            // )));
            var tmpFileDir = Path.Combine(_config.FileBasePath, "TempFiles");
            if (!Directory.Exists(tmpFileDir))
                Directory.CreateDirectory(tmpFileDir);
            var gparr = sameYpErrors
                .Where(s => _mode != "test" || !s.DamageType.StartsWith("外观正常"))
                .GroupBy(s => s.TaskDetailId)
                .Select(yp => DrawErrorPerSingleImage(minio, yp, _mode))
                .Aggregate(new List<ReportErrorDetail>(), (acc, cur) =>
                {
                    acc.AddRange(cur);
                    return acc;
                })
                .OrderBy(s => s.oldfile).ToArray();

            TableRow trInfo = null;
            TableRow trImg = null;
            // TableRow trDetailInfo = null;
            // TableRow trDetailInfo2 = null;
            for (int k = 0; k < gparr.Length; k++)
            {
                var fname = gparr[k].filename;
                var dtype = gparr[k].damagetype;
                var dpos = gparr[k].damageposition;
                var pos = gparr[k].position;
                var dsize = gparr[k].damagesize;

                //var dtype, var sug, var dpos, var dsize, var pos) = gparr[k];
                Console.WriteLine($"GenerateReport {_fac.FactoryName} {_faci.Name} total:{gparr.Length} cur:{k} {dtype} {dpos} {dsize}");
                if ((k % 3) == 0)
                {
                    trInfo = new TableRow(new TableRowProperties(
                        new CantSplit(),
                        new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                        new TableJustification { Val = TableRowAlignmentValues.Center }));
                    trImg = new TableRow(new TableRowProperties(
                            new CantSplit(),
                            new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                            new TableJustification { Val = TableRowAlignmentValues.Center }
                    ));
                    //trDetailInfo = new TableRow();
                    //trDetailInfo2 = new TableRow();
                    table.Append(trImg);
                    table.Append(trInfo);

                    if (k == 0)
                    {
                        var tc1 = new TableCell();
                        tc1.Append(new TableCellProperties(
                              new VerticalMerge() { Val = MergedCellValues.Restart },
                              new GridSpan { Val = 1 },
                              new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
                          )
                        );
                        sameYpErrors.Key.ToArray()
                            .Select(s => createText(s.ToString()))
                            .ToList()
                            .ForEach(ch => { tc1.Append(ch); });

                        trImg.Append(tc1);

                        trInfo.Append(new TableCell(
                                               new TableCellProperties(
                                                new GridSpan { Val = 1 },
                                                   new VerticalMerge(),
                                                   new Paragraph()
                                               )
                                           ));
                    }
                    else
                    {
                        trImg.Append(new TableCell(
                            new TableCellProperties(
                                new GridSpan { Val = 1 },
                                  //new TableCellWidth() { Width = "498", Type = TableWidthUnitValues.Dxa },
                                  new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                                new VerticalMerge(),
                                 createText(dtype)
                            )
                        ));

                        trInfo.Append(new TableCell(
                                        new TableCellProperties(
                                            new GridSpan { Val = 1 },
                                             new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                                            new VerticalMerge(),
                                            new Paragraph()
                                        )
                                    ));
                    }

                }

                trInfo.Append(
                      new TableCell(
                          new TableCellProperties(
                            new GridSpan { Val = 1 },
                              //new TableCellWidth() { Width = "2655", Type = TableWidthUnitValues.Dxa },
                              new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                                createText(dtype)
                          ))
                    );
                var width = 1440000;
                if (_mode == "test")
                {
                    trImg.Append(
                        new TableCell(
                            new TableCellProperties(
                                new GridSpan { Val = 1 },
                                //new TableCellWidth() { Width = "2655", Type = TableWidthUnitValues.Dxa },
                                new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
                            ),
                            new CreateImageParagraph(fname, _doc, width, width).AddParagraph(),
                            createText(pos, line: new SpacingBetweenLines() { Line = "240", LineRule = LineSpacingRuleValues.Auto }),
                            createText(dpos, line: new SpacingBetweenLines() { Line = "240", LineRule = LineSpacingRuleValues.Auto }),
                            createText(dsize, line: new SpacingBetweenLines() { Line = "240", LineRule = LineSpacingRuleValues.Auto }))
                                   );
                }
                else
                {
                    trImg.Append(
                         new TableCell(
                            new TableCellProperties(
                                               new GridSpan { Val = 1 },
                                               //new TableCellWidth() { Width = "2655", Type = TableWidthUnitValues.Dxa },
                                               new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }
                                           ),
                               new CreateImageParagraph(fname, _doc, width, width).AddParagraph(),
                                               createText(dpos),
                                               createText(dsize))
                                   );
                }




                if ((k + 1) >= gparr.Length)
                {
                    var left = ((k + 1) % 3);
                    if (left != 0)
                    {
                        left = 3 - left;
                    }
                    foreach (var ks in Enumerable.Range(0, left))
                    {
                        trInfo.Append(new TableCell(
                            new TableCellProperties(new GridSpan { Val = 1 }),
                            createText("")
                        ));
                        trImg.Append(new TableCell(
                            new TableCellProperties(new GridSpan { Val = 1 }),
                            createText("")
                        ));
                    }
                }
            }
        }

        //C#  c sharp 
        var gps = _results.Where(s => !s.DamageType.Contains("外观正常"))
                        .GroupBy(s => s.DamageType.Split("-")[1]
                        // .Replace("锯齿尾缘", "锯齿尾缘损伤")
                        // .Replace("接闪器", "接闪器损伤")
                        // .Replace("锯齿尾缘", "锯齿尾缘损伤"))
                        .Replace("锯齿尾缘", "锯齿尾缘脱落损伤")
                        .Replace("接闪器", "接闪器脱落损伤")
                        .Replace("涡流板", "涡流板脱落损伤")
                        .Replace("扰流条", "扰流条脱落损伤"))
                        .Select(s => new { dtype = s.Key, yps = s.Select(m => m.Position.Split("/")[0]).Distinct() }).ToArray();
        List<string> sb = new List<string>();
        List<string> sb2 = new List<string>();
        List<string> sb3 = new List<string>();

        foreach (var gps2 in gps.GroupBy(s => s.yps.Count()).OrderByDescending(s => s.Key))
        {
            if (gps2.Key == 3)
                sb.Add("三个叶片都存在" + string.Join("、", gps2.Select(m => m.dtype)));
            if (gps2.Key == 2)
            {
                var mm = gps2.ToList().GroupBy(q => string.Join("、", q.yps.OrderBy(q => q)));
                foreach (var two in mm)
                {
                    // two.Aggregate()
                    sb2.Add($"{two.Key}存在" + string.Join("、", two.Select(m => m.dtype)));
                }
            }
            if (gps2.Key == 1)
            {
                foreach (var one in gps2.GroupBy(q => q.yps.FirstOrDefault()))
                {
                    sb3.Add($"{one.Key}存在{string.Join("、", one.Select(m => m.dtype))}");
                }
            }
        }
        sb2 = sb2.OrderBy(s => s).ToList();
        sb3 = sb3.OrderBy(s => s).ToList();
        sb2.ForEach(item => sb.Add(item));
        sb3.ForEach(item => sb.Add(item));


        table.Append(new TableRow(new TableCell(new TableCellProperties(
                            new CantSplit(),
                          //new TableCellWidth() { Width = "8505", Type = TableWidthUnitValues.Dxa },
                          new GridSpan { Val = 4 },
                            createText("检查结果：" + string.Join("现象；", sb) + "现象。", just: new Justification { Val = JustificationValues.Left })
                          ))));

        return new Paragraph(new ParagraphProperties(
            new Justification { Val = JustificationValues.Center },
            new AdjustRightIndent { Val = false },
             new SnapToGrid() { Val = false },
            new WidowControl(),
            new SpacingBetweenLines { Before = "60", After = "60" }),
            new Run(table, new Break() { Type = BreakValues.Page })
        );
    }

    Paragraph createText(string text, Justification just = null, SpacingBetweenLines line = null)
    {
        return new Paragraph(new ParagraphProperties(
                        new AdjustRightIndent { Val = false },
             new SnapToGrid() { Val = false },
                            line != null ? line : new SpacingBetweenLines() { Before = "60", After = "60", Line = "240", LineRule = LineSpacingRuleValues.Auto },
                             just == null ? new Justification { Val = JustificationValues.Center } : just,
                              new Run(new RunProperties(
                                        new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                                        new FontSizeComplexScript() { Val = "24" }),
                                new Text(text))));
    }
    Dictionary<string, SixLabors.ImageSharp.Color> colorMap = new Dictionary<string, SixLabors.ImageSharp.Color>
    {
        {"LV0",SixLabors.ImageSharp.Color.FromRgb(0x70, 0xad, 0x47) },
        {"LV1",SixLabors.ImageSharp.Color.FromRgb(0xff, 0xe7, 0x75) },
        {"LV2",SixLabors.ImageSharp.Color.FromRgb(0xff, 0xca, 0x59) },
        {"LV3",SixLabors.ImageSharp.Color.FromRgb(0xff, 0x92, 0x01) },
        {"LV4",SixLabors.ImageSharp.Color.FromRgb(0xe6, 0x4a, 0x19) },
        {"LV5",SixLabors.ImageSharp.Color.FromRgb(0x90, 0x12, 0x74) },
    };
    IEnumerable<ReportErrorDetail> DrawErrorPerSingleImage(MinioClient minio, IEnumerable<BklInspectionTaskResult> results, string mode)
    {
        var tmpFileDir = Path.Combine(_config.FileBasePath, "TempFiles");
        if (!Directory.Exists(tmpFileDir))
            Directory.CreateDirectory(tmpFileDir);

        FontCollection fonts = new FontCollection();
        fonts.Add("./simhei.ttf");
        var font = new SixLabors.Fonts.Font(fonts.Families.FirstOrDefault(), 20, FontStyle.Bold);
        var font1 = new SixLabors.Fonts.Font(fonts.Families.FirstOrDefault(), 40, FontStyle.Bold);
        foreach (var error in results)
        {
            var task = _taskDetails.Where(s => s.Id == error.TaskDetailId).FirstOrDefault();
            if (task == null)
            {
                Console.WriteLine($"NoTaskDetail {task.Id} {task.RemoteImagePath} {task.FacilityName}");
                continue;
            }
            var arr = task.RemoteImagePath.Split('/');
            var filename1 = Path.Combine(_config.MinioDataPath, arr[0], arr[1]);

            Console.WriteLine($"ReportRead {filename1}");
            using (var img = Image.Load(filename1))
            {
                if (!error.DamageType.Contains("外观正常"))
                    drawErrorShape(img, error);
                var newfilename = Guid.NewGuid().ToString() + Path.GetExtension(filename1);
                newfilename = Path.Combine(tmpFileDir, newfilename);
                Console.WriteLine($"ReportTempWrite {newfilename}");
                _tempFiles.Add(newfilename);
                img.Mutate(ctx =>
                {
                    ctx.Resize(new Size(img.Width / 4, img.Height / 4));
                });

                try
                {
                    var arr2 = arr[1].Split("_");
                    switch (mode)
                    {
                        case "test":
                            img.Mutate(ctx =>
                            {
                                ctx.DrawText($"{arr[1]}", font, SixLabors.ImageSharp.Color.FromRgb(0, 255, 0), new PointF(30, 30));
                            });
                            break;
                        case "levelmark":
                            img.Mutate(ctx =>
                            {
                                ctx.DrawText($"{arr[1]}", font, SixLabors.ImageSharp.Color.FromRgb(178, 169, 161), new PointF(30, 30));

                                int w = img.Width;
                                int h = img.Height;
                                var x = (int)(w * 0.9);
                                var w1 = (int)(w * 0.1);

                                if (colorMap.ContainsKey(error.DamageLevel))
                                {
                                    ctx.Fill(Brushes.Solid(colorMap[error.DamageLevel]), new RectangleF(x, 0, w1, h * 0.08f));
                                    ctx.DrawText($"{error.DamageLevel}", font1, SixLabors.ImageSharp.Color.FromRgb(0, 0, 0), new PointF(x + w1 * 0.25f, h * 0.1f * 0.1f));
                                }
                                else
                                {
                                    ctx.Fill(Brushes.Solid(SixLabors.ImageSharp.Color.FromRgb(0xcc, 0xcc, 0xcc)), new RectangleF(x, 0, w1, h * 0.08f));
                                    ctx.DrawText($"{error.DamageLevel}", font1, SixLabors.ImageSharp.Color.FromRgb(0, 0, 0), new PointF(x + w1 * 0.25f, h * 0.1f * 0.25f));
                                }
                            });
                            break;
                        default:
                            img.Mutate(ctx =>
                            {
                                ctx.DrawText($"{arr[1]}", font, SixLabors.ImageSharp.Color.FromRgb(178, 169, 161), new PointF(30, 30));
                            });
                            break;
                    }
                    //if (mode == "test")
                    //{
                    //    img.Mutate(ctx =>
                    //    {
                    //        ctx.DrawText($"{savingTags[1]}", font, SixLabors.ImageSharp.Color.FromRgb(0, 255, 0), new PointF(30, 30));
                    //    });
                    //    //new Emgu.CV.Structure.MCvScalar(161, 169, 178)
                    //    //Cv2.PutText(img, $"{savingTags[1]}", new OpenCvSharp.Point(30, 80), HersheyFonts.HersheyComplexSmall, 3, new Scalar(0, 0, 255), 1);
                    //}
                    //else
                    //{
                    //    img.Mutate(ctx =>
                    //    {
                    //        ctx.DrawText($"{savingTags[1]}", font, SixLabors.ImageSharp.Color.FromRgb(178, 169, 161), new PointF(30, 30));
                    //    });

                    //    //new Scalar(161, 169, 178)
                    //    //Cv2.PutText(img, $"{savingTags[1]}", new OpenCvSharp.Point(30, 40), HersheyFonts.HersheyComplexSmall, 1, new Scalar(161, 169, 178), 1);
                    //}

                }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }

                img.Save(newfilename);

                yield return new ReportErrorDetail(newfilename,
                    arr[1],
                    error.DamageType.Contains("外观正常") ? error.DamageType.Substring(5) : error.DamageType,
                    error.DamageDescription,
                    error.DamagePosition,
                    error.DamageSize,
                    task.Position);

            }
        }
    }

    IEnumerable<(string, string, string, string, string, string)> DrawMaxAreaSizeErrorImage(MinioClient minio, IGrouping<long, BklInspectionTaskResult> taskYP, string mode)
    {
        var task = _taskDetails.Where(s => s.Id == taskYP.Key).FirstOrDefault();
        var arr = task.RemoteImagePath.Split('/');
        var types = new List<string>();

        FontCollection fonts = new FontCollection();
        fonts.Add("./simhei.ttf");
        var font = new SixLabors.Fonts.Font(fonts.Families.FirstOrDefault(), 20, FontStyle.Bold);
        var font1 = new SixLabors.Fonts.Font(fonts.Families.FirstOrDefault(), 40, FontStyle.Bold);

        foreach (var shapeTypes in taskYP.GroupBy(s => s.DamageType))
        {
            var ordered = (from p in shapeTypes
                           join q in _taskDetails on p.TaskDetailId equals q.Id
                           orderby q.RemoteImagePath
                           select p);
            var filename1 = Path.Combine(_config.MinioDataPath, arr[0], arr[1]);
            var tmpFileDir = Path.Combine(_config.FileBasePath, "TempFiles");
            //using MemoryStream ms = new MemoryStream();
            //minio.GetObjectAsync(new GetObjectArgs().WithBucket(savingTags[0]).WithObject(savingTags[1]).WithCallbackStream(filestream =>
            // {
            //     filestream.CopyTo(ms);
            //     ms.Seek(0, SeekOrigin.Begin);
            // })).GetAwaiter().GetResult();

            if (!Directory.Exists(tmpFileDir))
                Directory.CreateDirectory(tmpFileDir);

            Console.WriteLine($"ReportRead {filename1}");
            using (var img = Image.Load(filename1))
            {
                if (!shapeTypes.Key.Contains("外观正常"))
                    drawErrorShape(img, ordered.ToArray());
                var newfilename = Guid.NewGuid().ToString() + Path.GetExtension(filename1);
                // var dir = System.IO.Path.Combine(_config.FileBasePath, "TempFiles");
                // if (!Directory.Exists(dir))
                // 	Directory.CreateDirectory(dir);
                newfilename = Path.Combine(tmpFileDir, newfilename);
                Console.WriteLine($"ReportTempWrite {newfilename}");

                _tempFiles.Add(newfilename);
                img.Mutate(ctx =>
                {
                    ctx.Resize(new Size(img.Width / 4, img.Height / 4));
                });
                //Cv2.PyrDown(img, img);
                //Cv2.PyrDown(img, img);
                var first = shapeTypes.Select(p => (p, p.DamageSize.Split('×')))
                      .Select(p => (p.p, double.Parse(p.Item2[0].Trim('m')) * double.Parse(p.Item2[1].Trim('m'))))
                      .OrderByDescending(s => s.Item2).First().p;

                try
                {
                    var arr2 = arr[1].Split("_");
                    switch (mode)
                    {
                        case "test":
                            img.Mutate(ctx =>
                            {
                                ctx.DrawText($"{arr[1]}", font, SixLabors.ImageSharp.Color.FromRgb(0, 255, 0), new PointF(30, 30));
                            });
                            break;
                        case "levelmark":
                            img.Mutate(ctx =>
                            {
                                ctx.DrawText($"{arr[1]}", font, SixLabors.ImageSharp.Color.FromRgb(178, 169, 161), new PointF(30, 30));

                                int w = img.Width;
                                int h = img.Height;
                                var x = (int)(w * 0.9);
                                var w1 = (int)(w * 0.1);

                                if (colorMap.ContainsKey(first.DamageLevel))
                                {
                                    ctx.Fill(Brushes.Solid(colorMap[first.DamageLevel]), new RectangleF(x, 0, w1, h * 0.08f));
                                    ctx.DrawText($"{first.DamageLevel}", font1, SixLabors.ImageSharp.Color.FromRgb(0, 0, 0), new PointF(x + w1 * 0.25f, h * 0.1f * 0.1f));
                                }
                                else
                                {
                                    ctx.Fill(Brushes.Solid(SixLabors.ImageSharp.Color.FromRgb(0xcc, 0xcc, 0xcc)), new RectangleF(x, 0, w1, h * 0.08f));
                                    ctx.DrawText($"{first.DamageLevel}", font1, SixLabors.ImageSharp.Color.FromRgb(0, 0, 0), new PointF(x + w1 * 0.25f, h * 0.1f * 0.25f));
                                }
                            });
                            break;
                        default:
                            img.Mutate(ctx =>
                            {
                                ctx.DrawText($"{arr[1]}", font, SixLabors.ImageSharp.Color.FromRgb(178, 169, 161), new PointF(30, 30));
                            });
                            break;
                    }
                    //if (mode == "test")
                    //{
                    //    img.Mutate(ctx =>
                    //    {
                    //        ctx.DrawText($"{savingTags[1]}", font, SixLabors.ImageSharp.Color.FromRgb(0, 255, 0), new PointF(30, 30));
                    //    });
                    //    //new Emgu.CV.Structure.MCvScalar(161, 169, 178)
                    //    //Cv2.PutText(img, $"{savingTags[1]}", new OpenCvSharp.Point(30, 80), HersheyFonts.HersheyComplexSmall, 3, new Scalar(0, 0, 255), 1);
                    //}
                    //else
                    //{
                    //    img.Mutate(ctx =>
                    //    {
                    //        ctx.DrawText($"{savingTags[1]}", font, SixLabors.ImageSharp.Color.FromRgb(178, 169, 161), new PointF(30, 30));
                    //    });

                    //    //new Scalar(161, 169, 178)
                    //    //Cv2.PutText(img, $"{savingTags[1]}", new OpenCvSharp.Point(30, 40), HersheyFonts.HersheyComplexSmall, 1, new Scalar(161, 169, 178), 1);
                    //}

                }
                catch (Exception ex) { Console.WriteLine(ex.ToString()); }

                img.Save(newfilename);


                yield return (newfilename,
                        shapeTypes.Key.Contains("外观正常") ? shapeTypes.Key.Substring(5) : shapeTypes.Key,
                           first.DamageDescription,
                           first.DamagePosition,
                           first.DamageSize,
                        task.Position);

            }
        }
    }

    private static void drawErrorShape(Image img, params BklInspectionTaskResult[] ordered)
    {
        img.Mutate(ctx =>
        {
            foreach (var shape in ordered)
            {
                if (shape.DamageX.Contains(","))
                {
                    try
                    {
                        var xs = shape.DamageX.Split(',').Select(s => int.Parse(s)).ToArray();
                        var ys = shape.DamageY.Split(',').Select(s => int.Parse(s)).ToArray();
                        var pts = Enumerable.Range(0, xs.Length).Select(ind => new PointF(xs[ind], ys[ind])).ToArray();
                        var lis = new List<PointF>();
                        lis.AddRange(pts);
                        lis.Add(pts[0]);
                        ctx = ctx.DrawPolygon(SixLabors.ImageSharp.Color.FromRgb(0, 255, 0), 10, pts.ToArray());
                        //Cv2.Polylines(img, lis, true, new Scalar(0, 255, 0), 10);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
                else
                {

                    try
                    {

                        PointF[] pts = new PointF[]
                        {
                            new PointF(int.Parse(shape.DamageX), int.Parse(shape.DamageY)),
                            new PointF(int.Parse(shape.DamageX)+int.Parse(shape.DamageWidth), int.Parse(shape.DamageY)),
                            new PointF(int.Parse(shape.DamageX)+int.Parse(shape.DamageWidth), int.Parse(shape.DamageY)+ int.Parse(shape.DamageHeight)),
                            new PointF(int.Parse(shape.DamageX), int.Parse(shape.DamageY)+ int.Parse(shape.DamageHeight)),
                            new PointF(int.Parse(shape.DamageX), int.Parse(shape.DamageY)),
                        };
                        ctx = ctx.DrawPolygon(SixLabors.ImageSharp.Color.FromRgb(0, 255, 0), 10, pts);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
        });
    }

    public void Done()
    {
        foreach (var f in _tempFiles)
        {
            try { File.Delete(f); }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }
    }
}
