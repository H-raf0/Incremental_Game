using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;

using GameServerApi.Models;

namespace GameServerApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly GameServerApi.Services.UserService _userService;
        public UserController(GameServerApi.Services.UserService userService)
        {
            _userService = userService;
        }

        // GET: api/<UserController>/All
        [HttpGet("All")]
        [AllowAnonymous]
        public async Task<ActionResult<List<UserPublic>>> GetAllUsers()
        {
            var users = await _userService.GetAllUsersAsync();
            return Ok(users);
        }

        // GET api/<UserController>/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<UserPublic>> GetUserById(int id)
        {
            var user = await _userService.GetUserByIdAsync(id);
            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }
            return Ok(user);
        }

        // GET api/<UserController>/Search/{name}
        [HttpGet("Search/{name}")]
        [Authorize]
        public async Task<ActionResult<IEnumerable<UserPublic>>> SearchUsers(string name)
        {
            var result = await _userService.SearchUsersAsync(name);
            return Ok(result);
        }

        // GET: api/<UserController>/AllAdmin
        [HttpGet("AllAdmin")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<IEnumerable<UserPublic>>> GetAllAdminUsers()
        {
            var result = await _userService.GetAllAdminUsersAsync();
            return Ok(result);
        }


        // POST api/<UserController>/Register
        [HttpPost("Register")]
        [AllowAnonymous]
        public async Task<ActionResult<dynamic>> RegisterUser([FromBody] UserPass newUser)
        {
            var (Success, Token, User, Error) = await _userService.RegisterUserAsync(newUser);
            if (!Success && Error != null)
            {
                return BadRequest(Error);
            }

            return CreatedAtAction(nameof(GetUserById), new { id = User!.Id }, new { token = Token, user = User });
        }


        // POST api/<UserController>
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<dynamic>> Login([FromBody] UserPass userPass)
        {
            var (Success, Token, User, Error) = await _userService.LoginAsync(userPass);
            if (!Success && Error != null)
            {
                if (Error.Code == "USER_NOT_FOUND") return NotFound(Error);
                return Unauthorized(Error);
            }
            return Ok(new { token = Token, user = User });
        }




        // PUT api/<UserController>/5
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<User>> UpdateUser(int id, [FromBody] UserUpdate userUpdate)
        {
            var user = await _userService.UpdateUserAsync(id, userUpdate);
            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }
            return Ok(user);

        }



        // DELETE api/<UserController>/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            var deleted = await _userService.DeleteUserAsync(id);
            if (!deleted)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }
            return Ok(true);

        }
        
    }
}
