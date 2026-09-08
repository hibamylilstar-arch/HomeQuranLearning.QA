using Academy.Application.Abstractions;
using Minio;
using Minio.DataModel.Args;

namespace Academy.Infrastructure.Storage;

public sealed class MinioStorageService : IStorageService
{
    private readonly IMinioClient _minioClient;

    public MinioStorageService(IMinioClient minioClient)
    {
        _minioClient = minioClient;
    }

    public async Task UploadAsync(
        string bucketName,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        bool found = await _minioClient.BucketExistsAsync(
            new BucketExistsArgs().WithBucket(bucketName),
            cancellationToken);

        if (!found)
        {
            await _minioClient.MakeBucketAsync(
                new MakeBucketArgs().WithBucket(bucketName),
                cancellationToken);
        }

        await _minioClient.PutObjectAsync(
            new PutObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithStreamData(content)
                .WithObjectSize(content.Length)
                .WithContentType(contentType),
            cancellationToken);
    }

    public async Task DownloadAsync(
        string bucketName,
        string objectKey,
        Stream destination,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(
            destination);

        var args =
            new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey)
                .WithCallbackStream(
                    (source, callbackCancellationToken) =>
                        source.CopyToAsync(
                            destination,
                            callbackCancellationToken));

        await _minioClient.GetObjectAsync(
            args,
            cancellationToken: cancellationToken);
    }

    public async Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        await _minioClient.RemoveObjectAsync(
            new RemoveObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectKey),
            cancellationToken);
    }
    public async Task<string> GetPresignedUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default)
    {
        var args = new PresignedGetObjectArgs()
            .WithBucket(bucketName)
            .WithObject(objectKey)
            .WithExpiry((int)expiry.TotalSeconds);

        return await _minioClient.PresignedGetObjectAsync(args);
    }
}
