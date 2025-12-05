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

        private readonly ApplicationDbContext _context;
        private readonly JwtService _jwtService;
        public UserController(ApplicationDbContext ctx, JwtService jwtService)
        {
            _context = ctx;
            _jwtService = jwtService;
        }

        // GET: api/<UserController>/All
        [HttpGet("All")]
        [AllowAnonymous]
        public async Task<ActionResult<List<UserPublic>>> GetAllUsers()
        {
            var users = await _context.Users
                .Select(u => new UserPublic(u.Id, u.Username, u.Role))
                .ToListAsync();

            return Ok(users);
        }

        // GET api/<UserController>/{id}
        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult<UserPublic>> GetUserById(int id)
        {
            var user = await _context.Users
                .Where(u => u.Id == id)
                .Select(u => new UserPublic(u.Id, u.Username, u.Role))
                .FirstOrDefaultAsync();

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
            if (string.IsNullOrWhiteSpace(name))
                return Ok(Array.Empty<UserPublic>());

            var lowerName = name.ToLower();

            var users = await _context.Users
                .Where(u => u.Username.ToLower().Contains(lowerName) || u.Username.ToLower() == lowerName)
                .ToListAsync();

            var result = users.Select(u => new UserPublic(u.Id, u.Username, u.Role));
            return Ok(result);
        }

        // GET: api/<UserController>/AllAdmin
        [HttpGet("AllAdmin")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult<IEnumerable<UserPublic>>> GetAllAdminUsers()
        {
            // Get all users with Role.ADMIN
            var admins = await _context.Users
                .Where(u => u.Role == Role.ADMIN)
                .ToListAsync();

            // Convert to UserPublic DTO
            var result = admins.Select(u => new UserPublic(u.Id, u.Username, u.Role));

            return Ok(result);
        }


        // POST api/<UserController>/Register
        [HttpPost("Register")]
        [AllowAnonymous]
        public async Task<ActionResult<dynamic>> RegisterUser([FromBody] UserPass newUser)
        {
            // Check if username already exists
            bool exists = await _context.Users.AnyAsync(u => u.Username == newUser.Username);
            if (exists)
            {
                return BadRequest(new ErrorResponse(
                    "Username already exists",
                    "USERNAME_EXISTS"
                ));
            }

            try
            {
                // Check if any Admin exists in the database
                bool adminExists = await _context.Users.AnyAsync(u => u.Role == Role.ADMIN);
                
                // Determine the role: ADMIN if no admin exists, otherwise USER
                Role userRole = adminExists ? Role.USER : Role.ADMIN;

                // Create user with password (constructor handles hashing)
                User user = new User(newUser.Username, newUser.Password, userRole);

                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                
                // Initialize progression for the new user
                var progression = new Progression(user.Id);
                _context.Progressions.Add(progression);
                await _context.SaveChangesAsync();

                // Generate JWT token
                var token = _jwtService.GenerateToken(user);

                // Return 201 Created with token
                return CreatedAtAction(nameof(GetUserById),
                    new { id = user.Id },
                    new { token = token, user = new UserPublic(user.Id, user.Username, user.Role) });
            }
            catch
            {
                // Any unexpected failure
                return BadRequest(new ErrorResponse(
                    "Registration failed",
                    "REGISTRATION_FAILED"
                ));
            }
        }


        // POST api/<UserController>
        [HttpPost("Login")]
        [AllowAnonymous]
        public async Task<ActionResult<dynamic>> Login([FromBody] UserPass userPass)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Username == userPass.Username);

            // non trouvé ou mot de passe incorrect
            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }
            if (!user.VerifyPassword(userPass.Password))
            {
                return Unauthorized(new ErrorResponse("invalid password", "INVALID_PASSWORD"));
            }

            // Generate JWT token
            var token = _jwtService.GenerateToken(user);

            // Return token with user info
            return Ok(new { token = token, user = new UserPublic(user.Id, user.Username, user.Role) });
        }




        // PUT api/<UserController>/5
        [HttpPut("{id}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult<User>> UpdateUser(int id, [FromBody] UserUpdate userUpdate)
        {
            // Check if the user exists
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }

            // Update username
            if (!string.IsNullOrEmpty(userUpdate.Username))
            {
                user.Username = userUpdate.Username;
            }

            // Update role
            if (userUpdate.Role != null)
            {
                // Mise à jour du rôle
                user.Role = userUpdate.Role.Value;
            }

            // Update password
            if (!string.IsNullOrEmpty(userUpdate.Password))
            {
                user.UpdatePassword(userUpdate.Password);
            }

            // Save changes
            await _context.SaveChangesAsync();

            return Ok(user);

        }



        // DELETE api/<UserController>/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<ActionResult> DeleteUser(int id)
        {
            // Rechercher l'utilisateur par son ID
            var user = await _context.Users.FindAsync(id);

            if (user == null)
            {
                return NotFound(new ErrorResponse("User not found", "USER_NOT_FOUND"));
            }


            // Supprimer l'utilisateur du contexte
            _context.Users.Remove(user);

            // Sauvegarder les modifications dans la base de données
            await _context.SaveChangesAsync();


            return Ok(true);

        }
        
    }
}
