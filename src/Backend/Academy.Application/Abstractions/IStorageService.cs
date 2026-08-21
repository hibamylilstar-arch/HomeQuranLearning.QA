namespace Academy.Application.Abstractions;

public interface IStorageService
{
    Task UploadAsync(
        string bucketName,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default);
}