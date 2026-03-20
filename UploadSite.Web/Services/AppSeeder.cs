using Microsoft.EntityFrameworkCore;
using UploadSite.Web.Data;
using UploadSite.Web.Enums;
using UploadSite.Web.Models;

namespace UploadSite.Web.Services;

public sealed class AppSeeder(
    AppDbContext dbContext,
    IUserService userService,
    AppPaths appPaths,
    SeedOptions seedOptions) : IAppSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(appPaths.DatabasePath)!);
        Directory.CreateDirectory(appPaths.StagingRoot);
        Directory.CreateDirectory(appPaths.LibraryRoot);

        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        await EnsureBatchIdsAsync(cancellationToken);

        if (await dbContext.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        await userService.CreateUserAsync(
            seedOptions.AdminUserName,
            seedOptions.AdminPassword,
            UserRole.Admin,
            cancellationToken);
    }

    private async Task EnsureBatchIdsAsync(CancellationToken cancellationToken)
    {
        var columnExists = await dbContext.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS Value FROM pragma_table_info('ImportJobs') WHERE name = 'BatchId'")
            .SingleAsync(cancellationToken);

        if (columnExists == 0)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                "ALTER TABLE ImportJobs ADD COLUMN BatchId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'",
                cancellationToken);
        }

        var jobsWithoutBatch = await dbContext.ImportJobs
            .Where(x => x.BatchId == Guid.Empty)
            .ToListAsync(cancellationToken);

        jobsWithoutBatch = jobsWithoutBatch
            .OrderBy(x => x.UploadedAtUtc)
            .ToList();

        foreach (var group in jobsWithoutBatch
                     .GroupBy(x => x.Status is ImportStatus.NeedsReview or ImportStatus.ReadyToImport
                         ? $"{x.AlbumArtist}|{x.Album}|{x.Year}"
                         : x.Id.ToString()))
        {
            var batchId = Guid.NewGuid();
            foreach (var job in group)
            {
                job.BatchId = batchId;
            }
        }

        if (jobsWithoutBatch.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
