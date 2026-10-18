using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace Sqids.AspNetCore.Tests;

[ApiController]
[Route("bind")]
public sealed class SqidTestController : ControllerBase
{
	[HttpGet("route/{id:sqid}")]
	public string Route(Sqid id) => id.Value.ToString(CultureInfo.InvariantCulture);

	[HttpGet("query")]
	public string Query([FromQuery] Sqid id) => id.Value.ToString(CultureInfo.InvariantCulture);

	[HttpGet("optional")]
	public string Optional([FromQuery] Sqid? id) => id is { } value ? value.Value.ToString(CultureInfo.InvariantCulture) : "none";

	[HttpGet("empty")]
	public new string Empty([FromQuery] Sqid id) => id.Value.ToString(CultureInfo.InvariantCulture);

	[HttpGet("int/{id}")]
	public string Int(int id) => id.ToString(CultureInfo.InvariantCulture);
}
