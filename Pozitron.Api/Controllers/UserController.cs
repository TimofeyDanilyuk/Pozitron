using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pozitron.Api.Data;
using Pozitron.Api.Entitites;
using Pozitron.Api.Hubs;

namespace Pozitron.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly IHubContext<ChatHub> _hub;

        public UserController(AppDbContext context, IWebHostEnvironment environment, IHubContext<ChatHub> hub)
        {
            _context = context;
            _hub = hub;
            _environment = environment;
        }

        // Получить список контактов
        [HttpGet("contacts")]
        public async Task<IActionResult> GetContacts()
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var contacts = await _context.UserContacts
                .Where(uc => uc.UserId == userId)
                .Include(uc => uc.Contact)
                .Select(uc => new {
                    uc.Contact!.Id,
                    uc.Contact.Username,
                    uc.Contact.AvatarUrl,
                    uc.Contact.EmojiPrefix,
                    uc.AddedAt
                })
                .ToListAsync();
            return Ok(contacts);
        }

        // Добавить контакт
        [HttpPost("contacts/{contactId}")]
        public async Task<IActionResult> AddContact(Guid contactId)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            if (userId == contactId) return BadRequest("Нельзя добавить себя");

            var exists = await _context.UserContacts
                .AnyAsync(uc => uc.UserId == userId && uc.ContactId == contactId);
            if (exists) return BadRequest("Уже в контактах");

            _context.UserContacts.Add(new UserContact { UserId = userId, ContactId = contactId });

            // Создаём DM чат если нет
            var myChats = await _context.ChatMembers
                .Where(cm => cm.UserId == userId)
                .Select(cm => cm.ChatId)
                .ToListAsync();

            var dmExists = await _context.ChatMembers
                .AnyAsync(cm => cm.UserId == contactId
                    && myChats.Contains(cm.ChatId)
                    && cm.Chat!.Type == ChatType.Direct);

            Guid chatId;
            if (!dmExists)
            {
                var newChat = new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct };
                newChat.Members.Add(new ChatMember { ChatId = newChat.Id, UserId = userId });
                newChat.Members.Add(new ChatMember { ChatId = newChat.Id, UserId = contactId });
                _context.Chats.Add(newChat);
                chatId = newChat.Id;
            }
            else
            {
                chatId = await _context.ChatMembers
                    .Where(cm => cm.UserId == contactId && myChats.Contains(cm.ChatId))
                    .Select(cm => cm.ChatId)
                    .FirstAsync();
            }

            await _context.SaveChangesAsync();

            var currentUser = await _context.Users.FindAsync(userId);
            var contact = await _context.Users.FindAsync(contactId);

            // Уведомляем себя о новом чате
            if (ChatHub.OnlineUsers.TryGetValue(userId.ToString(), out var myConn))
            {
                await _hub.Clients.Client(myConn).SendAsync("NewDmChat", new {
                    id = chatId,
                    type = ChatType.Direct,
                    name = contact?.Username,
                    avatarUrl = contact?.AvatarUrl,
                    isContact = true,
                    lastMessage = (string?)null
                });
            }

            return Ok(new { chatId });
        }

        // Удалить контакт
        [HttpDelete("contacts/{contactId}")]
        public async Task<IActionResult> RemoveContact(Guid contactId)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value!);
            var contact = await _context.UserContacts
                .FirstOrDefaultAsync(uc => uc.UserId == userId && uc.ContactId == contactId);
            
            if (contact == null) return NotFound();
            
            _context.UserContacts.Remove(contact);
            await _context.SaveChangesAsync();
            return Ok();
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

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
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