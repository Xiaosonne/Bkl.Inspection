using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Collections.Generic;
using System.IO;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using A = DocumentFormat.OpenXml.Drawing;
using System;
using System.Linq;

namespace Bkl.Inspection.Bussiness
{
    public class CreateTitle : ICreateParagraph
    {
        private  string _text;
        private string _fontStyle;
        private string _fontSize;
        private int _outlineLevel;
        private Justification _justofocation;
        private SpacingBetweenLines _spacingBetweenLines;

        public CreateTitle(string text,string fontStyle="黑体",string fontSize="30", int outlineLevel = 0, Justification justification = null, SpacingBetweenLines spacingBetweenLines = null)
        {
            _text = text;
            _fontStyle = fontStyle;
            _fontSize = fontSize;
            _outlineLevel= outlineLevel;
            _justofocation= justification;
            _spacingBetweenLines= spacingBetweenLines;
        }

        public Paragraph GetTitle(string text, string fontStyle = "黑体", string fontSize = "30", int outlineLevel = 0, Justification justification = null, SpacingBetweenLines spacingBetweenLines = null)
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
            if (outlineLevel > 0)
                paragraphProperties1.Append(new OutlineLevel() { Val = (outlineLevel - 1) });
           

            BookmarkStart bookmarkStart1 = new BookmarkStart() { Name = $"_Toc{Guid.NewGuid().ToString()}", Id = "2" };
            //BookmarkStart bookmarkStart2 = new BookmarkStart() { Name = "_Toc95825830", Id = "3" };
            //BookmarkStart bookmarkStart3 = new BookmarkStart() { Name = "_Toc139725383", Id = "4" };

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
            //BookmarkEnd bookmarkEnd2 = new BookmarkEnd() { Id = "3" };
            //BookmarkEnd bookmarkEnd3 = new BookmarkEnd() { Id = "4" };

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(bookmarkStart1);
            //paragraph1.Append(bookmarkStart2);
            //paragraph1.Append(bookmarkStart3);
            paragraph1.Append(run1);
            paragraph1.Append(bookmarkEnd1);
            //paragraph1.Append(bookmarkEnd2);
            //paragraph1.Append(bookmarkEnd3);
            return paragraph1;

        }

        public OpenXmlElement AddParagraph()
        {
            return GetTitle(_text,_fontStyle,_fontSize,_outlineLevel,_justofocation,_spacingBetweenLines);
        }

        public void Done()
        {
            //throw new NotImplementedException();
        }

        #region 
        //private IEnumerable<Dictionary<string, string>> _dataSource;
        //private Merge _colMerge;
        //private Merge _rowMerge;
        //private string[] _column;
        //private string[] _headers;
        //private Dictionary<string, int> _colWidth;

        //public static string PercentWidth(int pers)
        //{
        //    return ((pers * 8505) / 100).ToString();
        //}
        //public class Merge
        //{
        //    public int StartRow { get; set; }
        //    public int EndRow { get; set; }
        //    public int StartCol { get; set; }
        //    public int EndCol { get; set; }
        //}
        //public CreateTableHelper(
        //    IEnumerable<Dictionary<string, string>> dataSource,
        //    string[] column,
        //    string[] headers = null,

        //    Dictionary<string, int> colWith = null, Merge colMerge = null, Merge rowMerge = null)
        //{
        //    _dataSource = dataSource;
        //    _column = column;
        //    _headers = headers;
        //    _colWidth = colWith;
        //    _colMerge = colMerge;
        //    _rowMerge = rowMerge;
        //}
        //public OpenXmlElement AddParagraph()
        //{
        //    Table table = new Table();

        //    // Create a TableProperties object and specify its border information.
        //    TableProperties tblProp = new TableProperties(
        //        new TableJustification() { Val = TableRowAlignmentValues.Center },
        //        new TableWidth() { Width = "8505", Type = TableWidthUnitValues.Dxa },
        //       new TableBorders(
        //                        new TopBorder() { Val = BorderValues.Single, Color = "#b4b8bf", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
        //                        new LeftBorder() { Val = BorderValues.Single, Color = "#b4b8bf", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
        //                        new BottomBorder() { Val = BorderValues.Single, Color = "#b4b8bf", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
        //                        new RightBorder() { Val = BorderValues.Single, Color = "#b4b8bf", Size = (UInt32Value)12U, Space = (UInt32Value)0U },
        //                        new InsideHorizontalBorder() { Val = BorderValues.Single, Color = "#e8eaec", Size = (UInt32Value)4U, Space = (UInt32Value)0U },
        //                        new InsideVerticalBorder() { Val = BorderValues.Single, Color = "#e8eaec", Size = (UInt32Value)4U, Space = (UInt32Value)0U }
        //        ),
        //        new TableCellMarginDefault(
        //            new TableCellLeftMargin() { Width = 108, Type = TableWidthValues.Dxa },
        //            new TableCellRightMargin() { Width = 108, Type = TableWidthValues.Dxa }),
        //        new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center },
        //          new TableRowHeight() { Val = (UInt32Value)600U, HeightType = HeightRuleValues.AtLeast }
        //    );
        //    table.AppendChild<TableProperties>(tblProp);




        //    if (_headers != null)
        //    {
        //        TableRow tr = new TableRow();
        //        foreach (var col in _headers)
        //        {
        //            TableCell tc1 = new TableCell();

        //            var paragraphText = new Paragraph();

        //            //垂直居中显示
        //            var tcProperties = new TableCellProperties();

        //            // 添加着色（背景色）  
        //            Shading shading = new Shading()
        //            {
        //                Val = ShadingPatternValues.Solid, // 设置为纯色  
        //                Color = "#87c8a5", // 可以使用主题颜色或 RGB 颜色值，但这里用 "auto" 作为示例  
        //                //PointFillNames = "pink" // 设置背景颜色
        //            };

        //            tcProperties.Append(shading);

        //            var tcvAligin = new TableCellVerticalAlignment { Val = TableVerticalAlignmentValues.Center };
        //            tcProperties.Append(tcvAligin);
        //            tc1.AppendChild(tcProperties);
        //            var Pproperties = new ParagraphProperties();
        //            var jc = new Justification { Val = JustificationValues.Center };
        //            Pproperties.AppendChild(jc);


        //            //设置字体样式
        //            RunProperties runproperties = new RunProperties();//属性
        //            RunFonts fonts = new RunFonts() { EastAsia = "方正兰亭黑简" };//设置字体
        //            FontSize size = new FontSize() { Val = "35" };//设置字体大小
        //            Color color = new Color() { Val = "black" };//设置字体颜色

        //            runproperties.Append(color);
        //            runproperties.Append(size);
        //            runproperties.Append(fonts);

        //            Run run = new Run();
        //            run.Append(runproperties);
        //            run.Append(new Text(col));

        //            paragraphText.Append(run);
        //            paragraphText.Append(Pproperties);

        //            tc1.AppendChild(paragraphText);
        //            tr.Append(tc1);
        //        }
        //        table.Append(tr);
        //    }


        //    int rowIndex = 0;
        //    foreach (var raw in _dataSource)
        //    {
        //        TableRow tr = new TableRow();
        //        int colIndex = 0;
        //        foreach (var col in _column)
        //        {
        //            var tcProperties = new TableCellProperties();
        //            tcProperties.Append(new TableCellWidth() { Type = TableWidthUnitValues.Dxa, Width = PercentWidth(_colWidth != null ? _colWidth[col] : 50) });
        //            tcProperties.Append(new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center });
        //            if (_rowMerge != null)
        //            {
        //                if (rowIndex <= _rowMerge.EndRow && rowIndex >= _rowMerge.StartRow && colIndex <= _rowMerge.EndCol && colIndex >= _rowMerge.StartCol)
        //                {
        //                    if (rowIndex == _rowMerge.StartRow)
        //                        tcProperties.Append(new VerticalMerge() { Val = MergedCellValues.Restart });
        //                    else
        //                        tcProperties.Append(new VerticalMerge());
        //                }
        //            }
        //            if (_colMerge != null)
        //            {
        //                if (rowIndex <= _colMerge.EndRow && rowIndex >= _colMerge.StartRow)
        //                {
        //                    if (colIndex <= _colMerge.EndCol && colIndex >= _colMerge.StartCol)
        //                    {
        //                        if (colIndex == _colMerge.StartCol)
        //                        {
        //                            tcProperties.Append(new GridSpan { Val = _colMerge.EndCol - _colMerge.StartCol + 1 });
        //                        }
        //                        else
        //                        {
        //                            colIndex++;
        //                            continue;
        //                        }
        //                    }
        //                }
        //            }
        //            tcProperties.Append(new Paragraph(
        //                new ParagraphProperties(
        //                      new SpacingBetweenLines() { Before = "60", After = "60", Line = "240", LineRule = LineSpacingRuleValues.Auto },
        //                    new Justification { Val = JustificationValues.Center }),
        //                new Run(
        //                  new RunProperties(
        //                    new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
        //                    new FontSizeComplexScript() { Val = "24" }),
        //                    new Text(raw[col])
        //                )));
        //            TableCell tc1 = new TableCell();
        //            tc1.Append(tcProperties);
        //            tr.Append(tc1);
        //            colIndex++;
        //        }
        //        rowIndex++;
        //        table.Append(tr);
        //    }
        //    return new Paragraph(
        //            new ParagraphProperties(new Justification { Val = JustificationValues.Center },
        //            new AdjustRightIndent { Val = false },
        //         new SnapToGrid() { Val = false }
        //         ),
        //            new Run(table));
        //}
        //Paragraph createText(string text)
        //{
        //    return new Paragraph(new ParagraphProperties(
        //            new AdjustRightIndent { Val = false },
        //         new SnapToGrid() { Val = false },
        //                        new SpacingBetweenLines() { Before = "60", After = "60", Line = "240", LineRule = LineSpacingRuleValues.Auto },
        //                          new Justification { Val = JustificationValues.Center },
        //                          new Run(new Text(text))));
        //}
        //public void Done()
        //{
        //}
        ///// <summary>
        ///// 插入图片
        ///// </summary>
        ///// <param name="filePath">文件路径</param>
        ///// <param name="picturePath">图片路径</param>
        //public static void AddPictureIntoWord(string filePath= "C:\\Users\\tobby\\Downloads\\file.docx", string picturePath = "C:\\Users\\tobby\\Desktop\\风机缺陷.png")
        //{
        //    using (WordprocessingDocument doc = WordprocessingDocument.Open(filePath, true))
        //    {
        //        string picType = picturePath.Split('.').Last();
        //        ImagePartType imagePartType;
        //        ImagePart imagePart = null;
        //        // 通过后缀名判断图片类型, true 表示忽视大小写
        //        if (Enum.TryParse<ImagePartType>(picType, true, out imagePartType))
        //        {
        //            imagePart = doc.MainDocumentPart.AddImagePart(imagePartType);
        //        }

        //        imagePart.FeedData(File.Open(picturePath, FileMode.Open)); // 读取图片二进制流
        //        AddImageToBody(doc, doc.MainDocumentPart.GetIdOfPart(imagePart));
        //    }
        //}

        //private static void AddImageToBody(WordprocessingDocument wordDoc, string relationshipId)
        //{
        //    // Define the reference of the image.
        //    var element =
        //       new Drawing(
        //         new DW.Inline(
        //           new DW.Extent() { Cx = 990000L, Cy = 990000L }, // 调节图片大小
        //           new DW.EffectExtent()
        //           {
        //               LeftEdge = 0L,
        //               TopEdge = 0L,
        //               RightEdge = 0L,
        //               BottomEdge = 0L
        //           },
        //           new DW.DocProperties()
        //           {
        //               Id = (UInt32Value)1U,
        //               Name = "Picture 1"
        //           },
        //           new DW.NonVisualGraphicFrameDrawingProperties(
        //             new A.GraphicFrameLocks() { NoChangeAspect = true }),
        //           new A.Graphic(
        //             new A.GraphicData(
        //               new PIC.Picture(
        //                 new PIC.NonVisualPictureProperties(
        //                   new PIC.NonVisualDrawingProperties()
        //                   {
        //                       Id = (UInt32Value)0U,
        //                       Name = "New Bitmap Image.jpg"
        //                   },
        //                   new PIC.NonVisualPictureDrawingProperties()),
        //                 new PIC.BlipFill(
        //                   new A.Blip(
        //                     new A.BlipExtensionList(
        //                       new A.BlipExtension()
        //                       {
        //                           Uri =
        //                          "{28A0092B-C50C-407E-A947-70E740481C1C}"
        //                       })
        //                   )
        //                   {
        //                       Embed = relationshipId,
        //                       CompressionState =
        //                     A.BlipCompressionValues.Print
        //                   },
        //                   new A.Stretch(
        //                     new A.FillRectangle())),
        //                 new PIC.ShapeProperties(
        //                   new A.Transform2D(
        //                     new A.Offset() { X = 6000L, Y = 4000L },
        //                     new A.Extents() { Cx = 990000L, Cy = 990000L }), //与上面的对准
        //                   new A.PresetGeometry(
        //                     new A.AdjustValueList()
        //                   )
        //                   { Preset = A.ShapeTypeValues.Rectangle }))
        //             )
        //             { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
        //         )
        //         {
        //             DistanceFromTop = (UInt32Value)0U,
        //             DistanceFromBottom = (UInt32Value)0U,
        //             DistanceFromLeft = (UInt32Value)0U,
        //             DistanceFromRight = (UInt32Value)0U,
        //             EditId = "50D07946"
        //         });

        //    // Append the reference to body, the element should be in a Run.
        //    wordDoc.MainDocumentPart.Document.Body.AppendChild(new Paragraph(new Run(element)));
        //}
        #endregion
    }
}



