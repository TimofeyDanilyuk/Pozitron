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

    // Получить все паки текущего пользователя (добавленные)
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

    // Получить пак по id (для просмотра чужого пака)
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

    // Создать новый пак
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

        // Автоматически добавляем пак создателю
        _context.UserStickerPacks.Add(new UserStickerPack
        {
            UserId = userId,
            PackId = pack.Id
        });

        await _context.SaveChangesAsync();
        return Ok(new { pack.Id, pack.Name });
    }

    // Загрузить стикер в пак
    [HttpPost("{packId}/stickers")]
    public async Task<IActionResult> AddSticker(Guid packId, IFormFile file)
    {
        var userId = CurrentUserId;
        var pack = await _context.StickerPacks.FindAsync(packId);

        if (pack == null) return NotFound();
        if (pack.CreatedByUserId != userId) return Forbid();

        var allowedTypes = new[] { "image/png", "image/jpeg", "image/gif", "image/webp", "video/mp4" };
        if (!allowedTypes.Contains(file.ContentType.ToLower()))
            return BadRequest("Разрешены только png, jpg, gif, webp, mp4");

        if (file.Length > 50 * 1024 * 1024)
            return BadRequest("Файл слишком большой (макс. 50MB)");

        var rootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var folderPath = Path.Combine(rootPath, "uploads", "stickers", packId.ToString());
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        string fileName;
        string filePath;

        if (file.ContentType.ToLower() == "video/mp4")
        {
            // Сохраняем mp4 во временный файл
            var tempMp4 = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.mp4");
            using (var tempStream = new FileStream(tempMp4, FileMode.Create))
                await file.CopyToAsync(tempStream);

            fileName = $"{Guid.NewGuid()}.gif";
            filePath = Path.Combine(folderPath, fileName);

            // Конвертируем mp4 → gif через FFmpeg
            Xabe.FFmpeg.FFmpeg.SetExecutablesPath("/usr/bin");
            var conversion = await Xabe.FFmpeg.FFmpeg.Conversions.FromSnippet.ToGif(
                tempMp4, filePath,0,0);
            // Ограничиваем размер до 512x512 и fps до 15
            conversion.AddParameter("-vf \"scale=512:512:force_original_aspect_ratio=decrease,fps=15\"");
            await conversion.Start();

            System.IO.File.Delete(tempMp4);
        }
        else if (file.ContentType.ToLower() == "image/gif")
        {
            if (file.Length > 2 * 1024 * 1024)
                return BadRequest("GIF слишком большой (макс. 2MB)");

            fileName = $"{Guid.NewGuid()}.gif";
            filePath = Path.Combine(folderPath, fileName);
            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);
        }
        else
        {
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest("Файл слишком большой (макс. 5MB)");

            fileName = $"{Guid.NewGuid()}.png";
            filePath = Path.Combine(folderPath, fileName);

            using var inputStream = file.OpenReadStream();
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(inputStream);
            if (image.Width > 512 || image.Height > 512)
            {
                image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(512, 512),
                    Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max
                }));
            }
            await image.SaveAsPngAsync(filePath);
        }

        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var url = $"{baseUrl}/uploads/stickers/{packId}/{fileName}";

        var order = await _context.Stickers.CountAsync(s => s.PackId == packId);
        var sticker = new Sticker
        {
            Id = Guid.NewGuid(),
            PackId = packId,
            Url = url,
            Order = order
        };

        _context.Stickers.Add(sticker);
        if (pack.CoverUrl == null) pack.CoverUrl = url;
        await _context.SaveChangesAsync();

        return Ok(new { sticker.Id, sticker.Url });
    }

    // Удалить стикер из пака
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

    // Добавить чужой пак себе
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

    // Убрать пак
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