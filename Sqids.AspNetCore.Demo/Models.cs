using System.ComponentModel.DataAnnotations;

namespace Sqids.AspNetCore.Demo;

public class UserDto
{
    public Sqid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;
}

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
