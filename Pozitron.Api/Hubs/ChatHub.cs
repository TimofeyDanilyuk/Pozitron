using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Pozitron.Api.Data;
using Pozitron.Api.Entitites;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;

namespace Pozitron.Api.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly AppDbContext _context;
    public static readonly ConcurrentDictionary<string, string> OnlineUsers = new();

    public ChatHub(AppDbContext context) => _context = context;

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            OnlineUsers[userId] = Context.ConnectionId;
            await Clients.All.SendAsync("UserOnline", userId);
            await Clients.Caller.SendAsync("OnlineUsers", OnlineUsers.Keys.ToList());
        }
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            OnlineUsers.TryRemove(userId, out _);
            await Clients.All.SendAsync("UserOffline", userId);
        }
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinChat(string chatId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, chatId);
    }

    public async Task LeaveChat(string chatId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId);
    }

    public async Task SendMessage(string chatId, string content, string? replyToMessageId = null)
    {
        var userId = Guid.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;

        string? replyToContent = null;
        string? replyToUsername = null;
        Guid? replyToId = null;

        if (replyToMessageId != null && Guid.TryParse(replyToMessageId, out var replyGuid))
        {
            var replyMsg = await _context.Messages
                .Include(m => m.User)
                .FirstOrDefaultAsync(m => m.Id == replyGuid);

            if (replyMsg != null)
            {
                replyToId = replyMsg.Id;
                replyToContent = replyMsg.Type == MessageType.Text
                    ? replyMsg.Content
                    : replyMsg.Type == MessageType.Image ? "🖼️ Изображение"
                    : replyMsg.Type == MessageType.Video ? "📹 Видео"
                    : replyMsg.Type == MessageType.Sticker ? "🎭 Стикер"
                    : replyMsg.Type == MessageType.Voice ? "🎤 Голосовое"
                    : replyMsg.Content;
                replyToUsername = replyMsg.User?.Username;
            }
        }

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = Guid.Parse(chatId),
            UserId = userId,
            Content = content,
            SentAt = DateTime.UtcNow,
            ReplyToMessageId = replyToId,
            ReplyToContent = replyToContent,
            ReplyToUsername = replyToUsername
        };

        _context.Messages.Add(message);

        var members = await _context.ChatMembers
            .Where(cm => cm.ChatId == Guid.Parse(chatId) && cm.UserId != userId)
            .ToListAsync();

        foreach (var member in members)
            member.UnreadCount++;

        await _context.SaveChangesAsync();

        await Clients.Group(chatId).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            chatId,
            content = message.Content,
            sentAt = message.SentAt,
            userId = message.UserId,
            username = user.Username,
            avatarUrl = user.AvatarUrl,
            emojiPrefix = user.EmojiPrefix,
            isRead = false,
            replyToMessageId = replyToId,
            replyToContent,
            replyToUsername
        });

        foreach (var member in members)
        {
            var memberIdStr = member.UserId.ToString();
            if (OnlineUsers.TryGetValue(memberIdStr, out var connId))
            {
                await _context.Entry(member).ReloadAsync();
                await Clients.Client(connId).SendAsync("UnreadUpdated", new
                {
                    chatId,
                    unreadCount = member.UnreadCount
                });
            }
        }
    }

    public async Task SendSticker(string chatId, string stickerId)
    {
        var userId = Guid.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;

        var sticker = await _context.Stickers.FindAsync(Guid.Parse(stickerId));
        if (sticker == null) return;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = Guid.Parse(chatId),
            UserId = userId,
            Content = stickerId,
            AttachmentUrl = sticker.Url,
            Type = MessageType.Sticker,
            SentAt = DateTime.UtcNow,
        };

        _context.Messages.Add(message);
        await _context.SaveChangesAsync();

        await Clients.Group(chatId).SendAsync("ReceiveMessage", new
        {
            id = message.Id,
            chatId,
            content = stickerId,
            attachmentUrl = sticker.Url,
            type = "Sticker",
            sentAt = message.SentAt,
            userId = message.UserId,
            username = user.Username,
            avatarUrl = user.AvatarUrl,
            emojiPrefix = user.EmojiPrefix,
            packId = sticker.PackId,
            isRead = false
        });
    }

    public async Task AddReaction(string messageId, string emoji)
    {
        var userId = Guid.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var msgGuid = Guid.Parse(messageId);

        // Если уже такая же реакция — удаляем (toggle)
        var existing = await _context.MessageReactions
            .FirstOrDefaultAsync(r => r.MessageId == msgGuid && r.UserId == userId && r.Emoji == emoji);

        string action;
        if (existing != null)
        {
            _context.MessageReactions.Remove(existing);
            action = "removed";
        }
        else
        {
            // Убираем любую другую реакцию этого пользователя на это сообщение
            var other = await _context.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == msgGuid && r.UserId == userId);
            if (other != null)
                _context.MessageReactions.Remove(other);

            _context.MessageReactions.Add(new MessageReaction
            {
                Id = Guid.NewGuid(),
                MessageId = msgGuid,
                UserId = userId,
                Emoji = emoji
            });
            action = "added";
        }

        await _context.SaveChangesAsync();

        // Получаем актуальные реакции
        var reactions = await _context.MessageReactions
            .Where(r => r.MessageId == msgGuid)
            .GroupBy(r => r.Emoji)
            .Select(g => new { emoji = g.Key, count = g.Count(), userIds = g.Select(r => r.UserId).ToList() })
            .ToListAsync();

        // Получаем chatId сообщения
        var msg = await _context.Messages.FindAsync(msgGuid);
        if (msg == null) return;

        await Clients.Group(msg.ChatId.ToString()).SendAsync("ReactionUpdated", new
        {
            messageId,
            reactions
        });
    }
}