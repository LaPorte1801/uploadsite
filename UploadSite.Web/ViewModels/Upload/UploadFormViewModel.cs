using System.ComponentModel.DataAnnotations;

namespace UploadSite.Web.ViewModels.Upload;

public sealed class UploadFormViewModel : IValidatableObject
{
    public List<IFormFile> AudioFiles { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AudioFiles.Count == 0)
        {
            yield return new ValidationResult(
                "Select at least one MP3, M4A, or FLAC file before uploading.",
                [nameof(AudioFiles)]);
        }
    }
}
