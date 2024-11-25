using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Collections.Generic;
using Bkl.Models;
using Bkl.Infrastructure;
using System.Linq;
using Minio;
using System.Reactive.Linq;
using System;
using System.IO;
using DocumentFormat.OpenXml.Packaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using Bkl.Inspection;

public class ReportTemplate
{
    public static ReportTemplate Static = new ReportTemplate
    {
        Company = "大唐清洁能源",
        Central = "大唐清洁能源",
        Title = "线路巡检报告",
        ErrorImageWH = 200,
        ConfThreshold = 0.8,
        ErrorImageCellWH = 7.5,
        MainImageQuality = 60,
        AirDrone = "DJI M3T ",
        SrcScale = 4
    };
    public string Company { get; set; }
    public string Central { get; set; }
    public string Title { get; set; }
    public double ConfThreshold { get; set; }
    public double ErrorImageWH { get; set; }
    public double ErrorImageCellWH { get; set; }
    public int MainImageQuality { get; set; }
    public string AirDrone { get; set; }
    public int SrcScale { get; set; }
}

public class CreatePowerlineExportParagraph : ICreateParagraph
{
    private BklConfig _config;
    private IRedisClient _redis;
    private long _reportIndex;
    private List<string> _tempFiles;
    private BklInspectionTask _task;
    private List<BklInspectionTaskResult> _results;
    private List<BklInspectionTaskDetail> _taskDetails;
    private WordprocessingDocument _doc;

    public CreatePowerlineExportParagraph(
        BklConfig config,
        WordprocessingDocument doc,
        IRedisClient redis,
        BklInspectionTask task,
        long index,
        List<BklInspectionTaskDetail> details,
        List<BklInspectionTaskResult> results)
    {
        _task = task;
        _results = results;
        _taskDetails = details;
        _doc = doc;
        _tempFiles = new List<string>();
        _config = config;
        _redis = redis;
        _reportIndex = index;
    }
    const int wordWidth = 11906;

    const int emu_per_dxa = 635;
    public static string PercentWidth(int pers)
    {
        return ((pers * 8505) / 100).ToString();
    }

    public static LevelConfig[] POWER_CONFIG = new LevelConfig[]
    {
         new ("jyz","绝缘子","normal"),
         new ("jyzps","绝缘子破损","error"),
         new ("fzc","防震锤","error"),
         new ("xj","线夹","normal"),
         new ("nc","鸟巢" ,"error"),
         new ("zcxs","重锤锈蚀","error"),
         new ("jjxs","金具锈蚀","error"),
         new ("gbxs","部件锈蚀","error"),
         new ("fzcxs","防震锤锈蚀","error"),
         new ("jyzzw","绝缘子赃污","error"),
         new ("xjwt","线夹问题","error"),
         new ("gtyw","杆塔异物","error"),
    };



    Justification left => new Justification { Val = JustificationValues.Left };
    Justification center => new Justification { Val = JustificationValues.Center };
    SpacingBetweenLines spacing_240_120_400 => new SpacingBetweenLines { Before = "240", After = "120", Line = "400", LineRule = LineSpacingRuleValues.Exact };

    SpacingBetweenLines spacing_480_240_400 => new SpacingBetweenLines { Before = "480", After = "240", Line = "400", LineRule = LineSpacingRuleValues.Exact };
    SpacingBetweenLines spacing_480_960_600 => new SpacingBetweenLines { Before = "480", After = "960", Line = "600", LineRule = LineSpacingRuleValues.Exact };
    SpacingBetweenLines spacing_240_120 => new SpacingBetweenLines { Before = "240", After = "120" };
    SpacingBetweenLines spacing_400 => new SpacingBetweenLines() { Line = "400", LineRule = LineSpacingRuleValues.Exact };
    SpacingBetweenLines spacing_360_auto => new SpacingBetweenLines() { Line = "360", LineRule = LineSpacingRuleValues.Auto };

    SpacingBetweenLines spacing_60_60 => new SpacingBetweenLines { Before = "60", After = "60" };
    Indentation indent_200 => new Indentation { FirstLine = "480", FirstLineChars = 200 };
    Indentation indent_640_200 => new Indentation { FirstLine = "640", FirstLineChars = 200 };

    Table newTable()
    {
        var table1 = new Table();
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
        table1.AppendChild<TableProperties>(tblProp);
        return table1;
    }
    //到场光伏组件EL检测不合格缺陷统计表
    void addColumn(Table table, string[] widths)
    {
        TableGrid tableGrid1 = new TableGrid();
        foreach (var col in widths)
        {
            tableGrid1.Append(new GridColumn() { Width = col });
        }
        table.Append(tableGrid1);
    }
    void addCells(TableRow tr1, string[] texts, int[] colspan = null)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            tr1.Append(new TableCell(
                new TableCellProperties(
                    new GridSpan { Val = colspan == null ? 1 : colspan[i] },
                    new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                    createText(text))));
        }
    }
    void addCells(TableRow tr1, OpenXmlElement[] texts, int[] colspan = null)
    {
        for (int i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            tr1.Append(new TableCell(
                new TableCellProperties(
                    new GridSpan { Val = colspan == null ? 1 : colspan[i] },
                    new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                    text)));
        }
    }

    TableBorders TableNonBorders()
    {
        TableBorders tableBorders1 = new TableBorders();
        TopBorder topBorder1 = new TopBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U };
        LeftBorder leftBorder1 = new LeftBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U };
        BottomBorder bottomBorder1 = new BottomBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U };
        RightBorder rightBorder1 = new RightBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U };
        InsideHorizontalBorder insideHorizontalBorder1 = new InsideHorizontalBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U };
        InsideVerticalBorder insideVerticalBorder1 = new InsideVerticalBorder() { Val = BorderValues.None, Color = "auto", Size = (UInt32Value)0U, Space = (UInt32Value)0U };

        tableBorders1.Append(topBorder1);
        tableBorders1.Append(leftBorder1);
        tableBorders1.Append(bottomBorder1);
        tableBorders1.Append(rightBorder1);
        tableBorders1.Append(insideHorizontalBorder1);
        tableBorders1.Append(insideVerticalBorder1);
        return tableBorders1;
    }

    TableBorders TableTextBorders()
    {
        TableBorders tableBorders1 = new TableBorders();
        TopBorder topBorder1 = new TopBorder()
        {
            Val = BorderValues.Single,
            Color = "black",
            Size = (UInt32Value)12U,
            Space = (UInt32Value)0U
        };
        LeftBorder leftBorder1 = new LeftBorder()
        {
            Val = BorderValues.None,
            Color = "auto",
            Size = (UInt32Value)0U,
            Space = (UInt32Value)0U
        };
        RightBorder rightBorder1 = new RightBorder()
        {
            Val = BorderValues.None,
            Color = "auto",
            Size = (UInt32Value)0U,
            Space = (UInt32Value)0U
        };
        BottomBorder bottomBorder1 = new BottomBorder()
        {
            Val = BorderValues.Single,
            Color = "black",
            Size = (UInt32Value)12U,
            Space = (UInt32Value)0U
        };

        InsideHorizontalBorder insideHorizontalBorder1 = new InsideHorizontalBorder()
        {
            Val = BorderValues.Single,
            Color = "auto",
            Size = (UInt32Value)4U,
            Space = (UInt32Value)0U
        };
        InsideVerticalBorder insideVerticalBorder1 = new InsideVerticalBorder()
        {
            Val = BorderValues.Single,
            Color = "auto",
            Size = (UInt32Value)4U,
            Space = (UInt32Value)0U
        };

        tableBorders1.Append(topBorder1);
        tableBorders1.Append(leftBorder1);
        tableBorders1.Append(bottomBorder1);
        tableBorders1.Append(rightBorder1);
        tableBorders1.Append(insideHorizontalBorder1);
        tableBorders1.Append(insideVerticalBorder1);
        return tableBorders1;
    }

    Table GetTable(string[][] texts, string[] columnWidths,
        Func<int, int, string, Paragraph> getParagraph,
        Func<int, int, int> getCellSpan,
        Func<int, int, string> getColWidth,
         Func<TableBorders> getBorders,
         Action<TableCellProperties> onTableCellProperties = null,
         Action<TableProperties> onTableProperties = null, string tableWidth = "9411", uint rowHeight = 907)
    {
        Table table1 = new Table();

        TableProperties tableProperties1 = new TableProperties();

        TableWidth tableWidth1 = new TableWidth() { Width = tableWidth, Type = TableWidthUnitValues.Dxa };
        TableJustification tableJustification1 = new TableJustification() { Val = TableRowAlignmentValues.Center };

        TableBorders tableBorders1 = getBorders();

        tableProperties1.Append(tableWidth1);
        tableProperties1.Append(tableJustification1);
        tableProperties1.Append(tableBorders1);
        if (onTableProperties != null)
            onTableProperties(tableProperties1);

        TableGrid tableGrid1 = new TableGrid();
        foreach (var col in columnWidths)
        {
            GridColumn gridColumn1 = new GridColumn() { Width = col };
            tableGrid1.Append(gridColumn1);
        }

        table1.Append(tableProperties1);
        table1.Append(tableGrid1);

        foreach (var rowIndex in Enumerable.Range(0, texts.GetUpperBound(0) + 1))
        {
            TableRow tableRow1 = new TableRow();

            TableRowProperties tableRowProperties1 = new TableRowProperties();
            if (rowHeight > 0)
            {
                TableRowHeight tableRowHeight1 = new TableRowHeight() { Val = (UInt32Value)rowHeight, HeightType = HeightRuleValues.Exact };
                tableRowProperties1.Append(tableRowHeight1);
            }
            TableJustification tableJustification2 = new TableJustification() { Val = TableRowAlignmentValues.Center };
            tableRowProperties1.Append(tableJustification2);

            tableRow1.Append(tableRowProperties1);
            foreach (var colIndex in Enumerable.Range(0, texts[rowIndex].Length))
            {
                var text = texts[rowIndex][colIndex];
                TableCell tableCell1 = new TableCell();

                TableCellProperties tableCellProperties1 = new TableCellProperties();
                if (onTableCellProperties != null)
                    onTableCellProperties(tableCellProperties1);

                TableCellVerticalAlignment tableCellVertical = new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center };
                tableCellProperties1.Append(tableCellVertical);

                var str = getColWidth(rowIndex, colIndex);
                if (string.IsNullOrEmpty(str))
                    tableCellProperties1.Append(new TableCellWidth { Width = str });
                tableCellProperties1.Append(new GridSpan { Val = getCellSpan(rowIndex, colIndex) });

                var para = getParagraph(rowIndex, colIndex, text);

                tableCell1.Append(tableCellProperties1);
                tableCell1.Append(para);

                tableRow1.Append(tableCell1);
            }
            table1.Append(tableRow1);
        }

        return table1;
    }



    public Paragraph GetText(string text, string fontStyle = "黑体", string fontSize = "24", Justification justification = null, SpacingBetweenLines spacingBetweenLines = null, Indentation indentation = null)
    {
        Paragraph paragraph1 = new Paragraph() { RsidParagraphMarkRevision = "007A7D1A", RsidParagraphAddition = "007A7D1A", RsidParagraphProperties = "007A7D1A", RsidRunAdditionDefault = "007A7D1A" };
        ParagraphProperties paragraphProperties1 = new ParagraphProperties();
        AdjustRightIndent adjustRightIndent1 = new AdjustRightIndent() { Val = false };
        SnapToGrid snapToGrid1 = new SnapToGrid() { Val = false };
        paragraphProperties1.Append(adjustRightIndent1);
        paragraphProperties1.Append(snapToGrid1);
        if (justification != null)
            paragraphProperties1.Append(justification);
        if (spacingBetweenLines != null)
            paragraphProperties1.Append(spacingBetweenLines);
        if (indentation != null)
            paragraphProperties1.Append(indentation);

        Run run1 = new Run() { RsidRunProperties = "007A7D1A" };
        RunProperties runProperties1 = new RunProperties();
        RunFonts runFonts2 = new RunFonts()
        {
            Hint = FontTypeHintValues.EastAsia,
            Ascii = "Times New Roman",
            HighAnsi = "Times New Roman",
            EastAsia = fontStyle,
            ComplexScript = "Times New Roman"
        };
        FontSize fontSize1 = new FontSize() { Val = fontSize };
        runProperties1.Append(runFonts2);
        runProperties1.Append(fontSize1);
        Text text1 = new Text();
        text1.Text = text;
        run1.Append(runProperties1);
        run1.Append(text1);
        paragraph1.Append(paragraphProperties1);
        paragraph1.Append(run1);
        return paragraph1;

    }

    Paragraph GetTitle(string text, string fontStyle = "黑体", string fontSize = "30", OutlineLevel outlineLevel = null, Justification justification = null, SpacingBetweenLines spacingBetweenLines = null)
    {
        Paragraph paragraph1 = new Paragraph() { RsidParagraphMarkRevision = "007A7D1A", RsidParagraphAddition = "007A7D1A", RsidParagraphProperties = "007A7D1A", RsidRunAdditionDefault = "007A7D1A" };

        ParagraphProperties paragraphProperties1 = new ParagraphProperties();
        KeepNext keepNext1 = new KeepNext();
        KeepLines keepLines1 = new KeepLines();
        AutoSpaceDE autoSpaceDE1 = new AutoSpaceDE() { Val = false };
        AutoSpaceDN autoSpaceDN1 = new AutoSpaceDN() { Val = false };
        AdjustRightIndent adjustRightIndent1 = new AdjustRightIndent() { Val = false };
        SnapToGrid snapToGrid1 = new SnapToGrid() { Val = false };


        paragraphProperties1.Append(keepNext1);
        paragraphProperties1.Append(keepLines1);
        paragraphProperties1.Append(autoSpaceDE1);
        paragraphProperties1.Append(autoSpaceDN1);
        paragraphProperties1.Append(adjustRightIndent1);
        paragraphProperties1.Append(snapToGrid1);
        if (spacingBetweenLines != null)
            paragraphProperties1.Append(spacingBetweenLines);
        if (justification != null)
            paragraphProperties1.Append(justification);
        if (outlineLevel != null)
            paragraphProperties1.Append(outlineLevel);

        BookmarkStart bookmarkStart1 = new BookmarkStart() { Name = "_Toc7795", Id = "2" };
        BookmarkStart bookmarkStart2 = new BookmarkStart() { Name = "_Toc95825830", Id = "3" };
        BookmarkStart bookmarkStart3 = new BookmarkStart() { Name = "_Toc139725383", Id = "4" };

        Run run1 = new Run() { RsidRunProperties = "007A7D1A" };

        RunProperties runProperties1 = new RunProperties();
        RunFonts runFonts2 = new RunFonts()
        {
            Hint = FontTypeHintValues.EastAsia,
            Ascii = "Times New Roman",
            HighAnsi = "Times New Roman",
            EastAsia = fontStyle,
            ComplexScript = "Times New Roman"
        };
        BoldComplexScript boldComplexScript2 = new BoldComplexScript();
        Kern kern2 = new Kern() { Val = (UInt32Value)44U };
        FontSize fontSize2 = new FontSize() { Val = fontSize };

        runProperties1.Append(runFonts2);
        runProperties1.Append(boldComplexScript2);
        runProperties1.Append(kern2);
        runProperties1.Append(fontSize2);
        LastRenderedPageBreak lastRenderedPageBreak1 = new LastRenderedPageBreak();
        Text text1 = new Text();
        text1.Text = text;

        run1.Append(runProperties1);
        run1.Append(lastRenderedPageBreak1);
        run1.Append(text1);
        BookmarkEnd bookmarkEnd1 = new BookmarkEnd() { Id = "2" };
        BookmarkEnd bookmarkEnd2 = new BookmarkEnd() { Id = "3" };
        BookmarkEnd bookmarkEnd3 = new BookmarkEnd() { Id = "4" };

        paragraph1.Append(paragraphProperties1);
        paragraph1.Append(bookmarkStart1);
        paragraph1.Append(bookmarkStart2);
        paragraph1.Append(bookmarkStart3);
        paragraph1.Append(run1);
        paragraph1.Append(bookmarkEnd1);
        paragraph1.Append(bookmarkEnd2);
        paragraph1.Append(bookmarkEnd3);
        return paragraph1;

    }

    Paragraph GetParagraph(string text, int spacing = 20, string fontSize = "48", string fontStyle = "黑体", SpacingBetweenLines spacingBetweenLines = null, JustificationValues justification = JustificationValues.Center)
    {
        Paragraph paragraph1 = new Paragraph() { };

        ParagraphProperties paragraphProperties1 = new ParagraphProperties();
        AutoSpaceDE autoSpaceDE1 = new AutoSpaceDE() { Val = false };
        AutoSpaceDN autoSpaceDN1 = new AutoSpaceDN() { Val = false };
        AdjustRightIndent adjustRightIndent1 = new AdjustRightIndent() { Val = false };
        Justification justification1 = new Justification() { Val = justification };

        //ParagraphMarkRunProperties paragraphMarkRunProperties1 = new ParagraphMarkRunProperties();
        //RunFonts runFonts1 = new RunFonts() {
        //    Hint = FontTypeHintValues.EastAsia,
        //    Ascii = "Times New Roman",
        //    HighAnsi = "Times New Roman",
        //    EastAsia = fontStyle };
        //Spacing spacing1 = new Spacing() { Val = 20 };
        //FontSize fontSize1 = new FontSize() { Val = fontSize };
        //FontSizeComplexScript fontSizeComplexScript1 = new FontSizeComplexScript() { Val = fontSize };

        //paragraphMarkRunProperties1.Append(runFonts1);
        //paragraphMarkRunProperties1.Append(spacing1);
        //paragraphMarkRunProperties1.Append(fontSize1);
        //paragraphMarkRunProperties1.Append(fontSizeComplexScript1);


        paragraphProperties1.Append(autoSpaceDE1);
        paragraphProperties1.Append(autoSpaceDN1);
        paragraphProperties1.Append(adjustRightIndent1);
        if (spacingBetweenLines != null)
            paragraphProperties1.Append(spacingBetweenLines);
        paragraphProperties1.Append(justification1);
        //paragraphProperties1.Append(paragraphMarkRunProperties1); 

        Run run1 = new Run() { };

        RunProperties runProperties1 = new RunProperties();
        RunFonts runFonts2 = new RunFonts()
        {
            Hint = FontTypeHintValues.EastAsia,
            Ascii = "Times New Roman",
            HighAnsi = "Times New Roman",
            EastAsia = fontStyle
        };
        Spacing spacing2 = new Spacing() { Val = spacing };
        FontSize fontSize2 = new FontSize() { Val = fontSize };
        FontSizeComplexScript fontSizeComplexScript2 = new FontSizeComplexScript() { Val = fontSize };

        runProperties1.Append(runFonts2);
        runProperties1.Append(spacing2);
        runProperties1.Append(fontSize2);
        runProperties1.Append(fontSizeComplexScript2);
        Text text1 = new Text();
        text1.Text = text;

        run1.Append(runProperties1);
        run1.Append(text1);
        paragraph1.Append(paragraphProperties1);
        paragraph1.Append(run1);
        return paragraph1;

    }

    Paragraph createText(string text, Justification just = null, SpacingBetweenLines line = null)
    {
        return new Paragraph(
            new ParagraphProperties(
                new AdjustRightIndent { Val = false },
                new SnapToGrid() { Val = false },
                line != null ? line : new SpacingBetweenLines()
                {
                    Before = "60",
                    After = "60",
                    Line = "240",
                    LineRule = LineSpacingRuleValues.Auto
                },
                just == null ? new Justification { Val = JustificationValues.Center } : just,
                new Run(new RunProperties(
                    new RunFonts()
                    {
                        Hint = FontTypeHintValues.EastAsia,
                        Ascii = "Times New Roman",
                        HighAnsi = "Times New Roman",
                        EastAsia = "宋体"
                    },
                    new FontSizeComplexScript() { Val = "24" }),
                    new Text(text))));
    }
    TableRow newTableRow(float height = 0)
    {
        return new TableRow(new TableRowProperties(
                          new CantSplit(),
                       height == 0 ?
                       new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast } :
                       new TableRowHeight { Val = (uint)(567 * height), HeightType = HeightRuleValues.Exact },
                          new TableJustification { Val = TableRowAlignmentValues.Center }));
    }
    Dictionary<int, string> dic = new Dictionary<int, string>
    {
        {0,"〇" },
        {1,"一" },
        {2,"二" },
        {3,"三" },
        {4,"四" },
        {5,"五" },
        {6,"六" },
        {7,"七" },
        {8,"八" },
        {9,"九" },
    };
    private ReportTemplate templateConfig;

    string GetDate(DateTime date)
    {
        return $"{dic[date.Year / 1000]}{dic[(date.Year % 1000) / 100]}{dic[(date.Year % 100) / 10]}{dic[(date.Year % 10)]}年{dic[date.Month]}月";
    }

    public OpenXmlElement AddParagraph()
    {
        var minio = new MinioClient().WithEndpoint(_config.MinioConfig.EndPoint)
            .WithCredentials(_config.MinioConfig.Key, _config.MinioConfig.Secret)
            .WithRegion(_config.MinioConfig.Region)
            .Build();

        minio.CreateBucket("sysconfig").GetAwaiter().GetResult();
        var powerconfig = minio.ReadObject<LevelConfig[]>("power-class.txt", "sysconfig").GetAwaiter().GetResult();
        if (powerconfig == null)
        {
            minio.WriteObject(POWER_CONFIG, "power-class.txt", "sysconfig").GetAwaiter().GetResult();
            powerconfig = POWER_CONFIG;
        }

        templateConfig = minio.ReadObject<ReportTemplate>("power-report-template.txt", "sysconfig").GetAwaiter().GetResult();
        if (templateConfig == null)
        {
            minio.WriteObject(ReportTemplate.Static, "power-report-template.txt", "sysconfig").GetAwaiter().GetResult();
            templateConfig = ReportTemplate.Static;
        }

        if (templateConfig.ConfThreshold > 0)
        {
            _results = _results.Where(s => int.TryParse(s.DamageLevel, out var conf) ? conf > templateConfig.ConfThreshold : true).ToList();
        }

        var ads = _results.Select(t => t.DamageType).Distinct().ToList();
        var CN_MapLevel = powerconfig.ToDictionary(s => s.@class, s => s.level);
        var CN_Map = powerconfig.ToDictionary(s => s.@class, s => s.name);
        ads.Where(s => CN_Map.ContainsKey(s) == false)
            .ToList()
            .ForEach(t =>
            {
                CN_Map.Add(t, t);
            });
        ads.Where(s => CN_MapLevel.ContainsKey(s) == false)
            .ToList()
            .ForEach(t =>
            {
                CN_MapLevel.Add(t, "error");
            });

        var normal = _results.Where(s => CN_MapLevel[s.DamageType] == "normal").ToList();
        _results = _results.Where(s => CN_MapLevel[s.DamageType] != "normal").ToList();
        int allsteps = _results.GroupBy(s => s.FacilityId).Count() * 2;
        //统计表格
        List<OpenXmlElement> tbs = new List<OpenXmlElement>();

        _redis.SetEntryInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.total", allsteps);
        _redis.SetEntryInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 0);
        _redis.SetEntryInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.time", long.Parse(DateTime.Now.ToString("yyyyMMddHHmm")));


        tbs.Add(GetTitle("1  线路巡检报告", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400));

        //_redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);


        tbs.Add(GetTitle("1.1  杆塔线路缺陷详情", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400));

        //_redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);


        int index2 = 1;

        foreach (var sameFaci in _results.GroupBy(s => s.FacilityId).OrderBy(s => s.Key))
        {

            //var pictable = newTable();
            //addColumn(pictable, new string[] { PercentWidth(18), PercentWidth(46), PercentWidth(18), PercentWidth(18) });
            var id = sameFaci.First().TaskDetailId;
            var d1 = _taskDetails.First(s => s.Id == id);

            List<(BklInspectionTaskDetail detail, List<BklInspectionTaskResult> errors)> detailAndErrors = sameFaci.Join(_taskDetails,
                        (left) => left.TaskDetailId,
                        (right) => right.Id,
                        (p, q) => new { error = p, detail = q })
                   .GroupBy(s => s.detail.Id)
                   .Select(s => (s.First().detail, s.Select(q => q.error).ToList()))
                   .ToList();
            object lock1 = new object();
            List<OpenXmlElement> tbElements = new List<OpenXmlElement>();


            var dt = DateTime.Now;
            Dictionary<long, MemoryStream> streams = new Dictionary<long, MemoryStream>();
            //先生成照片
            detailAndErrors
               .AsParallel()
               .ForAll(sd =>
               {
                   try
                   {
                       loadImagesStream(sd, minio, lock1, streams);

                   }
                   catch (Exception ex)
                   {
                       Console.WriteLine("loadStream " + _config.FileBasePath);
                       Console.WriteLine("loadStream " + sd.detail.RemoteImagePath);
                       Console.WriteLine("loadStream " + ex.ToString());
                   }
               });
            Console.WriteLine("first " + DateTime.Now.Subtract(dt).TotalSeconds);

            dt = DateTime.Now;

            ImagePart imagePart = _doc.MainDocumentPart.AddImagePart(ImagePartType.Jpeg);
            Dictionary<long, ImagePart> imageParts = new Dictionary<long, ImagePart>();
            detailAndErrors.ForEach(s =>
            {
                imageParts.Add(s.detail.Id, _doc.MainDocumentPart.AddImagePart(ImagePartType.Jpeg));
                s.errors.ToList().ForEach(errr =>
                {
                    imageParts.Add(errr.Id, _doc.MainDocumentPart.AddImagePart(ImagePartType.Jpeg));
                });
            });

            streams.AsParallel().ForAll(img =>
            {
                var part = imageParts[img.Key];
                part.FeedData(img.Value);
            });
            Console.WriteLine("second " + DateTime.Now.Subtract(dt).TotalSeconds);

            dt = DateTime.Now;
            detailAndErrors
                .AsParallel()
                .ForAll(sd =>
                {
                    var ImageWidth = templateConfig.ErrorImageCellWH;

                    var pictable = newTable();
                    addColumn(pictable, new string[] { PercentWidth(18), PercentWidth(50), PercentWidth(18), PercentWidth(14) });
                    var tbr1 = newTableRow();
                    addCells(tbr1, new string[] { "照片编号：", sd.detail.RemoteImagePath, "缺陷类型：", string.Join(",", sd.errors.Select(s => CN_Map[s.DamageType]).Distinct()) });

                    string lat = TryCatchExtention.TryCatch(() => (string)_redis.GetValueFromHash($"PowerLine:{sd.detail.RemoteImagePath}", "GpsLatitude"), "");
                    string lon = TryCatchExtention.TryCatch(() => (string)_redis.GetValueFromHash($"PowerLine:{sd.detail.RemoteImagePath}", "GpsLongitude"), "");
                    string alt = TryCatchExtention.TryCatch(() => (string)_redis.GetValueFromHash($"PowerLine:{sd.detail.RemoteImagePath}", "RelativeAltitude"), "");
                    var tbr2 = newTableRow();
                    addCells(tbr2, new string[] { "拍摄定位：", $"经：{lat}，纬：{lon}，高：{alt}，点位：{sd.detail.Position}", }, new int[] { 1, 3 });


                    var tbr3 = newTableRow(12);

                    var imageelemment = new CreateImageParagraph(
                        imageParts[sd.detail.Id],
                        _doc.MainDocumentPart.GetIdOfPart(imageParts[sd.detail.Id]),
                        11 * 360000,
                        11 * 360000).AddParagraph();

                    addCells(tbr3, new OpenXmlElement[] { createText("原图："), imageelemment }, new int[] { 1, 3 });
                    pictable.Append(tbr1);
                    pictable.Append(tbr2);
                    pictable.Append(tbr3);
                    //var r1 = H2 / sd.errors.Count;
                    //if (r1 < (H2 / 2))
                    //{
                    //    r1 = H2 / 2;
                    //}
                    foreach (var err in sd.errors)
                    {
                        var imgelement = new CreateImageParagraph(imageParts[err.Id],
                            _doc.MainDocumentPart.GetIdOfPart(imageParts[err.Id]),
                            (int)ImageWidth * 360000,
                            (int)ImageWidth * 360000
                            //(int)r1 * 360000
                            )
                        .AddParagraph();
                        var tbrr = newTableRow(0);
                        addCells(tbrr, new OpenXmlElement[] { createText($"{CN_Map[err.DamageType]}："), imgelement }, new int[] { 1, 3 });
                        pictable.Append(tbrr);
                    }

                    lock (lock1)
                    {
                        tbElements.Add(pictable);
                    }

                });
            Console.WriteLine("third " + DateTime.Now.Subtract(dt).TotalSeconds);

            tbs.Add(GetTitle($"1.1.{index2++} {d1.FacilityName}缺陷详情", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400));
            tbElements.ForEach(s =>
            {
                tbs.Add(s);
            });
            _redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);
        }


        tbs.Add(GetTitle("1.2  杆塔缺陷照片统计", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400));
        //_redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);

        {

            tbs.Add(GetTitle($"1.2.1  各类缺陷照片占比", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400));
            var statable = newTable();
            addColumn(statable, new string[] { PercentWidth(60), PercentWidth(20), PercentWidth(20) });


            var row2 = newTableRow();
            addCells(row2, new string[] { "缺陷名称", "照片数量", "缺陷数量" }, new int[] { 1, 1, 1 });
            statable.Append(row2);

            var call = _results.Select(s => s.TaskDetailId).Distinct().Count();
            var call2 = _results.Count();



            foreach (var sameFaci in _results.GroupBy(s => s.DamageType).OrderBy(s => s.Key))
            {
                var c1 = sameFaci.Select(s => s.TaskDetailId).Distinct().Count();
                var c2 = sameFaci.Count();
                var row3 = newTableRow();
                addCells(row3, new string[] { CN_Map[sameFaci.Key], $"共{c1}张照片 {(c1 * 100.0 / call).ToString("0.00")}%", $" {c2}处缺陷 {(c2 * 100.0 / call2).ToString("0.00")}%" }, new int[] { 1, 1, 1 });
                statable.Append(row3);
            }
            tbs.Add(statable);
            //_redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);
        }
        {
            tbs.Add(GetTitle($"1.2.2  各个杆塔缺陷占比", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400));

            var statable = newTable();
            addColumn(statable, new string[] { PercentWidth(60), PercentWidth(40) });


            var row2 = newTableRow();
            addCells(row2, new string[] { "杆塔名称", "缺陷数量" }, new int[] { 1, 2 });
            statable.Append(row2);

            var call2 = _results.Count();


            foreach (var sameFaci in _results.GroupBy(s => s.FacilityId).OrderBy(s => s.Key))
            {
                var c2 = sameFaci.Count();
                var row3 = newTableRow();
                var d = _taskDetails.Where(s => s.FacilityId == sameFaci.Key).First();
                addCells(row3, new string[] { d.FacilityName, $" {c2}处缺陷 {(c2 * 100.0 / call2).ToString("0.00")}%" }, new int[] { 1, 1 });
                statable.Append(row3);
            }
            tbs.Add(statable);
            //_redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);
        }
        {
            tbs.Add(GetTitle($"1.2.3  缺陷分布占比", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400));

            var statable = newTable();
            addColumn(statable, new string[] { PercentWidth(50), PercentWidth(25), PercentWidth(25) });


            var row2 = newTableRow();
            addCells(row2, new string[] { "缺陷名称", "缺陷照片数量", "缺陷占比" }, new int[] { 2, 1, 1 });
            statable.Append(row2);

            var call2 = _results.Count();

            foreach (var sameFaci in _results.GroupBy(s => s.DamageType).OrderBy(s => s.Key))
            {
                var c2 = sameFaci.Count();
                var row3 = newTableRow();
                addCells(row3, new string[] { CN_Map[sameFaci.Key], $"共计{c2}处", $"{(c2 * 100.0 / call2).ToString("0.00")}%" }, new int[] { 2, 1, 1 });
                statable.Append(row3);
            }
            tbs.Add(statable);
            //_redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);
        }

        tbs.Add(GetTitle("1.3  杆塔缺陷列表", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400));
        int index = 1;
        //_redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);
        foreach (var sameFaci in _results.GroupBy(s => s.FacilityId).OrderBy(s => s.Key))
        {
            var id = sameFaci.First().TaskDetailId;
            var d1 = _taskDetails.First(s => s.Id == id);

            tbs.Add(GetTitle($"1.3.{index++}  {d1.FacilityName}缺陷列表", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400));

            var table_error_statistic = newTable();
            addColumn(table_error_statistic, new string[] { PercentWidth(20), PercentWidth(40), PercentWidth(40) });
            var tbHeader = new TableRow(new TableRowProperties(
                            new CantSplit(),
                            new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                            new TableJustification { Val = TableRowAlignmentValues.Center }));
            string[] headers = new string[] { "杆塔编号", "照片编号", "缺陷类型" };

            addCells(tbHeader, headers);
            table_error_statistic.Append(tbHeader);

            foreach (var results in sameFaci.GroupBy(s => s.TaskDetailId).OrderBy(s => s.Key))
            {
                var tbr1 = new TableRow(new TableRowProperties(
                      new CantSplit(),
                      new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                      new TableJustification { Val = TableRowAlignmentValues.Center }));
                var result = results.First();
                var detail = _taskDetails.First(s => s.Id == result.TaskDetailId);
                addCells(tbr1, new string[] {
                        detail.FacilityName,
                        detail.RemoteImagePath,
                        string.Join(",",results.GroupBy(s=>s.DamageType).Select(s=>$"{CN_Map[s.Key]}{s.Count()}处").Distinct())
                    });
                table_error_statistic.Append(tbr1);
            }

            tbs.Add(table_error_statistic);
            _redis.IncrementValueInHash($"ReportProgress:{_task.FactoryId}:{_task.Id}", $"{_reportIndex}.progress", 1);
        }


        var contentPr = new Paragraph(
        new ParagraphProperties(
            new Justification { Val = JustificationValues.Center },
            new AdjustRightIndent { Val = false },
            new SnapToGrid() { Val = false },
            new SpacingBetweenLines { Before = "60", After = "60" }),
        new Run(),
        //new Break() { Type = BreakValues.Page },
        //new CreateEmptyParagraph().AddParagraph(),

        GetTable(
            texts: new string[][] {
                new string[]{   templateConfig.Company },
                new string[]{   templateConfig.Central},
                                new string[]{ " " },
                new string[]{ " " },
                     new string[]{   templateConfig.Title },
                new string[]{ " ", " " },
                new string[]{ " ", " " },
                new string[]{ " ", " " },
                          new string[]{ " ", " " },
                new string[]{ " ", " " },
                              new string[]{ " ", " " },


                new string[]{ " ", " " },

                new string[]{ " ", " " },
                new string[]{   "项目地点：", _task.FactoryName },
                new string[]{   "项目名称：", "无人机线路巡检" },
                new string[]{ "巡检方式：", "无人机巡检" },
                new string[]{ "巡检飞行器：", templateConfig.AirDrone },
                new string[]{ "报告日期：", DateTime.Now.ToString("yyyy-MM-dd") },
            },
            getParagraph: (row, col, text) =>
            {
                if (row <= 4)
                    return GetParagraph(text, fontSize: "30", fontStyle: "黑体");

                if (col == 0)
                    return GetParagraph(text, fontSize: "30", fontStyle: "黑体", justification: JustificationValues.Right);
                else
                    return GetParagraph(text, fontSize: "30", fontStyle: "仿宋", justification: JustificationValues.Left);
            },
            getColWidth: (row, col) =>
            {
                if (row <= 4)
                    return "8773";
                if (row > 4 && col == 0)
                {
                    return "4386";
                }
                if (row > 4 && col == 1)
                {
                    return "4387";
                }
                return "";
            },
            getBorders: TableNonBorders,

            getCellSpan: (row, col) =>
            {
                if (row <= 4)
                    if (col == 0)
                        return 2;
                    else
                        return 0;
                else return 1;
            },
            tableWidth: "8773",
            columnWidths: new string[] { "4386", "4387" },
            rowHeight: 680U),
        new Break() { Type = BreakValues.Page });
        //GetTitle("8  光伏组件实验室最大功率检测", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),
        //GetTitle("8.1  检测说明", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),
        //GetTitle($"8.1.1  检测目的", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
        //GetText($"在标准测试条件（1000W/m2，25℃）下对光伏组件进行最大功率检测，确认检测结果是否满足光伏组件供货合同或技术协议中的相关规定。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
        //GetTitle($"8.1.2  检测方法与判定标准", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
        //GetText($"（1）检测方法：使用太阳能模拟器，对光伏组件在标准测试条件下的最大功率值进行检测。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
        //GetText($"（2）判定标准：参考《中国华电集团有限公司2022年单晶硅光伏组件框架采购招标文件》（技术部分）以及本项目光伏组件供货合同、技术协议中对光伏组件最大功率的约定值，结合本次检测结果进行综合分析。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),


        tbs.ForEach(s => contentPr.Append(s));
        return contentPr;
    }


    private void loadImagesStream((BklInspectionTaskDetail detail, List<BklInspectionTaskResult> errors) sd, MinioClient minio, object lock1, Dictionary<long, MemoryStream> streams)
    {
        var downpath = Path.Combine(_config.FileBasePath, sd.detail.RemoteImagePath);
        var msfilesrc = new MemoryStream();

        if (File.Exists(downpath) == false)
        {
            //使用本地文件
            if (_config.UseLocalFile)
            {
                using (FileStream fs = new FileStream(Path.Combine(_config.MinioDataPath, "power-line", sd.detail.RemoteImagePath), FileMode.Open))
                {
                    fs.CopyTo(msfilesrc);
                }

            }
            else
            {
                using var stream = minio.ReadStream(sd.detail.RemoteImagePath, "power-line").GetAwaiter().GetResult();

                if (_config.ReportImageInMemory)
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(msfilesrc);
                }
                else
                {
                    using (FileStream fs = new FileStream(downpath, FileMode.Create))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        stream.CopyTo(fs);
                    }
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(msfilesrc);
                }

            }

        }
        else
        {
            using var fs = File.OpenRead(_config.UseLocalFile ? Path.Combine(_config.MinioDataPath, "power-line", sd.detail.RemoteImagePath) : downpath);
            fs.CopyTo(msfilesrc);
        }

        msfilesrc.Seek(0, SeekOrigin.Begin);
        var mssrc = new MemoryStream();
        using (var img = Image.Load(msfilesrc))
        {
            img.Mutate(ctx =>
            {
                ctx.Resize(img.Width / templateConfig.SrcScale, img.Height / templateConfig.SrcScale);
            });
            img.SaveAsJpeg(mssrc, new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder { Quality = templateConfig.MainImageQuality });
            mssrc.Seek(0, SeekOrigin.Begin);
        }

        foreach (var err in sd.errors)
        {
            msfilesrc.Seek(0, SeekOrigin.Begin);
            using var img = Image.Load(msfilesrc);
            var x = (int)double.Parse(err.DamageX);
            var y = (int)double.Parse(err.DamageY);
            var w = (int)double.Parse(err.DamageWidth);
            var h = (int)double.Parse(err.DamageHeight);
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (w < 0) w = 0;
            if (h <= 0) h = 0;
            if ((x + w) > img.Width)
                w = img.Width - x;
            if ((y + h) > img.Height)
                h = img.Height - y;
            if (w < 0) w = 0;
            if (h <= 0) h = 0;
            var cx = x + w / 2;
            var cy = y + h / 2;
            int max = Math.Max(w, h);
            if (max < templateConfig.ErrorImageWH)
                max = (int)templateConfig.ErrorImageWH;

            x = cx - max / 2;
            y = cy - max / 2;
            int x2 = cx + max / 2;
            int y2 = cy + max / 2;
            if (x < 0) x = 0;
            if (y < 0) y = 0;
            if (x2 > img.Width)
                x2 = img.Width;
            if (y2 > img.Height)
                y2 = img.Height;
            h = y2 - y;
            w = x2 - x;

            try
            {
                img.Mutate(ctx =>
                {
                    ctx.Crop(new Rectangle((int)x, (int)y, (int)w, (int)h));
                });
            }
            catch (Exception ex)
            {

                Console.WriteLine(ex);
            }
            var ms = new MemoryStream();
            img.SaveAsJpeg(ms);
            ms.Seek(0, SeekOrigin.Begin);
            lock (lock1)
            {
                streams.Add(err.Id, ms);
            }
        }
        lock (lock1)
        {
            streams.Add(sd.detail.Id, mssrc);
        }
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
