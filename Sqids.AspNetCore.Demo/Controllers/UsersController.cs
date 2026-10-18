using Microsoft.AspNetCore.Mvc;
using Sqids.AspNetCore;

namespace Sqids.AspNetCore.Demo.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private static readonly Dictionary<long, (string Name, string Email)> Users = new()
    {
        { 1, ("Alice Smith", "alice@example.com") },
        { 2, ("Bob Johnson", "bob@example.com") },
        { 3, ("Charlie Brown", "charlie@example.com") }
    };

    [HttpGet("{id:sqid}")]
    public IActionResult GetUser(Sqid id)
    {
        if (Users.TryGetValue(id.Value, out var user))
        {
            return Ok(new UserResponse
            {
                Id = id.ToString(),
                Name = user.Name,
                Email = user.Email
            });
        }
        return NotFound(new { Message = $"User with id {id} not found" });
    }

    [HttpGet]
    public IActionResult GetUsers()
    {
        var results = Users.Select(kvp => new UserResponse
        {
            Id = Sqid.From(kvp.Key).ToString(),
            Name = kvp.Value.Name,
            Email = kvp.Value.Email
        });
        return Ok(results);
    }

    [HttpPost]
    public IActionResult CreateUser([FromBody] CreateUserRequest request)
    {
        var nextId = Users.Keys.Count + 1;
        Users[nextId] = (request.Name, request.Email);
        
        var sqid = Sqid.From(nextId);
        return CreatedAtAction(
            nameof(GetUser), 
            new { id = sqid.ToString() }, 
            new UserResponse
            {
                Id = sqid.ToString(),
                Name = request.Name,
                Email = request.Email
            });
    }

    [HttpGet("by-sqid")]
    public IActionResult GetBySqid([FromQuery] Sqid? id)
    {
        if (id is null)
        {
            return BadRequest(new { Message = "Sqid query parameter is required" });
        }

        var sqid = id.GetValueOrDefault();
        if (Users.TryGetValue(sqid.Value, out var user))
        {
            return Ok(new UserResponse
            {
                Id = sqid.ToString(),
                Name = user.Name,
                Email = user.Email
            });
        }
        return NotFound(new { Message = $"User with id {sqid} not found" });
    }
}
