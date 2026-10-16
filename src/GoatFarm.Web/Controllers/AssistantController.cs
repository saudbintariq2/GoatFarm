using System.Security.Claims;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using Microsoft.AspNetCore.Mvc;

namespace GoatFarm.Web.Controllers;

public class AssistantController : Controller
{
    private readonly IAssistantService _assistantService;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(IAssistantService assistantService, ILogger<AssistantController> logger)
    {
        _assistantService = assistantService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Chat([FromBody] AssistantChatRequest? request, CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { error = "Enter a question." });
        if (!ModelState.IsValid)
            return BadRequest(new { error = "The question is too long or not valid." });

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return Unauthorized(new { error = "Sign in to use the assistant." });

        try
        {
            return Json(await _assistantService.ChatAsync(userId, request, cancellationToken));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Assistant chat failed");
            return StatusCode(StatusCodes.Status500InternalServerError, new { error = "The assistant could not answer just now." });
        }
    }
}
