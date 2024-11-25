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

public class CreateWord
{

    private List<ICreateParagraph> _tempList;
    public CreateWord()
    {
        _tempList = new List<ICreateParagraph>();
    }
    public void CreateMainDocumentPart(MainDocumentPart part, params ICreateParagraph[] pas)
    {
        // WebSettingsPart webSettingsPart1 = part.AddNewPart<WebSettingsPart>("rId3");
        // GenerateWebSettingsPart1Content(webSettingsPart1);

        // ThemePart themePart1 = part.AddNewPart<ThemePart>("rId7");
        // GenerateThemePart1Content(themePart1);

        // DocumentSettingsPart documentSettingsPart1 = part.AddNewPart<DocumentSettingsPart>("rId2");
        // GenerateDocumentSettingsPart1Content(documentSettingsPart1);

        // StyleDefinitionsPart styleDefinitionsPart1 = part.AddNewPart<StyleDefinitionsPart>("rId1");
        // GenerateStyleDefinitionsPart1Content(styleDefinitionsPart1);

        // FontTablePart fontTablePart1 = part.AddNewPart<FontTablePart>("rId6");
        // GenerateFontTablePart1Content(fontTablePart1);

        // EndnotesPart endnotesPart1 = part.AddNewPart<EndnotesPart>("rId5");
        // GenerateEndnotesPart1Content(endnotesPart1);

        // FootnotesPart footnotesPart1 = part.AddNewPart<FootnotesPart>("rId4");
        // GenerateFootnotesPart1Content(footnotesPart1);
        _tempList.AddRange(pas);
        GeneratePartContent(part, pas);

    }

    // Generates content of part.
    private void GeneratePartContent(MainDocumentPart part, params ICreateParagraph[] pas)
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
        body1.Append(sectionProperties1);


        // Paragraph paragraph33 = new Paragraph()
        // {
        //     RsidParagraphAddition = "00B56F91",
        //     RsidRunAdditionDefault = "00B56F91"
        // }

        // ;

        // BookmarkStart bookmarkStart1 = new BookmarkStart()
        // {
        //     Name = "_GoBack",
        //     Id = "0"
        // }

        // ;

        // BookmarkEnd bookmarkEnd1 = new BookmarkEnd()
        // {
        //     Id = "0"
        // }

        // ;

        // paragraph33.Append(bookmarkStart1);
        // paragraph33.Append(bookmarkEnd1);

        // SectionProperties sectionProperties1 = new SectionProperties()
        // {
        //     RsidR = "00B56F91"
        // }

        // ;

        //PageSize pageSize1 = new PageSize()
        //{
        //    Width = (UInt32Value)11906U,
        //    Height = (UInt32Value)16838U
        //}

        // ;

        // PageMargin pageMargin1 = new PageMargin()
        // {
        //     Top = 1440,
        //     Right = (UInt32Value)1800U,
        //     Bottom = 1440,
        //     Left = (UInt32Value)1800U,
        //     Header = (UInt32Value)851U,
        //     Footer = (UInt32Value)992U,
        //     Gutter = (UInt32Value)0U
        // }

        // ;

        // Columns columns1 = new Columns()
        // {
        //     Space = "425"
        // }

        // ;

        // DocGrid docGrid1 = new DocGrid()
        // {
        //     Type = DocGridValues.Lines,
        //     LinePitch = 312
        // };

        // sectionProperties1.Append(pageSize1);
        // sectionProperties1.Append(pageMargin1);
        // sectionProperties1.Append(columns1);
        // sectionProperties1.Append(docGrid1);


        //   body1.Append(sectionProperties1);

        document1.Append(body1);

        part.Document = document1;
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

}