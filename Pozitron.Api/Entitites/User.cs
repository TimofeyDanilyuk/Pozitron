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

        public UserRole Role { get; set; } = UserRole.User;
        public bool IsBanned { get; set; } = false;

        public List<UserContact> Contacts { get; set; } = new();

        public string? SecurityQuestion { get; set; }
        public string? SecurityAnswerHash { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<Message> Messages { get; set; } = new();
        public List<UserAvatar> Avatars { get; set; } = new();
    }

    public class UserAvatar
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public User? User { get; set; }
        public string Url { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum UserRole
    {
        User,
        Admin
    }
}