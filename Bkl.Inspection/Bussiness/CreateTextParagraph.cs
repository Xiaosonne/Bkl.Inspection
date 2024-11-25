using DocumentFormat.OpenXml.Wordprocessing;
using W14 = DocumentFormat.OpenXml.Office2010.Word;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using Pic = DocumentFormat.OpenXml.Drawing.Pictures;
using Wps = DocumentFormat.OpenXml.Office2010.Word.DrawingShape;
using V = DocumentFormat.OpenXml.Vml;
using Ovml = DocumentFormat.OpenXml.Vml.Office;
using M = DocumentFormat.OpenXml.Math;
using Ds = DocumentFormat.OpenXml.CustomXmlDataProperties;
using Ap = DocumentFormat.OpenXml.ExtendedProperties;
using Op = DocumentFormat.OpenXml.CustomProperties;
using Vt = DocumentFormat.OpenXml.VariantTypes;
using A14 = DocumentFormat.OpenXml.Office2010.Drawing;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Drawing.Charts;

public interface ICreateParagraph
{
    OpenXmlElement AddParagraph();
    void Done();
}

public class CreateTextParagraph : ICreateParagraph
{
    public  Paragraph EmptyParagraph = new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "80", After = "80" }));
    private Indentation _ind;
    private string _text;

    private RunFonts _font;
    private string _fontSize;
    private JustificationValues _justification;
    private SpacingBetweenLines _lines;
    private int _outlineLevel;
    private Spacing _spacing;

    public CreateTextParagraph(
        string text,
        RunFonts font = null,
        string fontSize = "36",
        JustificationValues values = JustificationValues.Left,
        Indentation ind = null,
        SpacingBetweenLines lines = null,
        int outlineLevel=0,
        Spacing spacing=null
        )
    {
        _ind = ind;
        _text = text;
        _font = font;
        _fontSize = fontSize;
        _justification = values;
        _lines = lines;
        _outlineLevel = outlineLevel;
        _spacing = spacing;
    }
    public OpenXmlElement AddParagraph()
    {
        var pr = new ParagraphProperties(
            new AdjustRightIndent { Val = false },
            new SnapToGrid() { Val = false },
            _lines == null ? new SpacingBetweenLines() : _lines,
            new Justification { Val = _justification },
            _ind == null ? new Indentation { Left = "0", LeftChars = 0 } : _ind);
        if (_outlineLevel > 0)
        {
            pr.Append(new BookmarkStart { Name = "_GoBack", Id = "0" });
            pr.Append(new OutlineLevel() { Val = _outlineLevel });
        }

        return new Paragraph(
            pr,
            new Run(
            new RunProperties(
                _font == null ? new RunFonts() : _font,
                _spacing == null ? new Spacing():_spacing,
                new FontSizeComplexScript
                {
                    Val = _fontSize
                }
            ),
            new Text(_text)));
    }

    public void Done()
    {
    }
}