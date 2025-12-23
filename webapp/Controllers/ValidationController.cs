using Microsoft.AspNetCore.Mvc;
using webapp.Services;

namespace webapp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ValidationController(IContentValidationRepository repo) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus([FromQuery] string jobId, CancellationToken ct)
    {
        if (!Guid.TryParse(jobId, out var id)) return BadRequest("Invalid jobId");
        var rows = await repo.GetByJobAsync(id, ct);
        return Ok(rows.Select(r => new { folder = r.Folder, testKind = r.TestKind, status = r.Status }));
    }

    [HttpGet("latest")]
    public async Task<IActionResult> GetLatest([FromQuery] string testKind = "file_has_correct_md5_prefix", CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(testKind)) testKind = "file_has_correct_md5_prefix";
        var rows = await repo.GetLatestByTestKindAsync(testKind, ct);
        return Ok(rows.Select(r => new { folder = r.Folder, testKind = r.TestKind, status = r.Status }));
    }
}
