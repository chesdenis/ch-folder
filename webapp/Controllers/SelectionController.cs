using System.Linq;
using Microsoft.AspNetCore.Mvc;
using webapp.Services;

namespace webapp.Controllers;

[ApiController]
[Route("selection")]
public sealed class SelectionController(ISearchSessionSelectionRepository repo) : ControllerBase
{
    public sealed record SelectionRequest(Guid SessionId, string Md5);

    [HttpPost("add")]
    public async Task<IActionResult> Add([FromBody] SelectionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Md5))
            return BadRequest("md5 is required");

        var ok = await repo.AddSelectionAsync(req.SessionId, req.Md5, ct);
        return Ok(new { inserted = ok });
    }

    [HttpPost("remove")]
    public async Task<IActionResult> Remove([FromBody] SelectionRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Md5))
            return BadRequest("md5 is required");

        var ok = await repo.RemoveSelectionAsync(req.SessionId, req.Md5, ct);
        return Ok(new { deleted = ok });
    }

    [HttpGet("list")]
    public async Task<IActionResult> List([FromQuery] Guid sessionId, CancellationToken ct)
    {
        var items = await repo.GetSelectedMd5Async(sessionId, ct);
        var md5s = items.Select(i => i.Md5).ToArray();
        return Ok(md5s);
    }
}
