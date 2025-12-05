using Microsoft.EntityFrameworkCore;
using GameServerApi.Models;

namespace GameServerApi.Services
{
    public class UserService
    {
        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;

        public UserService(ApplicationDbContext context, JwtService jwtService)
        {
            _context = context;
            _jwtService = jwtService;
        }

        public async Task<List<UserPublic>> GetAllUsersAsync()
        {
            return await _context.Users
                .Select(u => new UserPublic(u.Id, u.Username, u.Role))
                .ToListAsync();
        }

        public async Task<UserPublic?> GetUserByIdAsync(int id)
        {
            return await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserPublic(u.Id, u.Username, u.Role))
                .FirstOrDefaultAsync();
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

        public async Task<(bool Success, string? Token, UserPublic? User, ErrorResponse? Error)> RegisterUserAsync(UserPass newUser)
        {
            bool exists = await _context.Users.AnyAsync(u => u.Username == newUser.Username);
            if (exists)
            {
                return (false, null, null, new ErrorResponse("Username already exists", "USERNAME_EXISTS"));
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
                return (true, token, new UserPublic(user.Id, user.Username, user.Role), null);
            }
            catch
            {
                return (false, null, null, new ErrorResponse("Registration failed", "REGISTRATION_FAILED"));
            }
        }

        public async Task<(bool Success, string? Token, UserPublic? User, ErrorResponse? Error)> LoginAsync(UserPass userPass)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == userPass.Username);

            if (user == null)
            {
                return (false, null, null, new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }
            if (!user.VerifyPassword(userPass.Password))
            {
                return (false, null, null, new ErrorResponse("invalid password", "INVALID_PASSWORD"));
            }

            var token = _jwtService.GenerateToken(user);
            return (true, token, new UserPublic(user.Id, user.Username, user.Role), null);
        }

        public async Task<User?> UpdateUserAsync(int id, UserUpdate userUpdate)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return null;

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
            return user;
        }

        public async Task<bool> DeleteUserAsync(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return false;

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}
