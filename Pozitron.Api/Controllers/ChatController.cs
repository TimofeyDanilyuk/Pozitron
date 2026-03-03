using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pozitron.Api.Data;
using Pozitron.Api.Entitites;
using Microsoft.EntityFrameworkCore;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _context;

    public ChatController(AppDbContext context) => _context = context;

    private Guid CurrentUserId => 
        Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);

    // Получить все чаты текущего пользователя + общий канал
    [HttpGet]
    public async Task<IActionResult> GetChats()
    {
        var userId = CurrentUserId;

        // Общий канал
        var general = await _context.Chats
            .Where(c => c.Type == ChatType.General)
            .Select(c => new ChatDto
            {
                Id = c.Id,
                Type = c.Type,
                Name = c.Name,
                LastMessage = c.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault()
            })
            .ToListAsync();

        // DM чаты пользователя
        var dms = await _context.ChatMembers
            .Where(cm => cm.UserId == userId)
            .Include(cm => cm.Chat)
                .ThenInclude(c => c.Members)
                    .ThenInclude(m => m.User)
            .Select(cm => new ChatDto
            {
                Id = cm.Chat!.Id,
                Type = cm.Chat.Type,
                Name = cm.Chat.Members
                    .Where(m => m.UserId != userId)
                    .Select(m => m.User!.Username)
                    .FirstOrDefault(),
                AvatarUrl = cm.Chat.Members
                    .Where(m => m.UserId != userId)
                    .Select(m => m.User!.AvatarUrl)
                    .FirstOrDefault(),
                LastMessage = cm.Chat.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault()
            })
            .ToListAsync();

        return Ok(general.Concat(dms));
    }

    // История сообщений чата
    [HttpGet("{chatId}/messages")]
    public async Task<IActionResult> GetMessages(Guid chatId, [FromQuery] int skip = 0, [FromQuery] int take = 50)
    {
        var messages = await _context.Messages
            .Where(m => m.ChatId == chatId)
            .OrderByDescending(m => m.SentAt)
            .Skip(skip)
            .Take(take)
            .Include(m => m.User)
            .Select(m => new MessageDto
            {
                Id = m.Id,
                Content = m.Content,
                SentAt = m.SentAt,
                UserId = m.UserId,
                Username = m.User!.Username,
                AvatarUrl = m.User.AvatarUrl,
                EmojiPrefix = m.User.EmojiPrefix
            })
            .ToListAsync();

        return Ok(messages.OrderBy(m => m.SentAt));
    }

    // Создать или открыть DM с пользователем
    [HttpPost("dm/{targetUserId}")]
    public async Task<IActionResult> GetOrCreateDm(Guid targetUserId)
    {
        var userId = CurrentUserId;

        // Ищем существующий DM между двумя пользователями
        var existing = await _context.ChatMembers
            .Where(cm => cm.UserId == userId)
            .Select(cm => cm.ChatId)
            .ToListAsync();

        var targetChats = await _context.ChatMembers
            .Where(cm => cm.UserId == targetUserId && existing.Contains(cm.ChatId))
            .Include(cm => cm.Chat)
            .Where(cm => cm.Chat!.Type == ChatType.Direct)
            .Select(cm => cm.Chat!)
            .FirstOrDefaultAsync();

        if (targetChats != null)
            return Ok(new { id = targetChats.Id });

        // Создаём новый DM
        var chat = new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct };
        chat.Members.Add(new ChatMember { ChatId = chat.Id, UserId = userId });
        chat.Members.Add(new ChatMember { ChatId = chat.Id, UserId = targetUserId });

        _context.Chats.Add(chat);
        await _context.SaveChangesAsync();

        return Ok(new { id = chat.Id });
    }

    // Список всех пользователей для поиска
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] string? search)
    {
        var userId = CurrentUserId;
        var query = _context.Users.Where(u => u.Id != userId);
        
        if (!string.IsNullOrEmpty(search))
            query = query.Where(u => u.Username.Contains(search));

        var users = await query
            .Select(u => new { u.Id, u.Username, u.AvatarUrl, u.EmojiPrefix })
            .ToListAsync();

        return Ok(users);
    }
}

public record ChatDto
{
    public Guid Id { get; set; }
    public ChatType Type { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public string? LastMessage { get; set; }
}

public record MessageDto
{
    public Guid Id { get; set; }
    public string? Content { get; set; }
    public DateTime SentAt { get; set; }
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? AvatarUrl { get; set; }
    public string? EmojiPrefix { get; set; }
}