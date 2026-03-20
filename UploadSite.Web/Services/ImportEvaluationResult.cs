namespace UploadSite.Web.Services;

public sealed class ImportEvaluationResult
{
    public bool MetadataComplete { get; init; }
    public string ValidationSummary { get; init; } = string.Empty;
    public string ProposedRelativePath { get; init; } = string.Empty;
    public IReadOnlyList<DuplicateCandidate> DuplicateCandidates { get; init; } = [];
}

public sealed class DuplicateCandidate
{
    public string RelativePath { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
