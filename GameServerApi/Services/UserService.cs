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

        /// <summary>
        /// Retrieves all users with their public information.
        /// </summary>
        /// <returns>A list of all users (public data only).</returns>
        public async Task<List<UserPublic>> GetAllUsersAsync()
        {
            return await _context.Users
                .Select(u => new UserPublic(u.Id, u.Username, u.Role))
                .ToListAsync();
        }

        /// <summary>
        /// Retrieves a specific user by their ID.
        /// </summary>
        /// <param name="id">The ID of the user to retrieve.</param>
        /// <returns>The user with their public information.</returns>
        /// <exception cref="GameException">Thrown if the user is not found.</exception>
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

        /// <summary>
        /// Searches for users by username.
        /// </summary>
        /// <param name="name">The username or partial username to search for.</param>
        /// <returns>A collection of users matching the search criteria.</returns>
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

        /// <summary>
        /// Retrieves all users with admin role.
        /// </summary>
        /// <returns>A collection of all admin users.</returns>
        public async Task<IEnumerable<UserPublic>> GetAllAdminUsersAsync()
        {
            var admins = await _context.Users
                .Where(u => u.Role == Role.ADMIN)
                .ToListAsync();

            return admins.Select(u => new UserPublic(u.Id, u.Username, u.Role));
        }

        /// <summary>
        /// Registers a new user account with automatic role assignment.
        /// </summary>
        /// <param name="newUser">The new user's credentials.</param>
        /// <returns>A tuple containing the JWT token and the new user's public information.</returns>
        /// <exception cref="GameException">Thrown if username already exists or registration fails.</exception>
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

        /// <summary>
        /// Authenticates a user and generates a JWT token.
        /// </summary>
        /// <param name="userPass">The user's login credentials.</param>
        /// <returns>A tuple containing the JWT token and the user's public information.</returns>
        /// <exception cref="GameException">Thrown if user not found or password is invalid.</exception>
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

        /// <summary>
        /// Updates an existing user's information.
        /// </summary>
        /// <param name="id">The ID of the user to update.</param>
        /// <param name="userUpdate">The updated user information.</param>
        /// <returns>The updated user object.</returns>
        /// <exception cref="GameException">Thrown if user is not found.</exception>
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

        /// <summary>
        /// Deletes a user account.
        /// </summary>
        /// <param name="id">The ID of the user to delete.</param>
        /// <exception cref="GameException">Thrown if user is not found.</exception>
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
