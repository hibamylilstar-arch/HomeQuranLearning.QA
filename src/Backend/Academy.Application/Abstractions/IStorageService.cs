namespace Academy.Application.Abstractions;

public interface IStorageService
{
    Task UploadAsync(
        string bucketName,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);

    Task DownloadAsync(
        string bucketName,
        string objectKey,
        Stream destination,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException(
            "Storage download is not supported by this implementation.");

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
