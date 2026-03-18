using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pozitron.Api.Data;

[Authorize]
[ApiController]
[Route("api/files")]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public FilesController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    [HttpGet("{chatId}/{filename}")]
    public async Task<IActionResult> GetFile(Guid chatId, string filename)
    {
        var userId = CurrentUserId;

        // Проверяем что юзер является участником чата
        var isMember = await _context.ChatMembers
            .AnyAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

        if (!isMember)
            return Forbid();

        // Защита от path traversal атак
        var safeFilename = Path.GetFileName(filename);
        if (string.IsNullOrEmpty(safeFilename) || safeFilename != filename)
            return BadRequest();

        var rootPath = Environment.GetEnvironmentVariable("UPLOAD_PATH")
            ?? Path.Combine(_environment.WebRootPath ?? Directory.GetCurrentDirectory(), "uploads");

        var filePath = Path.Combine(rootPath, "attachments", chatId.ToString(), safeFilename);

        if (!System.IO.File.Exists(filePath))
            return NotFound();

        var contentType = GetContentType(safeFilename);
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        // enableRangeProcessing: true — нужно для перемотки видео
        return File(stream, contentType, enableRangeProcessing: true);
    }

    private static string GetContentType(string filename)
    {
        var ext = Path.GetExtension(filename).ToLower();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".webp"           => "image/webp",
            ".mp4"            => "video/mp4",
            ".webm"           => "video/webm",
            _                 => "application/octet-stream"
        };
    }
}