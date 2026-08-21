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
}