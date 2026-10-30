namespace school_management_system.Service;
    public class FileStorageService(IWebHostEnvironment env)
    {
    public const long MaxBytes = 2 * 1024 * 1024;

        public async Task<string?>  SavePhotoAsync(IFormFile? file, string folder)
    {
        if (file == null || file.Length == 0) return null;
        if (file.Length > MaxBytes) throw new DomainException("Photos must be 2 MB or smaller.");

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".jpg" or ".jpeg" or ".png" or ".webp")) throw new DomainException("Photos must be JPG, PNG or WebP files.");

        await using var input = file.OpenReadStream();
        var head = new byte[12];
        var read = await input.ReadAsync(head.AsMemory(0, head.Length));
        var valid = ext switch
        {
            ".png" => read >= 8 && head[0] == 0x89 && head[1] == 0x50 && head[2] == 0x4E && head[3] == 0x47,
            ".jpg" or ".jpeg" => read >= 3 && head[0] == 0xFF && head[1] == 0xD8 && head[2] == 0xFF,
            ".webp" => read >= 12 && head[0] == 'R' && head[1] == 'I' && head[2] == 'F' && head[3] == 'F' && head[8] == 'W' && head[9] == 'E' && head[10] == 'B' && head[11] == 'P',
            _ => false
        };
        if (!valid) throw new DomainException("That file is not a valid image.");
        input.Position = 0;

        var dir = Path.Combine(env.WebRootPath, "uploads", folder);
        Directory.CreateDirectory(dir);
        var name = $"{Guid.NewGuid():N}{ext}";
        await using var output = File.Create(Path.Combine(dir, name));
        await input.CopyToAsync(output);
        return $"/uploads/{folder}/{name}";
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;
        var root = Path.GetFullPath(Path.Combine(env.WebRootPath, "uploads"));
        var full = Path.GetFullPath(Path.Combine(env.WebRootPath, relativePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase) && File.Exists(full)) File.Delete(full);
    }



}

