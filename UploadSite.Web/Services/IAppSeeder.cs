namespace UploadSite.Web.Services;

public interface IAppSeeder
{
    Task SeedAsync(CancellationToken cancellationToken);
}
