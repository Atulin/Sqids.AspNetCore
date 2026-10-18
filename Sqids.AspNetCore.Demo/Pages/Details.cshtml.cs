using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Sqids.AspNetCore.Demo.Pages;

/// <summary>
/// Demonstrates the <c>{id:sqid}</c> route constraint in Razor Pages:
/// <c>@page "{id:sqid}"</c> only matches canonical Sqids, so invalid IDs return 404 instead of 400.
/// </summary>
public class DetailsModel : PageModel
{
    private static readonly Dictionary<long, (string Name, string Email)> Users = new()
    {
        { 1, ("Alice Smith", "alice@example.com") },
        { 2, ("Bob Johnson", "bob@example.com") },
        { 3, ("Charlie Brown", "charlie@example.com") }
    };

    public Sqid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    public IActionResult OnGet(Sqid id)
    {
        Id = id;
        if (!Users.TryGetValue(id.Value, out var user))
        {
            return NotFound();
        }

        Name = user.Name;
        Email = user.Email;
        return Page();
    }
}
