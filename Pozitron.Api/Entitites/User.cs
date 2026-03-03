using Pozitron.Api.Entitites;

namespace Pozitron.Api.Entitites
{
    public class User
    {
        public Guid Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;

        public string? AvatarUrl { get; set; }
        public string? EmojiPrefix { get; set; }
        public string? DisplayName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<Message> Messages { get; set; } = new();
    }
}