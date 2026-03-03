using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Pozitron.Api.Entitites;

namespace Pozitron.Api.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<Chat> Chats => Set<Chat>();
        public DbSet<ChatMember> ChatMembers => Set<ChatMember>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Username)
                .IsUnique();

            modelBuilder.Entity<Message>()
                .HasOne<User>(m => m.User)
                .WithMany(u => u.Messages)
                .HasForeignKey(m => m.UserId); ;

            modelBuilder.Entity<ChatMember>()
                .HasKey(cm => new { cm.ChatId, cm.UserId });

            modelBuilder.Entity<Chat>()
                .HasMany(c => c.Messages)
                .WithOne(m => m.Chat)
                .HasForeignKey(m => m.ChatId);    
        }
    }
}