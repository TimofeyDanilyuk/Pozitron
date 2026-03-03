namespace Pozitron.Api.Entitites;

public class Message
{
    public Guid Id { get; set; }
    public string? Content { get; set; }
    public string? AttachmentUrl { get; set; }
    public MessageType Type { get; set; } = MessageType.Text;

    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Chat? Chat { get; set; }

    public Guid ChatId { get; set; }
}

public enum MessageType
{
    Text,
    Image,
    Video,
    Gif,
    Voice,
    Emoji
}