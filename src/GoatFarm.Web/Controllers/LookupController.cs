using GoatFarm.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

[IgnoreAntiforgeryToken]
public class LookupController : Controller
{
    private readonly ILookupService _lookupService;

    public LookupController(ILookupService lookupService) => _lookupService = lookupService;

    [HttpGet]
    public async Task<IActionResult> Get(string key, CancellationToken cancellationToken) =>
        Json(await _lookupService.GetListAsync(key, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> AddOption([FromBody] AddLookupOptionRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Key) || string.IsNullOrWhiteSpace(request.Value))
            return BadRequest(new { error = "Key and value are required." });

        try
        {
            var list = await _lookupService.AddOptionAsync(request.Key, request.Value, cancellationToken);
            return Json(list);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    public record AddLookupOptionRequest(string Key, string Value);
}
