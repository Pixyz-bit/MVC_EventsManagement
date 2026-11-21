using SkiaSharp;

namespace MVC_QCUEventsManagement.Services;

public sealed class EventBannerStore(IWebHostEnvironment environment, IConfiguration configuration)
{
    public const long MaxBytes = 5 * 1024 * 1024;
    private string Root => Path.GetFullPath(configuration["EventImages:RootPath"]
        ?? Path.Combine(environment.ContentRootPath, "PrivateData", "EventBanners"));

    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (file.Length is <= 0 or > MaxBytes || extension is not (".png" or ".jpg" or ".jpeg" or ".webp"))
            throw new InvalidDataException("Choose a PNG, JPG or WEBP image up to 5 MB.");
        await using var input = file.OpenReadStream();
        using var bounded = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
        {
            if (bounded.Length + read > MaxBytes) throw new InvalidDataException("The image must be 5 MB or smaller.");
            await bounded.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        bounded.Position = 0;
        using var codec = SKCodec.Create(bounded);
        if (codec is null || codec.EncodedFormat is not (SKEncodedImageFormat.Png or SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Webp)
            || codec.Info.Width <= 0 || codec.Info.Height <= 0 || codec.Info.Width > 8192 || codec.Info.Height > 8192
            || (long)codec.Info.Width * codec.Info.Height > 16_000_000)
            throw new InvalidDataException("Choose a valid image, at most 8192 pixels per side and 16 megapixels.");
        using var bitmap = new SKBitmap(new SKImageInfo(codec.Info.Width, codec.Info.Height));
        if (codec.GetPixels(bitmap.Info, bitmap.GetPixels()) != SKCodecResult.Success)
            throw new InvalidDataException("The image could not be read. Choose another file.");
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
        var name = $"{Guid.NewGuid():N}.png";
        Directory.CreateDirectory(Root);
        var path = Resolve(name);
        try
        {
            await using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await encoded.AsStream().CopyToAsync(output, cancellationToken);
        }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
        return name;
    }

    public string Resolve(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "\\A[a-f0-9]{32}\\.png\\z"))
            throw new InvalidDataException("Invalid banner reference.");
        return Path.Combine(Root, name);
    }

    public void Delete(string name) => File.Delete(Resolve(name));
}
