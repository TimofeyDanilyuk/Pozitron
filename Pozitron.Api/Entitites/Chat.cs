namespace Pozitron.Api.Entitites;

public class Chat
{
    public Guid Id { get; set; }
    public ChatType Type { get; set; }
    public string? Name { get; set; } // для общего канала
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<ChatMember> Members { get; set; } = new();
    public List<Message> Messages { get; set; } = new();
}

public class ChatMember
{
    public Guid ChatId { get; set; }
    public Chat? Chat { get; set; }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public int UnreadCount { get; set; } = 0;
}

public enum ChatType
{
    General,  // общий канал
    Direct    // личные сообщения
}