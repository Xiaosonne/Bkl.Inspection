using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using System.Collections.Generic;
using System.Linq;

public class CreateEmptyParagraph : ICreateParagraph
{
    private int _count;

    public OpenXmlElement AddParagraph()
    {
    
        return new Paragraph();
    }

    public CreateEmptyParagraph()
    { 
    }
    public void Done()
    {
        throw new System.NotImplementedException();
    }
}
public class CreateBookmarkEnd : ICreateParagraph
{
    public OpenXmlElement AddParagraph()
    {
        return new BookmarkEnd() { Id = "0" };
    }

    public void Done()
    {
    }
}