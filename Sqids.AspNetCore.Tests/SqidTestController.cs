using System.Globalization;
using Microsoft.AspNetCore.Mvc;

namespace Sqids.AspNetCore.Tests;

[ApiController]
public sealed class SqidTestController : ControllerBase
{
	[HttpGet("bind/route/{id:sqid}")]
	public string Route(Sqid id) => id.Value.ToString(CultureInfo.InvariantCulture);

	[HttpGet("bind/query")]
	public string Query([FromQuery] Sqid id) => id.Value.ToString(CultureInfo.InvariantCulture);

	[HttpGet("bind/optional")]
	public string Optional([FromQuery] Sqid? id) => id is { } value ? value.Value.ToString(CultureInfo.InvariantCulture) : "none";

	[HttpGet("bind/empty")]
	public string Empty([FromQuery] Sqid id) => id.Value.ToString(CultureInfo.InvariantCulture);

	[HttpGet("bind/int/{id}")]
	public string Int(int id) => id.ToString(CultureInfo.InvariantCulture);
}
