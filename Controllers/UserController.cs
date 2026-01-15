using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

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
        
        public UserController(GameServerApi.Services.UserService userService, ILogger<UserController> logger)
        {
            _userService = userService;
            _logger = logger;
        }

        // GET: api/<UserController>/All
        [HttpGet("All")]
        [AllowAnonymous]
        public async Task<List<UserPublic>> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return users;
        }

        // GET api/<UserController>/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<UserPublic> GetUserById(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            return user;
        }

        // GET api/<UserController>/Search/{name}
        [HttpGet("Search/{name}")]
        [Authorize]
        public async Task<IEnumerable<UserPublic>> SearchUsers(string name)
        {
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
        public async Task<object> RegisterUser([FromBody] UserPass newUser)
        {
            var (Token, User) = await _userService.RegisterUserAsync(newUser);

            return new { token = Token, user = User };
        }


        // POST api/<UserController>
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<object> Login([FromBody] UserPass userPass)
        {
            var (Token, User) = await _userService.LoginAsync(userPass);
            return new { token = Token, user = User };
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
        public async Task DeleteUser(int id)
        {
            await _userService.DeleteUserAsync(id);
        }
        
    }
}
