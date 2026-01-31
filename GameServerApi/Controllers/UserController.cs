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

        // GET: api/<UserController>/All
        [HttpGet("All")]
        [AllowAnonymous]
        public async Task<List<UserPublic>> GetAllUsers()
        {
            _logger.LogDebug("GetAllUsers called");
            var users = await _userService.GetAllUsersAsync();
            return users;
        }

        // GET api/<UserController>/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<UserPublic> GetUserById(int id)
        {
            _logger.LogDebug("GetUserById called for {UserId}", id);
            var user = await _userService.GetUserByIdAsync(id);
            return user;
        }

        // GET api/<UserController>/Search/{name}
        [HttpGet("Search/{name}")]
        [Authorize]
        public async Task<IEnumerable<UserPublic>> SearchUsers(string name)
        {
            _logger.LogDebug("SearchUsers called with name={Name}", name);
            var result = await _userService.SearchUsersAsync(name);
            return result;
        }

        // GET: api/<UserController>/AllAdmin
        [HttpGet("AllAdmin")]
        [Authorize(Roles = "Admin")]
        public async Task<IEnumerable<UserPublic>> GetAllAdminUsers()
        {
            var result = await _userService.GetAllAdminUsersAsync();
            return result;
        }


        // POST api/<UserController>/Register
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


        // POST api/<UserController>
        [HttpPost("Login")]
        [AllowAnonymous]
        [EnableRateLimiting("fixed")]

        public async Task<object> Login([FromBody] UserPass userPass)
        {
            _logger.LogInformation("Login attempt for username {Username}", userPass.Username);
            var (Token, User) = await _userService.LoginAsync(userPass);
            _logger.LogInformation("User logged in {Username} (Id: {UserId})", User.Username, User.Id);
            // Online tracking is now handled in ChatHub via SignalR connection
            return new { token = Token, user = User };
        }

        // POST api/<UserController>/Logout
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




        // PUT api/<UserController>/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<User> UpdateUser(int id, [FromBody] UserUpdate userUpdate)
        {
            var user = await _userService.UpdateUserAsync(id, userUpdate);
            return user;
        }



        // DELETE api/<UserController>/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<bool>> DeleteUser(int id)
        {
            await _userService.DeleteUserAsync(id);
            return Ok(true);
        }
        
    }
}
