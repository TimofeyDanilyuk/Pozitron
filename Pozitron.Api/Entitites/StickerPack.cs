namespace Pozitron.Api.Entitites;

public class StickerPack
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Sticker> Stickers { get; set; } = new();
    public List<UserStickerPack> UserPacks { get; set; } = new();
}

public class Sticker
{
    public Guid Id { get; set; }
    public Guid PackId { get; set; }
    public StickerPack? Pack { get; set; }
    public string Url { get; set; } = string.Empty;
    public int Order { get; set; } = 0;
}

public class UserStickerPack
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid PackId { get; set; }
    public StickerPack? Pack { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}