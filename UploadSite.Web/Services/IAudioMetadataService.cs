namespace UploadSite.Web.Services;

public interface IAudioMetadataService
{
    AudioValidationResult Validate(string filePath, string originalFileName);
}
