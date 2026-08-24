namespace Academy.Application.Abstractions;

public interface IStorageService
{
    Task UploadAsync(
        string bucketName,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);
    Task<string> GetPresignedUrlAsync(
        string bucketName,
        string objectKey,
        TimeSpan expiry,
        CancellationToken cancellationToken = default);
}
