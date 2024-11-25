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
using static Bkl.Models.BklConfig;
using SixLabors.ImageSharp.Drawing.Processing;


using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using Wp14 = DocumentFormat.OpenXml.Office2010.Word.Drawing;



public class CreateELExportParagraph : ICreateParagraph
{
    private BklConfig _config;
    private List<string> _tempFiles;
    private BklInspectionTask _task;
    private List<BklInspectionTaskResult> _results;
    private List<BklInspectionTaskDetail> _taskDetails;
    private IRedisClient _redis;
    private WordprocessingDocument _doc;

    public CreateELExportParagraph(
        BklConfig config,
        WordprocessingDocument doc,
        IRedisClient redis,
        BklInspectionTask task,
        List<BklInspectionTaskDetail> details,
        List<BklInspectionTaskResult> results)
    {
        _task = task;
        _results = results;
        _taskDetails = details;
        _redis = redis;
        _doc = doc;
        _tempFiles = new List<string>();
        _config = config;
    }
    const int wordWidth = 11906;

    const int emu_per_dxa = 635;
    public static string PercentWidth(int pers)
    {
        return ((pers * 8505) / 100).ToString();
    }
    static Dictionary<string, string> cnMap = new Dictionary<string, string>
    {
        {"hundang", "混档"},
        {"heipian", "黑片"},
        {"heiban", "黑斑"},
        {"beibanquexian", "背板缺陷"},
        {"suipian", "碎片"},
        {"yinlie", "隐裂"},
        {"heibian", "黑边"},
        {"bengbianquejiao", "崩边缺角"},
        {"duanshan", "断栅"},
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
    void addCells(TableRow tr1, string[] texts)
    {
        foreach (var text in texts)
        {
            tr1.Append(new TableCell(
               new TableCellProperties(
                   new GridSpan { Val = 1 },
                   new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                   createText(text))));
        }
    }

    async void addImageCell(TableRow tbr3, MinioClient minio, string[] paths,
        BklInspectionTaskDetail detail = null, BklInspectionTaskResult[] results = null, int w = 2000000, int h = 1000000)
    {
        var fname = Guid.NewGuid() + ".jpg";
        try
        {
            var getobjarg = new GetObjectArgs().WithBucket(paths[0]).WithObject("seg." + paths[1]).WithCallbackStream(stream =>
            {
                using (var f = new FileStream(Path.Combine(_config.FileBasePath, fname), FileMode.OpenOrCreate))
                {
                    stream.CopyTo(f);
                    f.Flush();
                }
            });

            var objresp = minio.GetObjectAsync(getobjarg).GetAwaiter().GetResult();

            using (var img = Image.Load(Path.Combine(_config.FileBasePath, fname)))
            {
                img.Mutate(ctx =>
                {
                    ctx = ctx.Resize(400, 200);
                    if (detail != null && results != null)
                    {
                        //float wratio = (float)(400 * 1.0 / float.Parse(detail.ImageWidth));
                        //float hratio = (float)(300 * 1.0 / float.Parse(detail.ImageHeight));
                        float wratio = 0.1f;
                        float hratio = 0.08888f;
                        foreach (var re in results)
                        {
                            PointF[] pfs = new PointF[]
                            {
                                new PointF( float.Parse(re.DamageX) * wratio, float.Parse(re.DamageY) * hratio),
                                new PointF((float.Parse(re.DamageX)+float.Parse(re.DamageWidth))  * wratio, float.Parse(re.DamageY) * hratio),
                                new PointF((float.Parse(re.DamageX)+float.Parse(re.DamageWidth))   * wratio, (float.Parse(re.DamageY)+float.Parse(re.DamageHeight))   * hratio),
                                new PointF(float.Parse(re.DamageX) * wratio, (float.Parse(re.DamageY)+float.Parse(re.DamageHeight)) * hratio),
                                new PointF( float.Parse(re.DamageX) * wratio, float.Parse(re.DamageY) * hratio)
                            };
                            ctx = ctx.DrawPolygon(SixLabors.ImageSharp.Color.Red, 3, pfs);
                        }
                    }
                });
                img.Save(Path.Combine(_config.FileBasePath, fname));
            }
            tbr3.Append(new TableCell(
                            new TableCellProperties(new GridSpan { Val = 1 }, new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }),
                            new CreateImageParagraph(Path.Combine(_config.FileBasePath, fname), _doc, w, h).AddParagraph()));
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("load image error " + paths[0] + "/" + paths[1] + " " + ex.ToString());

        }

        try
        {
            var getobjarg = new GetObjectArgs().WithBucket(paths[0]).WithObject(paths[1]).WithCallbackStream(stream =>
            {
                using (var f = new FileStream(Path.Combine(_config.FileBasePath, fname), FileMode.OpenOrCreate))
                {
                    stream.CopyTo(f);
                    f.Flush();
                }
            });

            var objresp = minio.GetObjectAsync(getobjarg).GetAwaiter().GetResult();

            using (var img = Image.Load(Path.Combine(_config.FileBasePath, fname)))
            {
                img.Mutate(ctx =>
                {
                    ctx = ctx.Resize(400, 200);
                    if (detail != null && results != null)
                    {
                        //float wratio = (float)(400 * 1.0 / float.Parse(detail.ImageWidth));
                        //float hratio = (float)(300 * 1.0 / float.Parse(detail.ImageHeight));
                        float wratio = 0.1f;
                        float hratio = 0.08888f;
                        foreach (var re in results)
                        {
                            PointF[] pfs = new PointF[]
                            {
                                new PointF( float.Parse(re.DamageX) * wratio, float.Parse(re.DamageY) * hratio),
                                new PointF((float.Parse(re.DamageX)+float.Parse(re.DamageWidth))  * wratio, float.Parse(re.DamageY) * hratio),
                                new PointF((float.Parse(re.DamageX)+float.Parse(re.DamageWidth))   * wratio, (float.Parse(re.DamageY)+float.Parse(re.DamageHeight))   * hratio),
                                new PointF(float.Parse(re.DamageX) * wratio, (float.Parse(re.DamageY)+float.Parse(re.DamageHeight)) * hratio),
                                new PointF( float.Parse(re.DamageX) * wratio, float.Parse(re.DamageY) * hratio)
                            };
                            ctx = ctx.DrawPolygon(SixLabors.ImageSharp.Color.Red, 3, pfs);
                        }
                    }
                });
                img.Save(Path.Combine(_config.FileBasePath, fname));
            }
            tbr3.Append(new TableCell(
                            new TableCellProperties(new GridSpan { Val = 1 }, new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }),
                            new CreateImageParagraph(Path.Combine(_config.FileBasePath, fname), _doc, w, h).AddParagraph()));
            return;
        }
        catch (Exception ex)
        {
            Console.WriteLine("LoadImageError " + paths[0] + "/" + paths[1] + " " + ex.ToString());
        }

        tbr3.Append(new TableCell(
                          new TableCellProperties(new GridSpan { Val = 1 },
                          new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }),
                          new CreateEmptyParagraph().AddParagraph()));
    }
    public Paragraph GetImage2(Stream stream)
    {
        ImagePart imagePart = _doc.MainDocumentPart.AddImagePart(ImagePartType.Jpeg);
        imagePart.FeedData(stream);
        string relationshipId = _doc.MainDocumentPart.GetIdOfPart(imagePart);


        Paragraph paragraph1 = new Paragraph() { RsidParagraphAddition = "003F7E11", RsidRunAdditionDefault = "00B9028E" };
        BookmarkStart bookmarkStart1 = new BookmarkStart() { Name = "_GoBack", Id = "0" };

        Run run1 = new Run() { RsidRunProperties = "00B9028E" };

        RunProperties runProperties1 = new RunProperties();
        RunFonts runFonts1 = new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体", ComplexScript = "Times New Roman" };
        NoProof noProof1 = new NoProof();
        FontSize fontSize1 = new FontSize() { Val = "24" };

        runProperties1.Append(runFonts1);
        runProperties1.Append(noProof1);
        runProperties1.Append(fontSize1);

        Drawing drawing1 = new Drawing();

        Wp.Anchor anchor1 = new Wp.Anchor() { DistanceFromTop = (UInt32Value)0U, DistanceFromBottom = (UInt32Value)0U, DistanceFromLeft = (UInt32Value)114300U, DistanceFromRight = (UInt32Value)114300U, SimplePos = false, RelativeHeight = (UInt32Value)251659264U, BehindDoc = true, Locked = false, LayoutInCell = true, AllowOverlap = true, EditId = "40F10837", AnchorId = "45B80DAA" };
        Wp.SimplePosition simplePosition1 = new Wp.SimplePosition() { X = 0L, Y = 0L };

        Wp.HorizontalPosition horizontalPosition1 = new Wp.HorizontalPosition() { RelativeFrom = Wp.HorizontalRelativePositionValues.LeftMargin };
        Wp.PositionOffset positionOffset1 = new Wp.PositionOffset();
        positionOffset1.Text = "0";

        horizontalPosition1.Append(positionOffset1);

        Wp.VerticalPosition verticalPosition1 = new Wp.VerticalPosition() { RelativeFrom = Wp.VerticalRelativePositionValues.TopMargin };
        Wp.PositionOffset positionOffset2 = new Wp.PositionOffset();
        positionOffset2.Text = "0";

        verticalPosition1.Append(positionOffset2);
        Wp.Extent extent1 = new Wp.Extent() { Cx = 7570800L, Cy = 10756800L };
        Wp.EffectExtent effectExtent1 = new Wp.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 6985L };
        Wp.WrapNone wrapNone1 = new Wp.WrapNone();
        Wp.DocProperties docProperties1 = new Wp.DocProperties() { Id = (UInt32Value)1U, Name = "图片 1" };

        Wp.NonVisualGraphicFrameDrawingProperties nonVisualGraphicFrameDrawingProperties1 = new Wp.NonVisualGraphicFrameDrawingProperties();

        A.GraphicFrameLocks graphicFrameLocks1 = new A.GraphicFrameLocks() { NoChangeAspect = true };
        graphicFrameLocks1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

        nonVisualGraphicFrameDrawingProperties1.Append(graphicFrameLocks1);

        A.Graphic graphic1 = new A.Graphic();
        graphic1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

        A.GraphicData graphicData1 = new A.GraphicData() { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" };

        Pic.Picture picture1 = new Pic.Picture();
        picture1.AddNamespaceDeclaration("pic", "http://schemas.openxmlformats.org/drawingml/2006/picture");

        Pic.NonVisualPictureProperties nonVisualPictureProperties1 = new Pic.NonVisualPictureProperties();
        Pic.NonVisualDrawingProperties nonVisualDrawingProperties1 = new Pic.NonVisualDrawingProperties() { Id = (UInt32Value)1520U, Name = "图片 1520" };

        Pic.NonVisualPictureDrawingProperties nonVisualPictureDrawingProperties1 = new Pic.NonVisualPictureDrawingProperties();
        A.PictureLocks pictureLocks1 = new A.PictureLocks() { NoChangeAspect = true, NoChangeArrowheads = true };

        nonVisualPictureDrawingProperties1.Append(pictureLocks1);

        nonVisualPictureProperties1.Append(nonVisualDrawingProperties1);
        nonVisualPictureProperties1.Append(nonVisualPictureDrawingProperties1);

        Pic.BlipFill blipFill1 = new Pic.BlipFill();
        A.Blip blip1 = new A.Blip() { Embed = relationshipId, CompressionState = A.BlipCompressionValues.None };
        A.SourceRectangle sourceRectangle1 = new A.SourceRectangle();

        A.Stretch stretch1 = new A.Stretch();
        A.FillRectangle fillRectangle1 = new A.FillRectangle();

        stretch1.Append(fillRectangle1);

        blipFill1.Append(blip1);
        blipFill1.Append(sourceRectangle1);
        blipFill1.Append(stretch1);

        Pic.ShapeProperties shapeProperties1 = new Pic.ShapeProperties();

        A.Transform2D transform2D1 = new A.Transform2D();
        A.Offset offset1 = new A.Offset() { X = 0L, Y = 0L };
        A.Extents extents1 = new A.Extents() { Cx = 7570800L, Cy = 10756800L };

        transform2D1.Append(offset1);
        transform2D1.Append(extents1);

        A.PresetGeometry presetGeometry1 = new A.PresetGeometry() { Preset = A.ShapeTypeValues.Rectangle };
        A.AdjustValueList adjustValueList1 = new A.AdjustValueList();

        presetGeometry1.Append(adjustValueList1);
        A.NoFill noFill1 = new A.NoFill();

        A.Outline outline1 = new A.Outline();
        A.NoFill noFill2 = new A.NoFill();

        outline1.Append(noFill2);

        shapeProperties1.Append(transform2D1);
        shapeProperties1.Append(presetGeometry1);
        shapeProperties1.Append(noFill1);
        shapeProperties1.Append(outline1);

        picture1.Append(nonVisualPictureProperties1);
        picture1.Append(blipFill1);
        picture1.Append(shapeProperties1);

        graphicData1.Append(picture1);

        graphic1.Append(graphicData1);

        Wp14.RelativeWidth relativeWidth1 = new Wp14.RelativeWidth() { ObjectId = Wp14.SizeRelativeHorizontallyValues.Margin };
        Wp14.PercentageWidth percentageWidth1 = new Wp14.PercentageWidth();
        percentageWidth1.Text = "0";

        relativeWidth1.Append(percentageWidth1);

        Wp14.RelativeHeight relativeHeight1 = new Wp14.RelativeHeight() { RelativeFrom = Wp14.SizeRelativeVerticallyValues.Margin };
        Wp14.PercentageHeight percentageHeight1 = new Wp14.PercentageHeight();
        percentageHeight1.Text = "0";

        relativeHeight1.Append(percentageHeight1);

        anchor1.Append(simplePosition1);
        anchor1.Append(horizontalPosition1);
        anchor1.Append(verticalPosition1);
        anchor1.Append(extent1);
        anchor1.Append(effectExtent1);
        anchor1.Append(wrapNone1);
        anchor1.Append(docProperties1);
        anchor1.Append(nonVisualGraphicFrameDrawingProperties1);
        anchor1.Append(graphic1);
        anchor1.Append(relativeWidth1);
        anchor1.Append(relativeHeight1);

        drawing1.Append(anchor1);

        run1.Append(runProperties1);
        run1.Append(drawing1);
        BookmarkEnd bookmarkEnd1 = new BookmarkEnd() { Id = "0" };
        paragraph1.Append(bookmarkStart1);
        paragraph1.Append(run1);
        paragraph1.Append(bookmarkEnd1);
        return paragraph1;

    }
    public Drawing GetImage(Stream stream)
    {
        ImagePart imagePart = _doc.MainDocumentPart.AddImagePart(ImagePartType.Jpeg);

        imagePart.FeedData(stream);
        string relationshipId = _doc.MainDocumentPart.GetIdOfPart(imagePart);


        Drawing drawing1 = new Drawing();
        Wp.Anchor anchor1 = new Wp.Anchor(
            new Wp.SimplePosition() { X = 0L, Y = 0L },
            new Wp14.RelativeWidth(new Wp14.PercentageWidth() { Text = "0" }) { ObjectId = Wp14.SizeRelativeHorizontallyValues.Margin },
            new Wp14.RelativeHeight(new Wp14.PercentageHeight() { Text = "0" }) { RelativeFrom = Wp14.SizeRelativeVerticallyValues.Margin },
            new Wp.HorizontalPosition(new Wp.PositionOffset() { Text = "-1161583" }) { RelativeFrom = Wp.HorizontalRelativePositionValues.Column },
            new Wp.VerticalPosition(new Wp.PositionOffset() { Text = "-946450" }) { RelativeFrom = Wp.VerticalRelativePositionValues.Paragraph },
            new Wp.Extent() { Cx = 7570800L, Cy = 10756800L },
            new Wp.EffectExtent() { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 6985L },
            new Wp.WrapNone(),
            new Wp.DocProperties() { Id = (UInt32Value)1U, Name = "图片 1" },
            new A.Graphic(
                new A.GraphicData(
                    new Pic.Picture(
                        new Pic.NonVisualPictureProperties(
                          new Pic.NonVisualPictureDrawingProperties(new A.PictureLocks() { NoChangeAspect = true, NoChangeArrowheads = true }),
                          new Pic.NonVisualDrawingProperties() { Id = (UInt32Value)1520U, Name = "图片 1520" }),
                        new Pic.BlipFill(
                            new A.Blip() { Embed = relationshipId, CompressionState = A.BlipCompressionValues.Email },
                            new A.SourceRectangle(),
                            new A.Stretch(),
                            new A.FillRectangle()),
                        new Pic.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset() { X = 0L, Y = 0L },
                                new A.Extents() { Cx = 7570800L, Cy = 10756800L }),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle },
                            new A.Outline(new A.NoFill()),
                            new A.NoFill(),
                            new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle })))
                { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = (UInt32Value)0U,
            DistanceFromBottom = (UInt32Value)0U,
            DistanceFromLeft = (UInt32Value)114300U,
            DistanceFromRight = (UInt32Value)114300U,
            SimplePos = false,
            RelativeHeight = (UInt32Value)251659264U,
            BehindDoc = true,
            Locked = false,
            LayoutInCell = true,
            AllowOverlap = true,
            EditId = "40F10837",
            AnchorId = "45B80DAA"
        };




        //Wp.NonVisualGraphicFrameDrawingProperties nonVisualGraphicFrameDrawingProperties1 = new Wp.NonVisualGraphicFrameDrawingProperties();

        //A.GraphicFrameLocks graphicFrameLocks1 = new A.GraphicFrameLocks() { NoChangeAspect = true };
        //graphicFrameLocks1.AddNamespaceDeclaration("a", "http://schemas.openxmlformats.org/drawingml/2006/main");

        //nonVisualGraphicFrameDrawingProperties1.Append(graphicFrameLocks1);

        drawing1.Append(anchor1);
        return drawing1;
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

    Table GetTable(string[][] texts, string[] columnWidths, Func<int, int, string, Paragraph> getParagraph, Func<TableBorders> getBorders, Action<TableCellProperties> onTableCellProperties = null, Action<TableProperties> onTableProperties = null, string tableWidth = "9411", uint rowHeight = 907)
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
                TableCellWidth tableCellWidth1 = new TableCellWidth() { Width = columnWidths[colIndex], Type = TableWidthUnitValues.Dxa };
                TableCellVerticalAlignment tableCellVertical = new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center };
                tableCellProperties1.Append(tableCellWidth1);
                tableCellProperties1.Append(tableCellVertical);

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
        {10,"十" },
        {11,"十一" },
        {12,"十二" },
    };
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

        //统计表格
        List<Table> tbs = new List<Table>();
        var table_error_statistic = newTable();
        addColumn(table_error_statistic, new string[] { "498", "2669", "2669", "2669" });
        var tbHeader = new TableRow(new TableRowProperties(
                        new CantSplit(),
                        new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                        new TableJustification { Val = TableRowAlignmentValues.Center }));
        string[] headers = new string[] { "序号", "缺陷类型", "缺陷数量（块）", "缺陷占比（%）" };

        addCells(tbHeader, headers);
        table_error_statistic.Append(tbHeader);

        int index = 0;
        int sum = _results.Select(s => s.FacilityId).Distinct().Count();
        foreach (var item in _results.GroupBy(s => s.DamageType))
        {
            var tbr1 = new TableRow(new TableRowProperties(
                      new CantSplit(),
                      new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                      new TableJustification { Val = TableRowAlignmentValues.Center }));
            var ct = item.Select(s => s.FacilityId).Distinct().Count();
            addCells(tbr1, new string[] {
                index.ToString(),
                cnMap[item.Key],
                ct.ToString(),
                (100 * ((ct * 1.0) / sum)).ToString("0.00")
            });
            table_error_statistic.Append(tbr1);
            index++;
        }

        //缺陷照片表格
        var table_error_image = newTable();
        addColumn(table_error_image, new string[] { "2000", "5000", "500", "500" });
        var tbr2 = new TableRow(new TableRowProperties(
                      new CantSplit(),
                      new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                      new TableJustification { Val = TableRowAlignmentValues.Center }));
        addCells(tbr2, new string[] { "组件序列号", "测试图片", "判定结果", "备注" });
        table_error_image.Append(tbr2);
        foreach (var item in _results.GroupBy(s => s.FacilityName))
        {
            var tbr3 = new TableRow(new TableRowProperties(
                   new CantSplit(),
                   new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                   new TableJustification { Val = TableRowAlignmentValues.Center }));
            tbr3.Append(new TableCell(
                 new TableCellProperties(
                     new GridSpan { Val = 1 },
                     new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                     createText(item.Key))));


            var detail = _taskDetails.FirstOrDefault(s => s.Id == item.First().TaskDetailId);
            var errors = _results.Where(s => s.TaskDetailId == detail.Id).ToArray();
            var paths = detail.RemoteImagePath.Split('/');

            addImageCell(tbr3, minio, paths, detail, errors);

            tbr3.Append(new TableCell(
               new TableCellProperties(
                   new GridSpan { Val = 1 },
                   new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                   createText("不合格"))));

            var errs = item.Select(s => cnMap[s.DamageType]).Distinct().ToArray();

            tbr3.Append(new TableCell(
               new TableCellProperties(
                   new GridSpan { Val = 1 },
                   new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
                   createText(string.Join(",", errs)))));
            table_error_image.Append(tbr3);
        }

        //非缺陷表格
        var errIds = _results.Select(s => s.TaskDetailId).ToArray();
        var okDetails = _taskDetails.Where(s => !errIds.Contains(s.Id)).ToArray();
        var table_normal_image = newTable();
        addColumn(table_normal_image, new string[] { "4255", "4250" });
        for (int i = 0; i < okDetails.Count();)
        {
            var tbr11 = new TableRow(new TableRowProperties(
                  new CantSplit(),
                  new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                  new TableJustification { Val = TableRowAlignmentValues.Center }));
            var tbr22 = new TableRow(new TableRowProperties(
                  new CantSplit(),
                  new TableRowHeight { Val = 0, HeightType = HeightRuleValues.AtLeast },
                  new TableJustification { Val = TableRowAlignmentValues.Center }));
            if ((i + 1) < okDetails.Count())
            {
                addCells(tbr11, new string[] { okDetails[i].FacilityName, okDetails[i + 1].FacilityName });
                addImageCell(tbr22, minio, okDetails[i].RemoteImagePath.Split('/'), w: 2400000, h: 1200000);
                addImageCell(tbr22, minio, okDetails[i + 1].RemoteImagePath.Split('/'), w: 2400000, h: 1200000);
                table_normal_image.Append(tbr11);
                table_normal_image.Append(tbr22);
            }
            else
            {
                addCells(tbr11, new string[] { okDetails[i].FacilityName, "" });
                addImageCell(tbr22, minio, okDetails[i].RemoteImagePath.Split('/'), w: 2400000, h: 1200000);
                addCells(tbr22, new string[] { "" });
                table_normal_image.Append(tbr11);
                table_normal_image.Append(tbr22);
            }
            i += 2;
        }
        var type = "到场";
        if (_task.TaskName.Contains("到场") && _task.TaskName.Contains("安装"))
        {
            type = "到场及安装";
        }
        else
        {
            type = (_task.TaskName.Contains("安装") ? "安装" : "到场");
        }
        var indexImg = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "index.jpg");
        MemoryStream ms = new MemoryStream();
        using (FileStream fs = new FileStream(indexImg, FileMode.Open))
        {
            fs.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
        }
        string terrain = _redis.GetValueFromHash("FactoryMeta", $"{_task.FactoryId}-Terrain");
        var draw = GetImage2(ms);
        var tableIndex = GetTable(
            texts: new string[][] {
                new string[]{_task.FactoryName},
                new string[]{ $"{terrain}发电项目"},
                new string[]{ $"组件{type}技术检测报告"},
            },
            getParagraph: (row, col, text) => GetParagraph(text, fontSize: "48", fontStyle: "黑体", spacingBetweenLines: new SpacingBetweenLines
            {
                Line = "400",
                LineRule = LineSpacingRuleValues.AtLeast
            }),
            getBorders: TableNonBorders,
            tableWidth: "9411",
            columnWidths: new string[] { "9411" },
            rowHeight: 907U);
        var tableIndex2 = GetTable(
            texts: new string[][] {
                new string[]{"华电电力科学研究院有限公司" },
                new string[]{GetDate(DateTime.Now) },
            },
            getParagraph: (row, col, text) =>
            GetParagraph(text, fontSize: "44", spacingBetweenLines: new SpacingBetweenLines
            {
                Line = "400",
                LineRule = LineSpacingRuleValues.AtLeast
            }, fontStyle: "华文中宋"),
            getBorders: TableNonBorders,
            tableWidth: "6771",
            columnWidths: new string[] { "6771" },
            rowHeight: 907U);

        //var prp1 = new ParagraphProperties(
        //    new Justification { Val = JustificationValues.Center },
        //    new AdjustRightIndent { Val = false },
        //    new SnapToGrid() { Val = false },
        //    new SpacingBetweenLines { Before = "60", After = "60" });
        //var pr = new Paragraph();
        //pr.Append(prp1);
        ////pr.Append(new Paragraph());
        //////pr.AppendChild(draw); 
        ////pr.Append(new Paragraph(new Run(tableIndex)));
        ////pr.Append(new Paragraph());
        ////pr.Append(new Paragraph(new Run(tableIndex2)));
        //pr.Append(new Run());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph(new Run(tableIndex)));
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph());
        //pr.Append(new Paragraph(new Run(tableIndex2)));
        //pr.Append(draw);
        //pr.Append(new Break() { Type = BreakValues.Page });
        //pr.Append();

        //pr.AppendChild(new Run()); 

        var contentPr = new Paragraph(
                new ParagraphProperties(
                new Justification { Val = JustificationValues.Center },
                new AdjustRightIndent { Val = false },
                new SnapToGrid() { Val = false },
                new SpacingBetweenLines { Before = "60", After = "60" }),
                new Run(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(new Run(tableIndex)),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(),
                new Paragraph(new Run(tableIndex2)),
                draw,
                new Break() { Type = BreakValues.Page },
                new CreateEmptyParagraph().AddParagraph(),
                GetTable(
                    texts: new string[][] {
                        new string[]{"项目负责单位：","华电电力科学研究院有限公司" },
                        new string[]{"",            "杭州华电工程咨询有限公司" },
                        new string[]{"项目合作单位：","" },
                        new string[]{ "起讫日期：", "2023-01-01" },
                        new string[]{ "工作负责人：", " " },
                        new string[]{ "主要参加人：", " " },
                        new string[]{ " ", " " },
                        new string[]{ " ", " " },
                        new string[]{ " ", " " },
                        new string[]{ " ", " " },
                        new string[]{ "编写：", " " },
                        new string[]{ " ", " " },
                        new string[]{ " ", " " },
                        new string[]{ "审核：", " " },
                        new string[]{ " ", " " },
                        new string[]{ " ", " " },
                        new string[]{ "批准：", " " },
                        new string[]{ " ", " " },
                        new string[]{ " ", " " },
                    },
                    getParagraph: (row, col, text) => col == 0 ? GetParagraph(text, fontSize: "30", fontStyle: "黑体", justification: JustificationValues.Left) :
                                                                 GetParagraph(text, fontSize: "30", fontStyle: "仿宋"),
                    getBorders: TableNonBorders,
                    tableWidth: "8773",
                    columnWidths: new string[] { "2260", "6513" },
                    rowHeight: 680U),
                new Break() { Type = BreakValues.Page },


                GetParagraph("摘  要", fontSize: "30", fontStyle: "黑体", spacingBetweenLines: new SpacingBetweenLines() { Before = "360", After = "360", Line = "400", LineRule = LineSpacingRuleValues.Exact }),
                GetText("摘要内容", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                new Break() { Type = BreakValues.Page },


                GetParagraph("光伏发电项目组件到场与安装检测技术报告", fontSize: "36", fontStyle: "黑体", spacingBetweenLines: new SpacingBetweenLines() { Before = "480", After = "480", Line = "600", LineRule = LineSpacingRuleValues.Exact }),

                GetTitle("1  前言", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("项目简介", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetTitle("2  检测目的", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("通过取样开展光伏组件外观检查、电致发光（EL）检测与实验室最大功率检测，以确认光伏组件供货与安装质量状况，确认到场与已安装光伏组件是否满足集团公司及项目建设单位相关技术标准要求。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetTitle("3  依据标准", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("IEC61215-2-2021 地面用光伏组件(PV)——设计鉴定和定型——第二部分：测试方法", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("IEC62446-1-2016 光伏系统——测试、文件和维护——第一部分：并网光伏系统——文件、调试测试和检验 ", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("GB 50794-2012 光伏发电站施工规范", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("GB 50797-2012 光伏发电站设计规范", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("《中国华电集团有限公司2022年单晶硅光伏组件框架采购招标文件》（技术部分）", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("《中国华电集团有限公司光伏发电工程达标投产验收管理规范》（中国华电建函〔2020〕91号）", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("《中国华电集团有限公司风力发电、光伏发电企业技术监督实施规范》（中国华电生函〔2022〕480号）", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("其他必要的商务、技术文件", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetTitle("4  检测样品", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),

                GetTitle("4.1  样品信息", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),

                //GetText("检测样品为隆基生产的550W单晶硅光伏组件、晶澳生产的540W单晶硅光伏组件，标称性能参数见表4-1a、4-1b，型号铭牌见附录A。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                //GetText("表4-1a  隆基550W光伏组件标称性能参数", fontSize: "22", fontStyle: "宋体", justification: center, spacingBetweenLines: spacing_240_120),


                GetTitle("4.2  抽检数量", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),

                GetTitle("5  仪器设备", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),

                GetTitle("6  光伏组件外观检查", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),

                GetTitle("6.1  检查说明", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),


                GetTitle("6.1.1  检查目的", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("检查光伏组件的外观，是否有明显破损、缺陷情况，并记录。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetTitle("6.1.2  检查方法与判定标准", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("（1）检查方法：以目视方法检查光伏组件背板、边框、正面盖板以及电池片是否存在明显外观损伤。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("（2）判定标准：根据《地面用光伏组件（PV）——设计鉴定和定型——第二部分：测试方法》（IEC 61215-2-2021）标准，外观合格的光伏组件不应存在下列任何一项缺陷：", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetText("a.开裂，弯曲，不规整或外表的损伤；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("b.电池破碎；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("c.电池存在裂纹；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("d.互联线或接头处存在缺陷；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("e.电池互相接触或与边框接触；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("f.密封不良；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("g.在组件的边缘和任何一部分电路之间形成连续的气泡或脱层通道；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("h.塑料材料表面不洁；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("i.接线盒破损，连接线裸露，密封不好等；", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText("j.可能影响组件性能的其他任何情况。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetTitle("6.2  检查结果", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("检查结果表明：", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetTitle("7  光伏组件点致发光（EL）检测", fontSize: "30", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle("7.1  检查说明", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle("7.1.1  检测目的", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("检测光伏组件是否存在缺陷情况，例如：隐裂、黑片、断栅及混档等现象。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),


                GetTitle("7.1.2  检测方法", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("对光伏组件进行反向充电，使其发出近红外光，并用EL检测专用相机进行拍照。重点观察隐裂、黑片、断栅、裂片等问题。检测时保留影像，记录问题组件序列号，以便后续查找。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetTitle("7.1.3  检测判定条件", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText("本次光伏组件EL检测判定标准主要参照《中国华电集团有限公司2022年单晶硅光伏组件框架采购招标文件》（技术部分）与《中国华电集团有限公司光伏发电工程达标投产验收管理规范》（中国华电建函〔2020〕91号）等相关要求，具体内容见表7-1。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                GetText("表7-1  晶体硅光伏组件抽检EL检测判定标准", fontSize: "22", fontStyle: "宋体", justification: center, spacingBetweenLines: spacing_240_120),


                GetTable(
                    texts: new string[][] {
                        new string[]{"序号","项目","A1极品","说明"},
                        new string[]{"1","断栅\r\n栅线粗细不均\r\n","1、栅线颜色一致，无氧化、黄变，断栅长度≤1 mm，单片电池≤3处，一块光伏组件断栅电池≤电池数量的3%；\r\n2、不允许连续性断栅；\r\n3、双面电池背面断栅长度≤3mm，单片电池≤10处，同一块组件断栅电池≤电池数量的10%。\r\n"," " },
                        new string[]{"1","断栅\r\n栅线粗细不均\r\n","1、栅线颜色一致，无氧化、黄变，断栅长度≤1 mm，单片电池≤3处，一块光伏组件断栅电池≤电池数量的3%；\r\n2、不允许连续性断栅；\r\n3、双面电池背面断栅长度≤3mm，单片电池≤10处，同一块组件断栅电池≤电池数量的10%。\r\n"," " },
                        new string[]{"2","崩边缺角","1、每片存在长≤2mm，深≤0.8mm的崩边≤3处，每块光伏组件上述崩边电池片≤2片；\r\n2、每片存在长≤1mm，深≤0.5mm的U型缺口≤1处；\r\n3、每块光伏组件上述型缺口电池片≤2片。\r\n"," " },
                        new string[]{"3","碎片", "无论任何类型的碎片均不允许。"," "},
                        new string[]{"4","间距","无论任何类型的碎片均不允许。"," "},
                        new string[]{"5", "背板\r\n缺陷\r\n","1、凹凸高度≤0.3mm的忽略不计；0.3-0.5mm，个数≤10处；\r\n2、背膜处无气泡、鼓包；\r\n3、无背板褶皱、划伤、气泡、鼓泡；\r\n4、不允许背板划痕。\r\n"," " },
                        new string[]{"6", "玻璃结石\r\n划伤\r\n","1、凹凸高度≤0.3mm的忽略不计；0.3-0.5mm，个数≤10处；\r\n2、背膜处无气泡、鼓包；\r\n3、无背板褶皱、划伤、气泡、鼓泡；\r\n4、不允许背板划痕。\r\n" ," "},
                        new string[]{ "7", "玻璃气泡", "圆形气泡直径≤1.0mm不超5个，长形气泡长度≤2.0mm，宽度≤0.5mm，不超过5个。"," " },
                        new string[]{ "8", "边框错位\r\n、毛边、缝隙\r\n", "边角错位≤0.5mm；缝隙≤0.5mm。", " " },
                        new string[]{ "9", "边框溢胶", "背面硅胶不允许有缝隙、气泡，且胶量基本保持一致。", " " },
                        new string[]{ "10", "隐裂\r\n与EL碎片\r\n", "1、长度小于10mm 的隐裂忽略不计（单条隐裂长度或交叉隐裂较长的长度）；\r\n2、隐裂纹造成电池片损失面积≤3%，同一电池片上只允许有3条隐裂纹，同一组件上允许隐裂电池片数≤6%电池片总数；\r\n3、同一组件内允许交叉隐裂电池片≤2%电池片总数；\r\n4、不允许隐裂纹造成电池片损失面积＞3%；\r\n5、片状隐裂：不允许；单片缺角面积≤4mm²，单片电池≤1处，碎片无脱离，同一块组件缺角电池数量≤1片\r\n", "  " },
                        new string[]{ "11", "黑片", "不允许。", " " },
                        new string[]{ "12", "电池\r\n片黑斑\r\n", "单片不发光面积小于电池片面积的10%，单块光伏组件不允许超过光伏组件总片数10%。", " " },
                        new string[]{ "13", "电池片混档", "不允许出现灰度值相差＞35%的明暗电池片。", " " },
                        new string[]{ "14", "黑边", "1）黑边宽度≤1/8电池片，数量不计；\r\n2）黑边宽度＞1/5电池片宽度，不允许；\r\n3）1/8电池片＜黑边宽度≤1/5电池片，黑边电池片片数占光伏组件总片数比例≤10%。\r\n", " " },

                    },
                    getParagraph: (row, col, text) => col == 2 ? GetParagraph(text, fontSize: "21", fontStyle: "宋体", spacingBetweenLines: spacing_60_60, justification: JustificationValues.Left) : GetParagraph(text, fontSize: "21", spacingBetweenLines: spacing_60_60, fontStyle: "宋体"),
                    getBorders: TableTextBorders,
                    onTableProperties: properties =>
                    {
                        TableCellMarginDefault tableCellMarginDefault1 = new TableCellMarginDefault();
                        TopMargin topMargin1 = new TopMargin() { Width = "0", Type = TableWidthUnitValues.Dxa };
                        TableCellLeftMargin tableCellLeftMargin1 = new TableCellLeftMargin() { Width = 108, Type = TableWidthValues.Dxa };
                        BottomMargin bottomMargin1 = new BottomMargin() { Width = "0", Type = TableWidthUnitValues.Dxa };
                        TableCellRightMargin tableCellRightMargin1 = new TableCellRightMargin() { Width = 108, Type = TableWidthValues.Dxa };

                        tableCellMarginDefault1.Append(topMargin1);
                        tableCellMarginDefault1.Append(tableCellLeftMargin1);
                        tableCellMarginDefault1.Append(bottomMargin1);
                        tableCellMarginDefault1.Append(tableCellRightMargin1);
                        properties.Append(tableCellMarginDefault1);
                    },
                    tableWidth: "8773",
                    columnWidths: new string[] { "704", "998", "5103", "1700" },
                    rowHeight: 0),

                new CreateEmptyParagraph().AddParagraph(),
                GetTitle("7.2  检测结果", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle($"7.2.1  {type}光伏组件", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText($"共抽检光伏组件{_taskDetails.Count}块，" + (errIds.Length == 0 ? "未检出不合格组件。" : $"共检出{errIds.Distinct().Count()}块不合格组件。"), fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText($"{type}光伏组件（有缺陷）EL检测图像见附录B。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText($"{type}光伏组件（无缺陷）EL检测图像见附录C。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText($"表7-2  {type}光伏组件EL检测不合格缺陷统计表", fontSize: "22", fontStyle: "宋体", justification: center, spacingBetweenLines: spacing_240_120),

                table_error_statistic,

                GetTitle("8  光伏组件实验室最大功率检测", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle("8.1  检测说明", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle($"8.1.1  检测目的", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText($"在标准测试条件（1000W/m2，25℃）下对光伏组件进行最大功率检测，确认检测结果是否满足光伏组件供货合同或技术协议中的相关规定。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetTitle($"8.1.2  检测方法与判定标准", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText($"（1）检测方法：使用太阳能模拟器，对光伏组件在标准测试条件下的最大功率值进行检测。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText($"（2）判定标准：参考《中国华电集团有限公司2022年单晶硅光伏组件框架采购招标文件》（技术部分）以及本项目光伏组件供货合同、技术协议中对光伏组件最大功率的约定值，结合本次检测结果进行综合分析。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),


                GetTitle("8.2  检测结果", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle($"8.2.1  外观检查结果", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle($"8.2.2  电致发光（EL）检测结果", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle($"8.2.3  最大功率检测结果", fontSize: "24", outlineLevel: new OutlineLevel { Val = 2 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle("8.3  检测结果分析", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),


                GetTitle("9  结论与建议", outlineLevel: new OutlineLevel { Val = 0 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetTitle("9.1  结论", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),

                GetTitle($"9.2  相关建议", fontSize: "28", outlineLevel: new OutlineLevel { Val = 1 }, justification: left, spacingBetweenLines: spacing_480_240_400),
                GetText($"建议项目公司根据集团公司招标文件技术规范书及该项目供货合同技术规范书等要求，对检测存在缺陷的组件进行确认，是否满足相关要求。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText($"建议项目公司在后续项目施工过程中，严格管控现场光伏组件安装规范，避免施工人员踩踏或碰撞已安装组件，导致光伏组件受损。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),
                GetText($"建议运维期间，按照《中国华电集团有限公司风力发电、光伏发电企业技术监督实施规范》（中国华电生函〔2022〕480号）要求，开展定期检测工作，保障设备安全可靠运行。", fontStyle: "宋体", spacingBetweenLines: spacing_400, indentation: indent_200),

                new CreateEmptyParagraph().AddParagraph(),
                GetTitle($"附录B {type}光伏组件（有缺陷）EL检测图像", outlineLevel: new OutlineLevel { Val = 0 }, justification: center, spacingBetweenLines: spacing_240_120_400),
                table_error_image,

                GetTitle($"附录C {type}光伏组件（无缺陷）EL检测图像", outlineLevel: new OutlineLevel { Val = 0 }, justification: center, spacingBetweenLines: spacing_240_120_400),
                table_normal_image,
                new Break { Type = BreakValues.Page },

                GetText($"声  明", fontStyle: "黑体", fontSize: "48", justification: center, spacingBetweenLines: spacing_480_960_600, indentation: null),
                GetText($"（1）本技术报告的著作权属华电电力科学研究院有限公司，未经我院书面许可，任何单位和个人不得擅自复印本报告或擅自公开发表。", fontStyle: "宋体", fontSize: "32", justification: left, spacingBetweenLines: spacing_360_auto, indentation: indent_640_200),
                GetText($"（2）无华电电力科学研究院有限公司技术报告专用章或检验检测专用章的技术报告，属于无效报告。", fontStyle: "宋体", fontSize: "32", justification: left, spacingBetweenLines: spacing_360_auto, indentation: indent_640_200),
                GetText($"（3）我院对出具技术报告数据的准确性、分析结果或推断结论的正确性负责。如对本报告有任何疑问，可与我院归口管理部门（质量管理部）联系。", fontStyle: "宋体", fontSize: "32", justification: left, spacingBetweenLines: spacing_360_auto, indentation: indent_640_200),
                new Break { Type = BreakValues.Page },

                GetText($"服务电话", fontStyle: "黑体", fontSize: "48", justification: center, spacingBetweenLines: null, indentation: null),
                new CreateEmptyParagraph().AddParagraph(),

                GetText($"华电电力科学研究院有限公司质量管理部：0571-85246363", fontStyle: "黑体", fontSize: "32", justification: center, spacingBetweenLines: spacing_360_auto, indentation: null),
                GetText($"华电电力科学研究院有限公司相关业务部门联系电话：", fontStyle: "黑体", fontSize: "32", justification: center, spacingBetweenLines: spacing_360_auto, indentation: null),
                GetTable(
                    texts: new string[][] {
                        new string[]{"办公室", "0571-85246838",   "0571-85246756"},
                        new string[]{"安全生产部",   "0571-85246711",   "0571-85246363"},
                        new string[]{"计划发展部",   "0571-85246814",   "0571-85246419"},
                        new string[]{"财务资产部",   "0571-85246815",   "0571-85246802"},
                        new string[]{"电力工业产品质量标准研究所",   "0571-85246218",   "0571-85246290"},
                        new string[]{"技术监督中心",  "0571-85246755",   "0571-85246364"},
                        new string[]{"汽机及燃机技术部",    "0571-85246788",   "0571-85246760"},
                        new string[]{"锅炉及环化技术部",    "0571-85246716",   "0571-85246255"},
                        new string[]{"电气及热控技术部",    "0571-85246619",   "0571-85246781"},
                        new string[]{"材料技术部",   "0571-85246203",   "0571-85246232"},
                        new string[]{"供热技术部",   "0571-85246757",   "0571-85246283"},
                        new string[]{"调试技术中心",  "0571-85246835",   "0571-85246114"},
                        new string[]{"分布式能源技术部",    "0571-85246830",   "0571-85246083"},
                        new string[]{"水电与新能源技术部",   "0571-85246082",   "0571-85246236"},
                        new string[]{"新技术研发中心", "0571-85246099",   "0571-85246774"},
                        new string[]{"电煤质检中心",  "0571-85246790",   "0571-85246242"},
                        new string[]{"环境保护监督技术中心",  "0571-85246826",   "0571-85246727"},
                        new string[]{"分析测试中心",  "0571-85246715",   "0571-85246208"},
                        new string[]{"东北分院",    "024-23782689",    "024-23782685"},
                        new string[]{"山东分院",    "0531-82817910",   "0531-82817870"},
                        new string[]{"西安分院",    "029-85503521",    "029-85503383"},
                        new string[]{"中南区域中心",  "027-88076776","/"},
                        new string[]{"西南区域中心",  "028-62355545",    "/"},
                        new string[]{"华电电力科学研究院有限公司传真", "0571-88083037",   "/"},
                        new string[]{"客户服务电子邮箱",    "zlglb@chder.com", "/"},
                    },
                    getParagraph: (row, col, text) => GetText(text, fontSize: "21", spacingBetweenLines: spacing_400),
                    getBorders: TableNonBorders,
                    onTableProperties: properties =>
                    {
                        TableCellMarginDefault tableCellMarginDefault1 = new TableCellMarginDefault();
                        TopMargin topMargin1 = new TopMargin() { Width = "0", Type = TableWidthUnitValues.Dxa };
                        TableCellLeftMargin tableCellLeftMargin1 = new TableCellLeftMargin() { Width = 108, Type = TableWidthValues.Dxa };
                        BottomMargin bottomMargin1 = new BottomMargin() { Width = "0", Type = TableWidthUnitValues.Dxa };
                        TableCellRightMargin tableCellRightMargin1 = new TableCellRightMargin() { Width = 108, Type = TableWidthValues.Dxa };

                        tableCellMarginDefault1.Append(topMargin1);
                        tableCellMarginDefault1.Append(tableCellLeftMargin1);
                        tableCellMarginDefault1.Append(bottomMargin1);
                        tableCellMarginDefault1.Append(tableCellRightMargin1);
                        properties.Append(tableCellMarginDefault1);
                    },
                    tableWidth: "7939",
                    columnWidths: new string[] { "3402", "2268", "2269", },
                    rowHeight: 0)
        );
        //pr.Append(contentPr);
        return contentPr;
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
