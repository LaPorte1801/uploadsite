using System.Text;

namespace UploadSite.Web.Services;

public static class FileNameSanitizer
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    public static string Sanitize(string value)
    {
        var builder = new StringBuilder(value.Trim());

        foreach (var invalidChar in InvalidChars)
        {
            builder.Replace(invalidChar, '_');
        }

        return builder.ToString().Trim().TrimEnd('.');
    }
}
