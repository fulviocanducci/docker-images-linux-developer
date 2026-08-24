namespace ConvertPdf.Extensions;

public static class WebExtensions
{
    public static string GetFileExtension(this IFormFile file)
    {
        if (file == null)
        {
            return string.Empty;
        }
        return Path.GetExtension(file.FileName).ToLowerInvariant();
    }
}
