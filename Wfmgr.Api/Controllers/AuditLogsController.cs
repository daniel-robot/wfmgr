using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Wfmgr.Application.Workflows.V1;

namespace Wfmgr.Api.Controllers;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController : ControllerBase
{
    private readonly ICaseQueryService _queryService;

    public AuditLogsController(ICaseQueryService queryService)
    {
        _queryService = queryService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditLogViewDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AuditLogViewDto>>> GetAuditLogs(CancellationToken ct)
    {
        var items = await _queryService.GetAuditLogsAsync(ct);
        return Ok(items);
    }
}
