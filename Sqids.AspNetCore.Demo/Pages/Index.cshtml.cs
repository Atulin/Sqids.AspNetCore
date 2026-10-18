using Microsoft.AspNetCore.Mvc.RazorPages;
using Sqids.AspNetCore;

namespace Sqids.AspNetCore.Demo.Pages;

public class IndexModel : PageModel
{
    private static readonly Dictionary<long, string> Users = new()
    {
        { 1, "Alice Smith" },
        { 2, "Bob Johnson" },
        { 3, "Charlie Brown" }
    };

    public List<(string Sqid, string Name)> UserLinks { get; } = [];

    public void OnGet()
    {
        UserLinks.AddRange(Users.Select(kvp => (Sqid.From(kvp.Key).ToString(), kvp.Value)));
    }
}
