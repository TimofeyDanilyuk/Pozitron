using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Pozitron.Api.Data;
using Pozitron.Api.Entitites;
using BC = BCrypt.Net.BCrypt;

namespace Pozitron.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;

    public AdminController(AppDbContext context) => _context = context;

    private bool IsAdmin => User.IsInRole("Admin");

    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!IsAdmin) return Forbid();
        return Ok(new
        {
            totalUsers = await _context.Users.CountAsync(),
            totalMessages = await _context.Messages.CountAsync(),
            totalChats = await _context.Chats.CountAsync(),
            bannedUsers = await _context.Users.CountAsync(u => u.IsBanned)
        });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        if (!IsAdmin) return Forbid();
        var users = await _context.Users
            .Select(u => new
            {
                u.Id,
                u.Username,
                u.DisplayName,
                u.AvatarUrl,
                u.EmojiPrefix,
                u.Role,
                u.IsBanned,
                u.CreatedAt,
                messageCount = _context.Messages.Count(m => m.UserId == u.Id)
            })
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
        return Ok(users);
    }

    [HttpDelete("users/{userId}")]
    public async Task<IActionResult> DeleteUser(Guid userId)
    {
        if (!IsAdmin) return Forbid();
        var currentId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        if (userId == currentId) return BadRequest("Нельзя удалить самого себя.");

        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        if (user.Role == UserRole.Admin) return BadRequest("Нельзя удалить другого админа.");

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpPost("users/{userId}/ban")]
    public async Task<IActionResult> ToggleBan(Guid userId)
    {
        if (!IsAdmin) return Forbid();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();
        if (user.Role == UserRole.Admin) return BadRequest("Нельзя заблокировать админа.");

        user.IsBanned = !user.IsBanned;
        await _context.SaveChangesAsync();
        return Ok(new { isBanned = user.IsBanned });
    }

    [HttpPost("users/{userId}/reset-password")]
    public async Task<IActionResult> ResetPassword(Guid userId, [FromBody] ResetPasswordRequest request)
    {
        if (!IsAdmin) return Forbid();
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return NotFound();

        user.PasswordHash = BC.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();
        return Ok();
    }

    [HttpGet("messages")]
    public async Task<IActionResult> GetMessages([FromQuery] int take = 50)
    {
        if (!IsAdmin) return Forbid();
        var messages = await _context.Messages
            .OrderByDescending(m => m.SentAt)
            .Take(take)
            .Include(m => m.User)
            .Include(m => m.Chat)
            .Select(m => new
            {
                m.Id,
                m.Content,
                m.SentAt,
                username = m.User!.Username,
                chatName = m.Chat!.Name ?? "DM"
            })
            .ToListAsync();
        return Ok(messages);
    }

    // Удалить сообщение
    [HttpDelete("messages/{messageId}")]
    public async Task<IActionResult> DeleteMessage(Guid messageId)
    {
        if (!IsAdmin) return Forbid();
        var message = await _context.Messages.FindAsync(messageId);
        if (message == null) return NotFound();

        _context.Messages.Remove(message);
        await _context.SaveChangesAsync();
        return Ok();
    }
}

public record ResetPasswordRequest(string NewPassword);