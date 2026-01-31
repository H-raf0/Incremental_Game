using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;

using GameServerApi.Models;
using GameServerApi.Exceptions;

namespace GameServerApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly GameServerApi.Services.UserService _userService;
        private readonly ILogger<UserController> _logger;
        private readonly GameServerApi.Services.ConnectionTrackerService _connectionTrackerService;

        public UserController(GameServerApi.Services.UserService userService, ILogger<UserController> logger, GameServerApi.Services.ConnectionTrackerService connectionTrackerService)
        {
            _userService = userService;
            _logger = logger;
            _connectionTrackerService = connectionTrackerService;
        }

        /// <summary>
        /// Retrieves a list of all users with public information.
        /// </summary>
        /// <returns>A list of all users with their public data (Id, Username, Role).</returns>
        [HttpGet("All")]
        [AllowAnonymous]
        public async Task<List<UserPublic>> GetAllUsers()
        {
            _logger.LogDebug("GetAllUsers called");
            var users = await _userService.GetAllUsersAsync();
            return users;
        }

        /// <summary>
        /// Retrieves a specific user by their ID.
        /// </summary>
        /// <param name="id">The ID of the user to retrieve.</param>
        /// <returns>The user with their public information.</returns>
        [HttpGet("{id}")]
        [Authorize]
        public async Task<UserPublic> GetUserById(int id)
        {
            _logger.LogDebug("GetUserById called for {UserId}", id);
            var user = await _userService.GetUserByIdAsync(id);
            return user;
        }

        /// <summary>
        /// Searches for users by username.
        /// </summary>
        /// <param name="name">The username or partial username to search for.</param>
        /// <returns>A collection of users matching the search criteria.</returns>
        [HttpGet("Search/{name}")]
        [Authorize]
        public async Task<IEnumerable<UserPublic>> SearchUsers(string name)
        {
            _logger.LogDebug("SearchUsers called with name={Name}", name);
            var result = await _userService.SearchUsersAsync(name);
            return result;
        }

        /// <summary>
        /// Retrieves all users with admin role.
        /// </summary>
        /// <returns>A collection of all admin users.</returns>
        [HttpGet("AllAdmin")]
        [Authorize(Roles = "Admin")]
        public async Task<IEnumerable<UserPublic>> GetAllAdminUsers()
        {
            var result = await _userService.GetAllAdminUsersAsync();
            return result;
        }


        /// <summary>
        /// Registers a new user account.
        /// </summary>
        /// <param name="newUser">The new user's credentials (username and password).</param>
        /// <returns>An object containing the authentication token and the new user's public information.</returns>
        [HttpPost("Register")]
        [AllowAnonymous]
        [EnableRateLimiting("fixed")]
        public async Task<object> RegisterUser([FromBody] UserPass newUser)
        {
            _logger.LogInformation("Register attempt for username {Username}", newUser.Username);
            var (Token, User) = await _userService.RegisterAsync(newUser);

            _logger.LogInformation("User registered {Username} (Id: {UserId})", User.Username, User.Id);
            return new { token = Token, user = User };
        }


        /// <summary>
        /// Authenticates a user and returns a JWT token.
        /// </summary>
        /// <param name="userPass">The user's login credentials.</param>
        /// <returns>An object containing the authentication token and the user's public information.</returns>
        [HttpPost("Login")]
        [AllowAnonymous]
        [EnableRateLimiting("fixed")]
        public async Task<object> Login([FromBody] UserPass userPass)
        {
            _logger.LogInformation("Login attempt for username {Username}", userPass.Username);
            var (Token, User) = await _userService.LoginAsync(userPass);
            _logger.LogInformation("User logged in {Username} (Id: {UserId})", User.Username, User.Id);
                // Online tracking is now handled in ChatHub via SignalR connections
            return new { token = Token, user = User };
        }

        /// <summary>
        /// Logs out the current authenticated user.
        /// </summary>
        /// <returns>A confirmation message if logout was successful.</returns>
        [HttpPost("Logout")]
        [Authorize]
        public IActionResult Logout()
        {
            // Get user id from claims
            var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdClaim, out int userId))
            {
                _logger.LogInformation("User logged out (Id: {UserId})", userId);
                return Ok(new { message = "Logged out successfully" });
            }
            else
            {
                _logger.LogWarning("Logout failed: UserId claim missing or invalid");
                return BadRequest(new { error = "Invalid user id" });
            }
        }




        /// <summary>
        /// Updates an existing user's information (admin only).
        /// </summary>
        /// <param name="id">The ID of the user to update.</param>
        /// <param name="userUpdate">The updated user information.</param>
        /// <returns>The updated user object.</returns>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<User> UpdateUser(int id, [FromBody] UserUpdate userUpdate)
        {
            var user = await _userService.UpdateUserAsync(id, userUpdate);
            return user;
        }



        /// <summary>
        /// Deletes a user account (admin only).
        /// </summary>
        /// <param name="id">The ID of the user to delete.</param>
        /// <returns>A boolean indicating success.</returns>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<bool>> DeleteUser(int id)
        {
            await _userService.DeleteUserAsync(id);
            return Ok(true);
        }
        
    }
}
