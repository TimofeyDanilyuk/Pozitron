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
            // Отправляем новому клиенту список онлайн
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

    public async Task SendMessage(string chatId, string content)
    {
        var userId = Guid.Parse(Context.User!.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return;

        var message = new Message
        {
            Id = Guid.NewGuid(),
            ChatId = Guid.Parse(chatId),
            UserId = userId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        _context.Messages.Add(message);

        // Инкрементируем счётчик всем участникам кроме отправителя
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
            emojiPrefix = user.EmojiPrefix
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
}