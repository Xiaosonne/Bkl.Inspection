using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using Thm15 = DocumentFormat.OpenXml.Office2013.Theme;
using Ovml = DocumentFormat.OpenXml.Vml.Office;
using V = DocumentFormat.OpenXml.Vml;
using M = DocumentFormat.OpenXml.Math;
using W15 = DocumentFormat.OpenXml.Office2013.Word;
using System;
using System.Collections.Generic;
using System.Linq;
using AW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using System.IO;
using System.Net;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using System.Diagnostics.SymbolStore;

namespace Bkl.Inspection.Bussiness
{
    public class CreateWordHelper
    {
        private readonly List<Dictionary<string, string>> _data;
        private List<ICreateParagraph> _tempList;
        public CreateWordHelper(List<Dictionary<string, string>> data)
        {
            _data = data;
            _tempList = new List<ICreateParagraph>();
        }
        public void CreateMainDocumentPart(MainDocumentPart part, string minioUrl, params ICreateParagraph[] pas)
        {

            _tempList.AddRange(pas);
            GeneratePartContent(part, minioUrl, pas);

        }

        // Generates content of part.
        private void GeneratePartContent(MainDocumentPart part, string minioUrl, params ICreateParagraph[] pas)
        {
            Document document1 = new Document()
            {
                MCAttributes = new MarkupCompatibilityAttributes()
                {
                    Ignorable = "w14 w15 wp14"
                }
            }

            ;
            document1.AddNamespaceDeclaration("wpc", "http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas");
            document1.AddNamespaceDeclaration("mc", "http://schemas.openxmlformats.org/markup-compatibility/2006");
            document1.AddNamespaceDeclaration("o", "urn:schemas-microsoft-com:office:office");
            document1.AddNamespaceDeclaration("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");
            document1.AddNamespaceDeclaration("m", "http://schemas.openxmlformats.org/officeDocument/2006/math");
            document1.AddNamespaceDeclaration("v", "urn:schemas-microsoft-com:vml");
            document1.AddNamespaceDeclaration("wp14", "http://schemas.microsoft.com/office/word/2010/wordprocessingDrawing");
            document1.AddNamespaceDeclaration("wp", "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing");
            document1.AddNamespaceDeclaration("w10", "urn:schemas-microsoft-com:office:word");
            document1.AddNamespaceDeclaration("w", "http://schemas.openxmlformats.org/wordprocessingml/2006/main");
            document1.AddNamespaceDeclaration("w14", "http://schemas.microsoft.com/office/word/2010/wordml");
            document1.AddNamespaceDeclaration("w15", "http://schemas.microsoft.com/office/word/2012/wordml");
            document1.AddNamespaceDeclaration("wpg", "http://schemas.microsoft.com/office/word/2010/wordprocessingGroup");
            document1.AddNamespaceDeclaration("wpi", "http://schemas.microsoft.com/office/word/2010/wordprocessingInk");
            document1.AddNamespaceDeclaration("wne", "http://schemas.microsoft.com/office/word/2006/wordml");
            document1.AddNamespaceDeclaration("wps", "http://schemas.microsoft.com/office/word/2010/wordprocessingShape");

            Body body1 = new Body();

            //封面
            body1.Append(GetCoverPage());

            // 在文档开头添加特殊字符

            Paragraph specialCharParagraph = new Paragraph(new Run(new Text("目录")));
            body1.Append(specialCharParagraph);

            List<TitleModel> titleList = new List<TitleModel>();
            int i = 0;
            foreach (var paragraph1 in pas)
            {
                body1.Append(paragraph1.AddParagraph());
                Console.WriteLine($"WordCreatePara total:{pas.Length} cur:{i}");
                i++;
            }

            SectionProperties sectionProperties1 = new SectionProperties() { RsidRPr = "00EC0123", RsidR = "00EC0123", RsidSect = "0009565B" };
            PageSize pageSize1 = new PageSize() { Width = (UInt32Value)11906U, Height = (UInt32Value)16838U };
            PageMargin pageMargin1 = new PageMargin() { Top = 1701, Right = (UInt32Value)1701U, Bottom = 1701, Left = (UInt32Value)1701U, Header = (UInt32Value)851U, Footer = (UInt32Value)992U, Gutter = (UInt32Value)0U };
            Columns columns1 = new Columns() { Space = "425" };
            DocGrid docGrid1 = new DocGrid() { Type = DocGridValues.Lines, LinePitch = 312 };

            sectionProperties1.Append(pageSize1);
            sectionProperties1.Append(pageMargin1);
            sectionProperties1.Append(columns1);
            sectionProperties1.Append(docGrid1);

            // 设置封面页不同的页眉页脚
            sectionProperties1.Append(new TitlePage());

            body1.Append(sectionProperties1);
            document1.Body = body1;
            part.Document = document1;
            //获取标题
            foreach (Paragraph paragraph in body1.Elements<Paragraph>())
            {
                string name = string.Empty;
                string title = GetTitleFromParagraph(paragraph, part, ref name);
                Paragraph titleParagraph = new Paragraph(new Run(new Text(title)));
                if (!string.IsNullOrEmpty(title))
                {
                    titleList.Add(new TitleModel
                    {
                        Title = title,
                        Name = name,
                        Paragraph = titleParagraph

                    });
                }
            }
            //生成目录
            Body catalog = AddTitle(titleList);

            // 插入目录
            var specialCharParagraphs = body1.Elements<Paragraph>().Where(p => p.InnerText.Contains("目录")).ToList();
            if (specialCharParagraphs.Any())
            {
                Paragraph specialParagraph = specialCharParagraphs.First();
                OpenXmlElement parentElement = specialParagraph.Parent;
                foreach (var element in catalog.Elements())
                {
                    parentElement.InsertAfter(element.CloneNode(true), specialParagraph);

                }
                specialParagraph.Remove();
            }
            document1.Body = (Body)body1.CloneNode(true);
            part.Document = (Document)document1.CloneNode(true);

            // 创建页眉和页脚
            CreateHeaderFooter(part, minioUrl);

            // 创建文档设置（文档打开自动更新目录提示）
            DocumentSettingsPart settingsPart = part.AddNewPart<DocumentSettingsPart>("rId11");
            settingsPart.Settings = new Settings(
                new UpdateFieldsOnOpen() { Val = true }
            );

            //document1.Append(body1);

            //part.Document = document1;

        }
        /// <summary>
        /// 创建页眉页脚
        /// </summary>
        /// <param name="mainPart"></param>
        public void CreateHeaderFooter(MainDocumentPart mainPart, string minioUrl)
        {
            Document document = mainPart.Document;

            Body body = document.Body;
            IEnumerable<SectionProperties> sections = body.Elements<SectionProperties>();

            foreach (var section in sections)
            {
                // 设置奇偶页的页眉页脚
                section.Append(new EvenAndOddHeaders());

                // 创建奇数页的页眉
                HeaderPart oddHeaderPart = mainPart.AddNewPart<HeaderPart>("rId" + GenerateId());
                GenerateHeader(oddHeaderPart, minioUrl);

                // 创建偶数页的页眉
                HeaderPart evenHeaderPart = mainPart.AddNewPart<HeaderPart>("rId" + GenerateId());
                GenerateHeader(evenHeaderPart, minioUrl);

                // 创建奇数页的页脚
                FooterPart oddFooterPart = mainPart.AddNewPart<FooterPart>("rId" + GenerateId());
                GeneratePageNumberFooter(oddFooterPart, new Justification { Val = JustificationValues.Right });

                // 创建偶数页的页脚
                FooterPart evenFooterPart = mainPart.AddNewPart<FooterPart>("rId" + GenerateId());
                GeneratePageNumberFooter(evenFooterPart);

                // 在节中设置奇偶页的页眉页脚引用
                HeaderReference oddHeaderRef = new HeaderReference() { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(oddHeaderPart) };
                HeaderReference evenHeaderRef = new HeaderReference() { Type = HeaderFooterValues.Even, Id = mainPart.GetIdOfPart(evenHeaderPart) };
                FooterReference oddFooterRef = new FooterReference() { Type = HeaderFooterValues.Default, Id = mainPart.GetIdOfPart(oddFooterPart) };
                FooterReference evenFooterRef = new FooterReference() { Type = HeaderFooterValues.Even, Id = mainPart.GetIdOfPart(evenFooterPart) };

                section.Append(oddHeaderRef);
                section.Append(oddFooterRef);
                section.Append(evenHeaderRef);
                section.Append(evenFooterRef);
            }
        }

        /// <summary>
        /// 页脚页码
        /// </summary>
        /// <param name="footerPart"></param>
        /// <param name="justification"></param>
        private void GeneratePageNumberFooter(FooterPart footerPart, Justification justification = null)
        {
            Footer footer = new Footer();
            Paragraph paragraph = new Paragraph();
            ParagraphProperties paragraphProperties = new ParagraphProperties();
            if (justification == null)
                justification = new Justification() { Val = JustificationValues.Left };

            paragraphProperties.Append(justification);


            // 添加页码
            Paragraph footerParagraph = new Paragraph(
                paragraphProperties,
                new Run(
                    new SimpleField() { Instruction = "PAGE" }
                )
            );
            footer.Append(footerParagraph);

            footerPart.Footer = footer;


        }

        /// <summary>
        /// 页眉
        /// </summary>
        /// <param name="headerPart"></param>
        private void GenerateHeader(HeaderPart headerPart, string imagePath)
        {
            Header header = new Header();
            imagePath = imagePath + "/页眉.png";
            Table table = new Table();
            TableProperties tableProperties = new TableProperties();
            tableProperties.Append(new TableWidth() { Width = "5000", Type = TableWidthUnitValues.Pct }); // 宽度设置为 100%
            table.Append(tableProperties);

            TableRow row = new TableRow();

            TableCell cell1 = new TableCell();
            TableCellProperties cellProperties1 = new TableCellProperties();
            cellProperties1.Append(new TableCellWidth() { Width = "2500", Type = TableWidthUnitValues.Pct }); // 单元格宽度设置为 50%
            cellProperties1.Append(new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }); // 左对齐
            cell1.Append(cellProperties1);
            var draw = AddImageToBody(headerPart, imagePath, 2);
            cell1.Append(new Paragraph(draw == null ? new Run(new Text("")) : draw));

            TableCell cell2 = new TableCell();
            TableCellProperties cellProperties2 = new TableCellProperties();
            cellProperties2.Append(new TableCellWidth() { Width = "2500", Type = TableWidthUnitValues.Pct }); // 单元格宽度设置为 50%
            cellProperties2.Append(new TableCellVerticalAlignment() { Val = TableVerticalAlignmentValues.Center }); // 右对齐

            cell2.Append(cellProperties2);
            cell2.Append(new Paragraph(new ParagraphProperties(new Justification
            {
                Val = JustificationValues.Right
            }, new Run(new Text("AI智能巡检分析报告"))

            )));
            row.Append(cell1);
            row.Append(cell2);
            table.Append(row);
            header.Append(table);

            headerPart.Header = header;
        }

        // 生成唯一的 ID
        private string GenerateId()
        {
            return Guid.NewGuid().ToString("N");
        }


        /// <summary>
        /// 设置目录
        /// </summary>
        /// <param name="titles"></param>
        /// <returns></returns>
        public Body AddTitle(List<TitleModel> titles)
        {

            Body body1 = new Body();

            SdtBlock sdtBlock1 = new SdtBlock();

            SdtProperties sdtProperties1 = new SdtProperties(
                new RunProperties(
                    new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体", ComplexScript = "黑体" },
                    new FontSize() { Val = "24" }),
                    new SdtId() { Val = 147470740 },
                    new W15.Color() { Val = "DBDBDB" },
                    new SdtContentDocPartObject(
                        new DocPartGallery() { Val = "Table of Contents" },
                        new DocPartUnique()
                    )
                );

            SdtEndCharProperties sdtEndCharProperties1 = new SdtEndCharProperties(new RunProperties(new Bold()));

            SdtContentBlock sdtContentBlock1 = new SdtContentBlock();
            Paragraph paragraph1 = new Paragraph() { RsidParagraphAddition = "006A0F5A", RsidParagraphProperties = "006A0F5A", RsidRunAdditionDefault = "006A0F5A", ParagraphId = "086BAEFD", TextId = "77777777" };

            ParagraphProperties paragraphProperties1 = new ParagraphProperties(
                    new AdjustRightIndent() { Val = false },
                    new SnapToGrid() { Val = false },
                    new Indentation() { FirstLine = "480" },
                    new Justification() { Val = JustificationValues.Center }
                 );

            Run run1 = new Run(
                new RunProperties(
                    new RunFonts() { Hint = FontTypeHintValues.EastAsia, Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体", ComplexScript = "黑体" },
                new FontSize() { Val = "28" },
                new FontSizeComplexScript() { Val = "28" }),
                new Text("目录"));

            paragraph1.Append(paragraphProperties1);
            paragraph1.Append(run1);
            sdtContentBlock1.Append(paragraph1);
            #region 目录项 
            for (int i = 0; i < titles.Count(); i++)
            //foreach (var item in titles)
            {


                Paragraph paragraph2 = new Paragraph() { RsidParagraphAddition = "000E464F", RsidRunAdditionDefault = "006A0F5A", ParagraphId = "72F7F969", TextId = "1AC6D33D" };
                ParagraphProperties paragraphProperties2 = new ParagraphProperties(
                    new ParagraphStyleId() { Val = "TOC1" },
                    new ParagraphMarkRunProperties(
                          new RunFonts() { AsciiTheme = ThemeFontValues.MinorHighAnsi, HighAnsiTheme = ThemeFontValues.MinorHighAnsi, EastAsiaTheme = ThemeFontValues.MinorEastAsia },
                          new NoProof(),
                          new FontSize() { Val = "22" },
                          new FontSizeComplexScript() { Val = "24" }
                          )
                    );
                if (i == 0)
                {

                    Run run2 = new Run(new FieldChar() { FieldCharType = FieldCharValues.Begin });
                    Run run3 = new Run(new FieldCode() { Space = SpaceProcessingModeValues.Preserve, Text = " TOC \\o \"1-3\" \\h \\z \\u " });
                    Run run4 = new Run(new FieldChar() { FieldCharType = FieldCharValues.Separate });

                    paragraph2.Append(run2);
                    paragraph2.Append(run3);
                    paragraph2.Append(run4);
                    paragraph2.Append(paragraphProperties2);
                }


                Hyperlink hyperlink1 = new Hyperlink() { History = true, Anchor = titles[i].Name };

                Run run5 = new Run(
                    new RunProperties(
                        new RunStyle() { Val = "a8" },
                        new RunFonts() { EastAsia = "黑体", ComplexScript = "Times New Roman" },
                        new BoldComplexScript(),
                        new NoProof(),
                        new Kern() { Val = (UInt32Value)44U },
                        new Languages() { Bidi = "ar" }
                    ),
                    new Text(titles[i].Title)
                );

                Run run7 = new Run(
                    new RunProperties(
                        new NoProof(),
                        new WebHidden()
                    ),
                    new TabChar());

                Run run8 = new Run(new RunProperties(
                        new NoProof(),
                        new WebHidden()
                    ),
                    new FieldChar() { FieldCharType = FieldCharValues.Begin });

                Run run9 = new Run(new RunProperties(
                        new NoProof(),
                        new WebHidden()
                    ),
                    new FieldCode() { Space = SpaceProcessingModeValues.Preserve, Text = $" PAGEREF {titles[i].Name} \\h " });

                Run run10 = new Run(new RunProperties(
                        new NoProof(),
                        new WebHidden()
                    ));

                Run run11 = new Run(new RunProperties(
                        new NoProof(),
                        new WebHidden()
                    ),
                    new FieldChar() { FieldCharType = FieldCharValues.Separate });


                Run run12 = new Run(new RunProperties(
                            new NoProof(),
                            new WebHidden()
                        ),
                         new SimpleField() { Instruction = "PAGE" }
                        // new Text() { Text = "2" }
                        );//页码数

                Run run13 = new Run(new RunProperties(
                                        new NoProof(),
                                        new WebHidden()
                                    ),
                                    new FieldChar() { FieldCharType = FieldCharValues.End });

                hyperlink1.Append(run5);
                hyperlink1.Append(run7);
                hyperlink1.Append(run8);
                hyperlink1.Append(run9);
                hyperlink1.Append(run10);
                hyperlink1.Append(run11);
                hyperlink1.Append(run12);
                hyperlink1.Append(run13);

                paragraph2.Append(hyperlink1);
                #endregion
                #region 目录样式
                Paragraph paragraph4 = new Paragraph(
                    new ParagraphProperties(
                        new AdjustRightIndent() { Val = false },
                        new SnapToGrid() { Val = false }),
                    new Run(
                        new RunProperties(
                            new RunFonts() { Ascii = "Times New Roman", HighAnsi = "Times New Roman", EastAsia = "宋体" },
                            new FontSize() { Val = "24" })
                    , new FieldChar() { FieldCharType = FieldCharValues.End }
                    ));
                #endregion

                sdtContentBlock1.Append(paragraph2);
                if (i == titles.Count() - 1)
                {

                    sdtContentBlock1.Append(paragraph4);//目录样式
                }
            }


            sdtBlock1.Append(sdtProperties1);
            sdtBlock1.Append(sdtEndCharProperties1);
            sdtBlock1.Append(sdtContentBlock1);

            #region 空段落
            Paragraph paragraph5 = CreateWordElementsHelper.AddBreakParagraph();
            #endregion

            #region 正文

            body1.Append(paragraph5);//空段落 保证目录占整页
            body1.Append(sdtBlock1);


            #endregion

            return body1;
        }
        /// <summary>
        /// 根据BookmarkStart的ID找标题
        /// </summary>
        /// <param name="paragraph"></param>
        /// <param name="mainPart"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetTitleFromParagraph(Paragraph paragraph, MainDocumentPart mainPart, ref string name)
        {
            BookmarkStart bookmarkStart = paragraph.Descendants<BookmarkStart>().FirstOrDefault();
            name = string.Empty;
            if (paragraph.ParagraphProperties?.OutlineLevel != null)
            {
                if (bookmarkStart != null && bookmarkStart.Name != null && bookmarkStart.Name.InnerText.Contains("_Toc"))
                {
                    string bookmarkId = bookmarkStart.Id;
                    // 根据 BookmarkStart 的 Id 查找对应的 BookmarkEnd 元素
                    BookmarkEnd bookmarkEnd = mainPart.Document.Body.Descendants<BookmarkEnd>()
                        .FirstOrDefault(b => b.Id == bookmarkId);
                    if (bookmarkEnd != null)
                    {
                        // 提取标题内容
                        string title = string.Concat(paragraph.Descendants<Text>().Select(t => t.Text));
                        name = bookmarkStart.Name.InnerText;
                        return title.Trim();
                    }
                }
            }
            else
            {
                // 如果 OutlineLevel 为 null，则递归检查子元素中的段落
                foreach (var childElement in paragraph.Elements())
                {
                    if (childElement is Paragraph childParagraph)
                    {
                        string result = GetTitleFromParagraph(childParagraph, mainPart, ref name);
                        if (!string.IsNullOrEmpty(result))
                        {
                            return result; // 找到第一个非 null OutlineLevel 的子段落后返回结果
                        }
                    }
                }
            }
            return null;
        }
        /// <summary>
        /// 封面页
        /// </summary>
        /// <returns></returns>
        public OpenXmlElement GetCoverPage()
        {
            List<OpenXmlElement> list = new List<OpenXmlElement>();

            for (int i = 0; i < 4; i++)
            {
                list.Add(new Paragraph());
            }

            list.Add(GetTitle("AI智能巡检分析报告"));

            Paragraph paragraph2 = new Paragraph(
               new ParagraphProperties(
                  new AdjustRightIndent() { Val = false },
                   new SnapToGrid() { Val = false },
                   new SpacingBetweenLines() { Before = "123", Line = "218", LineRule = LineSpacingRuleValues.Auto },
                   new Justification() { Val = JustificationValues.Center },
                   new ParagraphMarkRunProperties(
                      new RunFonts() { Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体", ComplexScript = "黑体" },
                      new FontSize() { Val = "28" },
                      new FontSizeComplexScript() { Val = "28" }
                      )
                  ),
               new Run(
                  new RunProperties(
                      new RunFonts() { Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体", ComplexScript = "黑体" },
                      new Spacing() { Val = -2 },
                      new FontSize() { Val = "28" },
                      new FontSizeComplexScript() { Val = "28" }
                      )
                  , new Text($"报告编号：BCR-SP{DateTime.Now.ToString("yyyyMMddHHmmss")}")
                  )
              );


            list.Add(paragraph2);
            for (int i = 0; i < 7; i++)
            {
                list.Add(new Paragraph());
            }


            list.Add(GetContent($"站点名称：{_data[0]["2"]}"));
            list.Add(GetContent($"任务名称：{_data[1]["2"]}"));
            list.Add(GetContent($"巡检周期：{_data[3]["2"]} 00:00:00至{_data[4]["2"]} 23:59:59"));

            #region 空段落
            Paragraph paragraph5 = CreateWordElementsHelper.AddBreakParagraph();
            #endregion

            list.Add(paragraph5);

            var contentPr = new Paragraph(
                  new ParagraphProperties(new Justification { Val = JustificationValues.Center },
                  new AdjustRightIndent { Val = false },
               new SnapToGrid() { Val = false }
               ),
                  new Run());



            list.ForEach(s => contentPr.Append(s));

            return contentPr;
        }
        Paragraph GetContent(string text)
        {
            Paragraph paragraph15 = new Paragraph(
                new ParagraphProperties(
                    new AdjustRightIndent() { Val = false },
                    new SnapToGrid() { Val = false },
                    new SpacingBetweenLines() { Line = "480", LineRule = LineSpacingRuleValues.Auto },
                    new Indentation() { FirstLine = "1112", FirstLineChars = 400 },
                    new ParagraphMarkRunProperties(
                        new RunFonts() { Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体", ComplexScript = "黑体" },
                        new Spacing() { Val = -2 },
                        new FontSize() { Val = "28" },
                        new FontSizeComplexScript() { Val = "28" }
                    )
                ),
                new Run(
                    new RunProperties(
                        new RunFonts() { Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体", ComplexScript = "黑体" },
                        new Spacing() { Val = -2 },
                        new FontSize() { Val = "28" },
                        new FontSizeComplexScript() { Val = "28" }
                    ),
                    new Text(text)
                )
            );
            return paragraph15;
        }
        /// <summary>
        /// 获取目录项 （标题）
        /// </summary>
        /// <param name="text"></param>
        /// <returns></returns>
        Paragraph GetTitle(string text)
        {
            Paragraph paragraph1 = new Paragraph(
                new ParagraphProperties(
                    new AdjustRightIndent() { Val = false },
                     new SnapToGrid() { Val = false },
                     new SpacingBetweenLines() { Before = "221", Line = "221", LineRule = LineSpacingRuleValues.Auto },
                     new Justification() { Val = JustificationValues.Center },
                     new ParagraphMarkRunProperties(
                        new RunFonts() { Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体", ComplexScript = "黑体" },
                        new Bold(),
                        new BoldComplexScript(),
                        new Spacing { Val = 5 },
                        new FontSize { Val = "44" },
                        new FontSizeComplexScript { Val = "44" }
                        )
                    ),
                    new Run(
                        new RunProperties(
                            new RunFonts() { Ascii = "黑体", HighAnsi = "黑体", EastAsia = "黑体", ComplexScript = "黑体" },
                            new Bold(),
                            new BoldComplexScript(),
                            new Spacing { Val = 5 },
                            new FontSize { Val = "44" },
                            new FontSizeComplexScript { Val = "44" }
                            ),
                        new Text(text))
                );

            return paragraph1;
        }


        public void Done()
        {
            foreach (var para in _tempList)
            {
                try
                {
                    para.Done();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }
        /// <summary>
        /// 插入图片（页眉）
        /// </summary>
        /// <param name="mainPart"></param>
        /// <param name="imagePath"></param>
        /// <param name="x"></param>
        /// <param name="y"></param>
        /// <returns></returns>
        private Drawing AddImageToBody(HeaderPart mainPart, string imagePath, int x = 1, int y = 1)
        {
            // 添加图片
            if (string.IsNullOrEmpty(imagePath) || mainPart == null) return new Drawing();
            ImagePart imagePart = mainPart.AddImagePart(ImagePartType.Jpeg);
            try
            {
                if (imagePath.Contains("http"))
                {

                    HttpWebRequest request = (HttpWebRequest)WebRequest.Create(imagePath);
                    HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                    // 确保请求成功  
                    if (response.StatusCode == HttpStatusCode.OK)
                    {
                        // 获取响应的内容作为流  
                        using (Stream stream = response.GetResponseStream())
                        {
                            imagePart.FeedData(stream);
                        }
                    }
                }
                else
                {
                    using (System.IO.FileStream stream = new System.IO.FileStream(imagePath, System.IO.FileMode.Open))
                    {
                        imagePart.FeedData(stream);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("页眉加载失败" + ex.ToString());
                return null;
            }
            string relationshipId = mainPart.GetIdOfPart(imagePart);
            Drawing element =
                 new Drawing(
                     new DW.Inline(
                         new DW.Extent() { Cx = 495000L * x, Cy = 350000L * y },
                         new DW.EffectExtent()
                         {
                             LeftEdge = 0L,
                             TopEdge = 0L,
                             RightEdge = 0L,
                             BottomEdge = 0L
                         },
                         new DW.DocProperties()
                         {
                             Id = (UInt32Value)1U,
                             Name = "Picture"
                         },
                         new DW.NonVisualGraphicFrameDrawingProperties(
                             new A.GraphicFrameLocks() { NoChangeAspect = true }),
                         new A.Graphic(
                             new A.GraphicData(
                                 new PIC.Picture(
                                     new PIC.NonVisualPictureProperties(
                                         new PIC.NonVisualDrawingProperties()
                                         {
                                             Id = (UInt32Value)0U,
                                             Name = "New Bitmap Image.jpg"
                                         },
                                         new PIC.NonVisualPictureDrawingProperties()),
                                     new PIC.BlipFill(
                                         new A.Blip(
                                             new A.BlipExtensionList(
                                                 new A.BlipExtension()
                                                 {
                                                     Uri = "{28A0092B-C50C-407E-A947-70E740481C1C}"
                                                 })
                                         )
                                         {
                                             Embed = relationshipId,
                                             CompressionState = A.BlipCompressionValues.Print
                                         },
                                         new A.Stretch(
                                             new A.FillRectangle())),
                                     new PIC.ShapeProperties(
                                         new A.Transform2D(
                                             new A.Offset() { X = 0L, Y = 0L },
                                             new A.Extents() { Cx = 495000L * x, Cy = 350000L * y }),
                                         new A.PresetGeometry(
                                             new A.AdjustValueList()
                                         )
                                         { Preset = A.ShapeTypeValues.Rectangle }))
                             )
                             { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" })
                     )
                     {
                         DistanceFromTop = (UInt32Value)0U,
                         DistanceFromBottom = (UInt32Value)0U,
                         DistanceFromLeft = (UInt32Value)0U,
                         DistanceFromRight = (UInt32Value)0U,
                         EditId = "50D07946"
                     });
            return element;

        }
        public class TitleModel
        {
            public string Title { get; set; }
            public string Name { get; set; }
            public Paragraph Paragraph { get; set; }
        }
    }
}
