using TagLib;
using System.Text.RegularExpressions;

namespace UploadSite.Web.Services;

public sealed class AudioMetadataService : IAudioMetadataService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3",
        ".m4a",
        ".flac"
    };

    public AudioValidationResult Validate(string filePath, string originalFileName)
    {
        var result = new AudioValidationResult();
        var extension = Path.GetExtension(filePath);

        if (!AllowedExtensions.Contains(extension))
        {
            result.Errors.Add("Unsupported file type. Only MP3, M4A, and FLAC are allowed.");
            return result;
        }

        using var file = TagLib.File.Create(filePath);
        var tag = file.Tag;
        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);

        result.Artist = tag.FirstPerformer ?? string.Empty;
        result.AlbumArtist = tag.FirstAlbumArtist ?? string.Empty;
        result.Album = tag.Album ?? string.Empty;
        result.Title = tag.Title ?? string.Empty;
        result.Genre = tag.FirstGenre;
        result.Year = (int)tag.Year;
        result.TrackNumber = (int)tag.Track;
        result.HasEmbeddedCover = tag.Pictures?.Length > 0;

        if (string.IsNullOrWhiteSpace(result.Artist))
        {
            result.Artist = "Unknown Artist";
            result.HasFallbackArtist = true;
            result.Warnings.Add("Artist tag was missing and was auto-filled as 'Unknown Artist'.");
        }

        if (string.IsNullOrWhiteSpace(result.AlbumArtist))
        {
            result.AlbumArtist = result.Artist;
            result.HasFallbackAlbumArtist = true;
            result.Warnings.Add("Album Artist tag was missing and was auto-filled.");
        }

        if (string.IsNullOrWhiteSpace(result.Album))
        {
            result.Album = "Unknown Album";
            result.HasFallbackAlbum = true;
            result.Warnings.Add("Album tag was missing and was auto-filled as 'Unknown Album'.");
        }

        if (string.IsNullOrWhiteSpace(result.Title))
        {
            result.Title = BuildTitleFromFileName(fileNameWithoutExtension);
            result.HasFallbackTitle = true;
            result.Warnings.Add("Title tag was missing and was auto-filled from the file name.");
        }

        if (result.Year <= 0)
        {
            result.HasMissingYear = true;
            result.Warnings.Add("Year tag is missing and still needs manual review.");
        }

        if (result.TrackNumber <= 0)
        {
            var inferredTrackNumber = TryInferTrackNumber(fileNameWithoutExtension);
            if (inferredTrackNumber > 0)
            {
                result.TrackNumber = inferredTrackNumber;
                result.HasFallbackTrackNumber = true;
                result.Warnings.Add("Track Number tag was missing and was inferred from the file name.");
            }
            else
            {
                result.Warnings.Add("Track Number tag is missing and still needs manual review.");
            }
        }

        if (!result.HasEmbeddedCover)
        {
            result.Errors.Add("Embedded cover art is required.");
        }

        return result;
    }

    private static string BuildTitleFromFileName(string fileNameWithoutExtension)
    {
        var cleaned = Regex.Replace(fileNameWithoutExtension, @"^\s*\d+\s*[-_. ]*\s*", string.Empty);
        cleaned = cleaned.Replace('_', ' ').Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? fileNameWithoutExtension.Trim() : cleaned;
    }

    private static int TryInferTrackNumber(string fileNameWithoutExtension)
    {
        var match = Regex.Match(fileNameWithoutExtension, @"^\s*(\d{1,3})\b");
        return match.Success && int.TryParse(match.Groups[1].Value, out var number) ? number : 0;
    }
}
