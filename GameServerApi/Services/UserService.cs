using Microsoft.EntityFrameworkCore;
using GameServerApi.Models;
using GameServerApi.Exceptions;
using Microsoft.Extensions.Logging;

namespace GameServerApi.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        private readonly ILogger<UserService> _logger;

        public UserService(ApplicationDbContext context, JwtService jwtService, ILogger<UserService> logger)
        {
            _context = context;
            _jwtService = jwtService;
            _logger = logger;
        }

        public async Task<List<UserPublic>> GetAllUsersAsync()
        {
            return await _context.Users
                .Select(u => new UserPublic(u.Id, u.Username, u.Role))
                .ToListAsync();
        }

        public async Task<UserPublic> GetUserByIdAsync(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserPublic(u.Id, u.Username, u.Role))
                .FirstOrDefaultAsync();
            if (user == null)
            {
                throw new GameException("User not found", "USER_NOT_FOUND", 404);
            }
            return user;
        }

        public async Task<IEnumerable<UserPublic>> SearchUsersAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return Array.Empty<UserPublic>();

            var lowerName = name.ToLower();

            var users = await _context.Users
                .Where(u => u.Username.ToLower().Contains(lowerName) || u.Username.ToLower() == lowerName)
                .ToListAsync();

            return users.Select(u => new UserPublic(u.Id, u.Username, u.Role));
        }

        public async Task<IEnumerable<UserPublic>> GetAllAdminUsersAsync()
        {
            var admins = await _context.Users
                .Where(u => u.Role == Role.ADMIN)
                .ToListAsync();

            return admins.Select(u => new UserPublic(u.Id, u.Username, u.Role));
        }

        public async Task<(string Token, UserPublic User)> RegisterAsync(UserPass newUser)
        {
            _logger.LogInformation("User registration attempt: {Username}", newUser.Username);
            
            bool exists = await _context.Users.AnyAsync(u => u.Username == newUser.Username);
            if (exists)
            {
                _logger.LogWarning("Registration failed: Username already exists - {Username}", newUser.Username);
                throw new GameException("Username already exists", "USERNAME_EXISTS", 400);
            }

            try
            {
                bool adminExists = await _context.Users.AnyAsync(u => u.Role == Role.ADMIN);
                Role userRole = adminExists ? Role.USER : Role.ADMIN;

                User user = new User(newUser.Username, newUser.Password, userRole);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var progression = new Progression(user.Id);
                _context.Progressions.Add(progression);
                await _context.SaveChangesAsync();

                var token = _jwtService.GenerateToken(user);
                _logger.LogInformation("User registered successfully: {Username} with role {Role}", user.Username, user.Role);
                return (token, new UserPublic(user.Id, user.Username, user.Role));
            }
            catch
            {
                _logger.LogError("Registration failed for user: {Username}", newUser.Username);
                throw new GameException("Registration failed", "REGISTRATION_FAILED", 500);
            }
        }

        public async Task<(string Token, UserPublic User)> LoginAsync(UserPass userPass)
        {
            _logger.LogInformation("User login attempt: {Username}", userPass.Username);
            
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == userPass.Username);

            if (user == null)
            {
                _logger.LogWarning("Login failed: User not found - {Username}", userPass.Username);
                throw new GameException("User not found", "USER_NOT_FOUND", 404);
            }
            if (!user.VerifyPassword(userPass.Password))
            {
                _logger.LogWarning("Login failed: Invalid password for user - {Username}", userPass.Username);
                throw new GameException("invalid password", "INVALID_PASSWORD", 401);
            }

            var token = _jwtService.GenerateToken(user);
            _logger.LogInformation("User logged in successfully: {Username}", userPass.Username);
            return (token, new UserPublic(user.Id, user.Username, user.Role));
        }

        public async Task<User> UpdateUserAsync(int id, UserUpdate userUpdate)
        {
            _logger.LogInformation("User update attempt: UserId {UserId}", id);
            
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User update failed: User not found - UserId {UserId}", id);
                throw new GameException("User not found", "USER_NOT_FOUND", 404);
            }

            if (!string.IsNullOrEmpty(userUpdate.Username))
            {
                user.Username = userUpdate.Username;
            }

            if (userUpdate.Role != null)
            {
                user.Role = userUpdate.Role.Value;
            }

            if (!string.IsNullOrEmpty(userUpdate.Password))
            {
                user.UpdatePassword(userUpdate.Password);
            }

            await _context.SaveChangesAsync();
            _logger.LogInformation("User updated successfully: UserId {UserId}", id);
            return user;
        }

        public async Task DeleteUserAsync(int id)
        {
            _logger.LogInformation("User deletion attempt: UserId {UserId}", id);
            
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                _logger.LogWarning("User deletion failed: User not found - UserId {UserId}", id);
                throw new GameException("User not found", "USER_NOT_FOUND", 404);
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            _logger.LogInformation("User deleted successfully: UserId {UserId}", id);
        }
    }
}
