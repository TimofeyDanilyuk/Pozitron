using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Pozitron.Api.Data;
using Pozitron.Api.Entitites;
using BC = BCrypt.Net.BCrypt;

namespace Pozitron.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.SecurityQuestion) || string.IsNullOrWhiteSpace(request.SecurityAnswer))
                return BadRequest("Укажи секретный вопрос и ответ.");

            if (await _context.Users.AnyAsync(u => u.Username == request.Username))
                return BadRequest("Этот ник уже занят, выбери другой.");

            var user = new User
            {
                Id = Guid.NewGuid(),
                Username = request.Username,
                PasswordHash = BC.HashPassword(request.Password),
                DisplayName = request.Username,
                SecurityQuestion = request.SecurityQuestion,
                SecurityAnswerHash = BC.HashPassword(request.SecurityAnswer.ToLower().Trim()),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Регистрация успешна!", userId = user.Id });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);

            if (user == null || !BC.Verify(request.Password, user.PasswordHash))
                return Unauthorized("Неверный логин или пароль");

            if (user.IsBanned)
                return Unauthorized("Ваш аккаунт заблокирован.");

            var token = GenerateJwtToken(user);

            return Ok(new
            {
                Token = token,
                User = new { user.Id, user.Username, user.EmojiPrefix, user.AvatarUrl, user.Role }
            });
        }

        [HttpGet("question/{username}")]
        public async Task<IActionResult> GetQuestion(string username)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
            if (user == null) return NotFound("Пользователь не найден.");
            if (string.IsNullOrEmpty(user.SecurityQuestion))
                return BadRequest("У этого пользователя не задан секретный вопрос.");

            return Ok(new { question = user.SecurityQuestion });
        }

        [HttpPost("recover")]
        public async Task<IActionResult> Recover([FromBody] RecoverRequest request)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == request.Username);
            if (user == null) return NotFound("Пользователь не найден.");

            if (string.IsNullOrEmpty(user.SecurityAnswerHash))
                return BadRequest("У этого пользователя не задан секретный вопрос.");

            if (!BC.Verify(request.SecurityAnswer.ToLower().Trim(), user.SecurityAnswerHash))
                return BadRequest("Неверный ответ на секретный вопрос.");

            user.PasswordHash = BC.HashPassword(request.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Пароль успешно изменён!" });
        }

        private string GenerateJwtToken(User user)
        {
            var jwtSettings = _configuration.GetSection("Jwt");
            var key = Encoding.ASCII.GetBytes(jwtSettings["Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Name, user.Username),
                    new Claim(ClaimTypes.Role, user.Role.ToString())
                }),
                Expires = DateTime.UtcNow.AddDays(7),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = jwtSettings["Issuer"],
                Audience = jwtSettings["Audience"]
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }

    public record RegisterRequest(string Username, string Password, string SecurityQuestion, string SecurityAnswer);
    public record LoginRequest(string Username, string Password);
    public record RecoverRequest(string Username, string SecurityAnswer, string NewPassword);
}