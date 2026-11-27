using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;

namespace GoatFarm.Infrastructure.Services;

public sealed record OffsiteBackupObject(string Name, long SizeBytes, DateTime LastModifiedUtc);

/// <summary>Copy of each backup kept off the app server (the container disk is wiped on every redeploy).</summary>
public interface IOffsiteBackupStore
{
    bool IsConfigured { get; }
    Task UploadAsync(string name, byte[] content, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OffsiteBackupObject>> ListAsync(CancellationToken cancellationToken = default);
    Task<byte[]?> DownloadAsync(string name, CancellationToken cancellationToken = default);
    Task<int> DeleteOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default);
}

public sealed class NullOffsiteBackupStore : IOffsiteBackupStore
{
    public bool IsConfigured => false;
    public Task UploadAsync(string name, byte[] content, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<IReadOnlyList<OffsiteBackupObject>> ListAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OffsiteBackupObject>>([]);
    public Task<byte[]?> DownloadAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<byte[]?>(null);
    public Task<int> DeleteOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default) => Task.FromResult(0);
}

/// <summary>
/// S3-compatible bucket (AWS S3, Cloudflare R2, Backblaze B2). Settings: BACKUP_BUCKET, BACKUP_KEY, BACKUP_SECRET and
/// BACKUP_ENDPOINT (leave the endpoint empty for AWS; set BACKUP_REGION if the bucket is not in us-east-1).
/// </summary>
public sealed class S3OffsiteBackupStore : IOffsiteBackupStore, IDisposable
{
    private const string Prefix = "goat-records-";
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;

    public S3OffsiteBackupStore(IAmazonS3 s3, string bucket)
    {
        _s3 = s3;
        _bucket = bucket;
    }

    public bool IsConfigured => true;

    public static IOffsiteBackupStore FromConfiguration(IConfiguration configuration)
    {
        var bucket = configuration["BACKUP_BUCKET"];
        var key = configuration["BACKUP_KEY"];
        var secret = configuration["BACKUP_SECRET"];
        if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(secret))
            return new NullOffsiteBackupStore();

        var config = new AmazonS3Config { ForcePathStyle = true };
        var endpoint = configuration["BACKUP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            config.ServiceURL = endpoint;
            config.AuthenticationRegion = configuration["BACKUP_REGION"] is { Length: > 0 } r ? r : "auto";
        }
        else
        {
            config.RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(
                configuration["BACKUP_REGION"] is { Length: > 0 } region ? region : "us-east-1");
        }

        return new S3OffsiteBackupStore(new AmazonS3Client(key, secret, config), bucket);
    }

    public async Task UploadAsync(string name, byte[] content, CancellationToken cancellationToken = default)
    {
        using var body = new MemoryStream(content);
        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = name,
            InputStream = body,
            ContentType = "application/json",
            DisablePayloadSigning = true
        }, cancellationToken);
    }

    public async Task<IReadOnlyList<OffsiteBackupObject>> ListAsync(CancellationToken cancellationToken = default)
    {
        var result = new List<OffsiteBackupObject>();
        var request = new ListObjectsV2Request { BucketName = _bucket, Prefix = Prefix };
        ListObjectsV2Response response;
        do
        {
            response = await _s3.ListObjectsV2Async(request, cancellationToken);
            foreach (var o in response.S3Objects ?? [])
                result.Add(new OffsiteBackupObject(o.Key, o.Size ?? 0, (o.LastModified ?? DateTime.UtcNow).ToUniversalTime()));
            request.ContinuationToken = response.NextContinuationToken;
        } while (response.IsTruncated == true);
        return result;
    }

    public async Task<byte[]?> DownloadAsync(string name, CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _s3.GetObjectAsync(_bucket, name, cancellationToken);
            using var buffer = new MemoryStream();
            await response.ResponseStream.CopyToAsync(buffer, cancellationToken);
            return buffer.ToArray();
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<int> DeleteOlderThanAsync(TimeSpan age, CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow - age;
        var old = (await ListAsync(cancellationToken))
            .Where(o => (BackupFileNames.TryParseSavedUtc(o.Name) ?? o.LastModifiedUtc) < cutoff)
            .ToList();
        foreach (var batch in old.Chunk(1000))
        {
            await _s3.DeleteObjectsAsync(new DeleteObjectsRequest
            {
                BucketName = _bucket,
                Objects = batch.Select(o => new KeyVersion { Key = o.Name }).ToList()
            }, cancellationToken);
        }
        return old.Count;
    }

    public void Dispose() => _s3.Dispose();
}
