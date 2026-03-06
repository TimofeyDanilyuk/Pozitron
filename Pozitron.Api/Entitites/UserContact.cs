using Pozitron.Api.Entitites;

public class UserContact
{
    public Guid UserId { get; set; }
    public User? User { get; set; }
    
    public Guid ContactId { get; set; }
    public User? Contact { get; set; }
    
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
}