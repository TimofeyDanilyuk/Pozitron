using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pozitron.Api.Data;
using Pozitron.Api.Entitites;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Pozitron.Api.Hubs;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ChatController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IHubContext<ChatHub> _hub;

    public ChatController(AppDbContext context, IHubContext<ChatHub> hub)
    {
        _context = context;
        _hub = hub;
    }

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
                UnreadCount = 0,
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
                UnreadCount = cm.UnreadCount,
                LastMessage = cm.Chat.Messages
                    .OrderByDescending(m => m.SentAt)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                IsContact = _context.UserContacts
                    .Any(uc => uc.UserId == userId && uc.ContactId == cm.Chat.Members
                        .Where(m => m.UserId != userId)
                        .Select(m => m.UserId)
                        .FirstOrDefault())
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
                AttachmentUrl = m.AttachmentUrl,
                Type = m.Type.ToString(),
                PackId = m.Type == MessageType.Sticker
                    ? _context.Stickers
                        .Where(s => s.Id.ToString() == m.Content)
                        .Select(s => (Guid?)s.PackId)
                        .FirstOrDefault()
                    : null,
                SentAt = m.SentAt,
                UserId = m.UserId,
                Username = m.User!.Username,
                AvatarUrl = m.User.AvatarUrl,
                EmojiPrefix = m.User.EmojiPrefix,
                IsRead = m.IsRead
            })
            .ToListAsync();

        return Ok(messages.OrderBy(m => m.SentAt));
    }

    // Создать или открыть DM с пользователем
    [HttpPost("dm/{targetUserId}")]
    public async Task<IActionResult> GetOrCreateDm(Guid targetUserId)
    {
        var userId = CurrentUserId;

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

        // Создаём новый 
        var chat = new Chat { Id = Guid.NewGuid(), Type = ChatType.Direct };
        chat.Members.Add(new ChatMember { ChatId = chat.Id, UserId = userId });
        chat.Members.Add(new ChatMember { ChatId = chat.Id, UserId = targetUserId });

        _context.Chats.Add(chat);
        await _context.SaveChangesAsync();

        // Получаем данные обоих пользователей для уведомления
        var currentUser = await _context.Users.FindAsync(userId);
        var targetUser = await _context.Users.FindAsync(targetUserId);

        // Уведомляем второго пользователя что у него новый DM
        var targetUserIdStr = targetUserId.ToString();
        if (ChatHub.OnlineUsers.TryGetValue(targetUserIdStr, out var targetConnectionId))
        {
            await _hub.Clients.Client(targetConnectionId).SendAsync("NewDmChat", new
            {
                id = chat.Id,
                type = ChatType.Direct,
                name = currentUser?.Username,
                avatarUrl = currentUser?.AvatarUrl,
                lastMessage = (string?)null
            });
        }

        // Уведомляем и самого инициатора (на случай нескольких вкладок)
        var currentUserIdStr = userId.ToString();
        if (ChatHub.OnlineUsers.TryGetValue(currentUserIdStr, out var currentConnectionId))
        {
            await _hub.Clients.Client(currentConnectionId).SendAsync("NewDmChat", new
            {
                id = chat.Id,
                type = ChatType.Direct,
                name = targetUser?.Username,
                avatarUrl = targetUser?.AvatarUrl,
                lastMessage = (string?)null
            });
        }

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

    [HttpPost("{chatId}/read")]
    public async Task<IActionResult> MarkAsRead(Guid chatId)
    {
        var userId = CurrentUserId;

        var member = await _context.ChatMembers
            .FirstOrDefaultAsync(cm => cm.ChatId == chatId && cm.UserId == userId);

        if (member != null)
            member.UnreadCount = 0;

        var unreadMessages = await _context.Messages
            .Where(m => m.ChatId == chatId && m.UserId != userId && !m.IsRead)
            .ToListAsync();

        foreach (var msg in unreadMessages)
            msg.IsRead = true;

        await _context.SaveChangesAsync();

        // Уведомляем отправителей если они онлайн
        var senderIds = unreadMessages.Select(m => m.UserId.ToString()).Distinct().ToList();
        foreach (var senderId in senderIds)
        {
            if (ChatHub.OnlineUsers.TryGetValue(senderId, out var connId))
                await _hub.Clients.Client(connId).SendAsync("MessagesRead", new { chatId = chatId.ToString() });
        }

        // Для общего чата — уведомляем всех онлайн пользователей в группе
        var chat = await _context.Chats.FindAsync(chatId);
        if (chat?.Type == ChatType.General)
        {
            await _hub.Clients.Group(chatId.ToString()).SendAsync("MessagesRead", new { chatId = chatId.ToString() });
        }

        return Ok();
    }
}

public record ChatDto
{
    public Guid Id { get; set; }
    public ChatType Type { get; set; }
    public string? Name { get; set; }
    public string? AvatarUrl { get; set; }
    public int UnreadCount { get; set; }
    public string? LastMessage { get; set; }
    public bool IsContact { get; set; }
}

public record MessageDto
{
    public Guid Id { get; set; }
    public string? Content { get; set; }
    public string? AttachmentUrl { get; set; }
    public string? Type { get; set; }
    public Guid? PackId { get; set; }
    public DateTime SentAt { get; set; }
    public Guid UserId { get; set; }
    public string? Username { get; set; }
    public string? AvatarUrl { get; set; }
    public string? EmojiPrefix { get; set; }
    public bool IsRead { get; set; }
}