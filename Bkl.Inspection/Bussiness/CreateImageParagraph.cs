using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using A = DocumentFormat.OpenXml.Drawing;
using System.IO;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures; 

public class CreateImageParagraph : ICreateParagraph
{
    Stream _stream;
    int _width;
    int _height;
    string _relationShipId;
    public CreateImageParagraph(string fileName, WordprocessingDocument doc, int width = 3780000, int height = 3780000)
    {
        _filename = System.IO.Path.GetFileName(fileName);
        _doc = doc;
        _stream = new FileStream(fileName, FileMode.Open, FileAccess.ReadWrite);
        _width = width;
        _height = height;
    }
    public CreateImageParagraph(Stream fileName, WordprocessingDocument doc, int width = 3780000, int height = 3780000)
    {
        _doc = doc;
        _stream = fileName;
        _width = width;
        _height = height;
    }
    ImagePart _imagePart;
    public CreateImageParagraph(ImagePart imagedata,string ralationShipId, int width = 3780000, int height = 3780000)
    { 
        _width = width;
        _height = height;
        _imagePart = imagedata;
        _relationShipId = ralationShipId;
    }

    private string _filename;
    WordprocessingDocument _doc;

    public OpenXmlElement AddParagraph()
    {
        if(_doc!=null  && _imagePart == null)
        {
            _imagePart = _doc.MainDocumentPart.AddImagePart(ImagePartType.Jpeg);
            _relationShipId = _doc.MainDocumentPart.GetIdOfPart(_imagePart);
            _imagePart.FeedData(_stream);
        } 


 
        // Define the reference of the image.
        var element =
             new Drawing(
                 new DW.Inline(
                     new DW.Extent() { Cx = _width, Cy = _height },
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
                         Name = _filename
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
                                         Name = _filename
                                     },
                                     new PIC.NonVisualPictureDrawingProperties()),
                                 new PIC.BlipFill(
                                     new A.Blip(
                                         new A.BlipExtensionList(
                                             new A.BlipExtension()
                                             {
                                                 Uri =
                                                    "{28A0092B-C50C-407E-A947-70E740481C1C}"
                                             })
                                     )
                                     {
                                         Embed = _relationShipId,
                                         CompressionState = A.BlipCompressionValues.None
                                     },
                                     new A.Stretch(new A.FillRectangle())),
                                 new PIC.ShapeProperties(
                                     new A.Transform2D(
                                         new A.Offset() { X = 0L, Y = 0L },
                                         new A.Extents() { Cx = _width, Cy = _height }),
                                     new A.PresetGeometry(
                                         new A.AdjustValueList()
                                     )
                                     { Preset = A.ShapeTypeValues.Rectangle })
                                 )
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

        // Append the reference to body, the element should be in a Run.
        return new Paragraph(new ParagraphProperties(
            new Justification { Val = JustificationValues.Center },
            new AdjustRightIndent { Val = false },
            new SnapToGrid() { Val = false },
            new SpacingBetweenLines { Before = "60", After = "60" }
            ), new Run(element));
    }

    public void Done()
    {
        throw new System.NotImplementedException();
    }
}
