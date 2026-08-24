namespace ConvertPdf.Utils;

public class AllowedExtensions
{
    private string[] allowedExtensions =
    [
        ".doc",
        ".docx",
        ".odt",
        ".rtf",
        ".txt",
        ".xls",
        ".xlsx",
        ".ods",
        ".csv",
        ".ppt",
        ".pptx",
        ".odp"
    ];
    public bool Contains(string extension)
    {
        if (extension == null)
        {
            return false;
        }
        return allowedExtensions.Contains(extension);
    }

    public bool NoContains(string extension)
    {
        return Contains(extension) == false;
    }
}
