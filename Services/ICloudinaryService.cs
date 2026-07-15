namespace Bus_ticket.Services;

public interface ICloudinaryService
{
    Task<(string Url, string PublicId)> UploadImageAsync(Stream stream, string fileName);
    Task DeleteImageAsync(string? publicId);
}
