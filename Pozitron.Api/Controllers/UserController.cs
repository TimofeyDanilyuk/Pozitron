using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pozitron.Api.Data;

namespace Pozitron.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public UserController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost("upload-avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            try 
            {
                if (file == null || file.Length == 0) return BadRequest("Файл не выбран");

                var rootPath = _environment.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var folderPath = Path.Combine(rootPath, "uploads", "avatars");

                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

                var extension = Path.GetExtension(file.FileName).ToLower();
                var fileName = $"{Guid.NewGuid()}{extension}";
                var filePath = Path.Combine(folderPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var avatarUrl = $"{baseUrl}/uploads/avatars/{fileName}";

                var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
                var user = await _context.Users.FindAsync(userId);
                
                if (user == null) return NotFound("Пользователь не найден");

                user.AvatarUrl = avatarUrl;
                await _context.SaveChangesAsync();

                return Ok(new { avatarUrl });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ОШИБКА ЗАГРУЗКИ: {ex.Message}");
                return StatusCode(500, $"Ошибка на сервере: {ex.Message}");
            }
        }

        [HttpPatch("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var user = await _context.Users.FindAsync(userId);

            if (user == null) return NotFound();

            if (!string.IsNullOrEmpty(request.DisplayName)) user.DisplayName = request.DisplayName;
            if (!string.IsNullOrEmpty(request.EmojiPrefix)) user.EmojiPrefix = request.EmojiPrefix;
            if (!string.IsNullOrEmpty(request.AvatarUrl)) user.AvatarUrl = request.AvatarUrl;

            await _context.SaveChangesAsync();
            return Ok(user);
        }

        [HttpPatch("username")]
        public async Task<IActionResult> ChangeUsername([FromBody] ChangeUsernameRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Username) || request.Username.Length < 3)
                return BadRequest("Ник должен быть не короче 3 символов");

            if (_context.Users.Any(u => u.Username == request.Username))
                return BadRequest("Этот ник уже занят");

            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return NotFound();

            user.Username = request.Username;
            user.DisplayName = request.Username;
            await _context.SaveChangesAsync();

            return Ok(new { user.Id, user.Username, user.EmojiPrefix, user.AvatarUrl });
        }
    }

    public record ChangeUsernameRequest(string Username);
    public record UpdateProfileRequest(string? DisplayName, string? EmojiPrefix, string? AvatarUrl);
}