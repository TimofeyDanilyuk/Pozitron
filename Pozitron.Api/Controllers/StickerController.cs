using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pozitron.Api.Data;
using Pozitron.Api.Entitites;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace Pozitron.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class StickerController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;

    public StickerController(AppDbContext context, IWebHostEnvironment environment)
    {
        _context = context;
        _environment = environment;
    }

    private Guid CurrentUserId =>
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    private string GetUploadRoot() =>
        Environment.GetEnvironmentVariable("UPLOAD_PATH")
        ?? Path.Combine(_environment.WebRootPath ?? Directory.GetCurrentDirectory(), "uploads");

    private string GetBaseUrl() =>
        $"{(Request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? Request.Scheme)}://{Request.Host}";

    [HttpGet("my")]
    public async Task<IActionResult> GetMyPacks()
    {
        var userId = CurrentUserId;
        var packs = await _context.UserStickerPacks
            .Where(usp => usp.UserId == userId)
            .Include(usp => usp.Pack)
                .ThenInclude(p => p!.Stickers)
            .Select(usp => new
            {
                usp.Pack!.Id,
                usp.Pack.Name,
                usp.Pack.CoverUrl,
                createdByMe = usp.Pack.CreatedByUserId == userId,
                stickers = usp.Pack.Stickers
                    .OrderBy(s => s.Order)
                    .Select(s => new { s.Id, s.Url })
            })
            .ToListAsync();
        return Ok(packs);
    }

    [HttpGet("{packId}")]
    public async Task<IActionResult> GetPack(Guid packId)
    {
        var userId = CurrentUserId;
        var pack = await _context.StickerPacks
            .Include(p => p.Stickers)
            .Include(p => p.CreatedByUser)
            .Where(p => p.Id == packId)
            .Select(p => new
            {
                p.Id,
                p.Name,
                p.CoverUrl,
                createdBy = p.CreatedByUser!.Username,
                isAdded = p.UserPacks.Any(usp => usp.UserId == userId),
                stickers = p.Stickers
                    .OrderBy(s => s.Order)
                    .Select(s => new { s.Id, s.Url })
            })
            .FirstOrDefaultAsync();

        if (pack == null) return NotFound();
        return Ok(pack);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePack([FromBody] CreatePackRequest request)
    {
        var userId = CurrentUserId;
        var pack = new StickerPack
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            CreatedByUserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.StickerPacks.Add(pack);
        _context.UserStickerPacks.Add(new UserStickerPack
        {
            UserId = userId,
            PackId = pack.Id
        });

        await _context.SaveChangesAsync();
        return Ok(new { pack.Id, pack.Name });
    }

    [HttpPost("{packId}/stickers")]
    public async Task<IActionResult> AddSticker(Guid packId, IFormFile file)
    {
        var userId = CurrentUserId;
        var pack = await _context.StickerPacks.FindAsync(packId);

        if (pack == null) return NotFound();
        if (pack.CreatedByUserId != userId) return Forbid();

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/gif", "image/webp" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest("Разрешены только png, jpg, gif, webp");

        var maxSize = file.ContentType.ToLower() == "image/gif" ? 2 * 1024 * 1024 : 5 * 1024 * 1024;
        if (file.Length > maxSize)
            return BadRequest(file.ContentType.ToLower() == "image/gif"
                ? "GIF слишком большой (макс. 2MB)"
                : "Файл слишком большой (макс. 5MB)");

        var rootPath = GetUploadRoot();
        var folderPath = Path.Combine(rootPath, "stickers", packId.ToString());
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        var isGif = file.ContentType.ToLower() == "image/gif";
        var extension = isGif ? ".gif" : ".png";
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(folderPath, fileName);

        if (isGif)
        {
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
        }
        else
        {
            using var inputStream = file.OpenReadStream();
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(inputStream);

            if (image.Width > 512 || image.Height > 512)
            {
                image.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(512, 512),
                    Mode = ResizeMode.Max
                }));
            }

            await image.SaveAsPngAsync(filePath);
        }

        var url = $"{GetBaseUrl()}/uploads/stickers/{packId}/{fileName}";

        var order = await _context.Stickers.CountAsync(s => s.PackId == packId);
        var sticker = new Sticker
        {
            Id = Guid.NewGuid(),
            PackId = packId,
            Url = url,
            Order = order
        };

        _context.Stickers.Add(sticker);

        if (pack.CoverUrl == null)
            pack.CoverUrl = url;

        await _context.SaveChangesAsync();
        return Ok(new { sticker.Id, sticker.Url });
    }

    [HttpDelete("{packId}/stickers/{stickerId}")]
    public async Task<IActionResult> DeleteSticker(Guid packId, Guid stickerId)
    {
        var userId = CurrentUserId;
        var pack = await _context.StickerPacks.FindAsync(packId);
        if (pack == null || pack.CreatedByUserId != userId) return Forbid();

        var sticker = await _context.Stickers.FindAsync(stickerId);
        if (sticker == null) return NotFound();

        _context.Stickers.Remove(sticker);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("{packId}/add")]
    public async Task<IActionResult> AddPack(Guid packId)
    {
        var userId = CurrentUserId;
        var exists = await _context.UserStickerPacks
            .AnyAsync(usp => usp.UserId == userId && usp.PackId == packId);

        if (exists) return BadRequest("Пак уже добавлен");

        _context.UserStickerPacks.Add(new UserStickerPack
        {
            UserId = userId,
            PackId = packId
        });

        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpDelete("{packId}/remove")]
    public async Task<IActionResult> RemovePack(Guid packId)
    {
        var userId = CurrentUserId;
        var usp = await _context.UserStickerPacks
            .FirstOrDefaultAsync(usp => usp.UserId == userId && usp.PackId == packId);

        if (usp == null) return NotFound();

        _context.UserStickerPacks.Remove(usp);
        await _context.SaveChangesAsync();
        return Ok();
    }
}

public record CreatePackRequest(string Name);