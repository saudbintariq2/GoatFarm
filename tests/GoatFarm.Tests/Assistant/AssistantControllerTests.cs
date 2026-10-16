using System.Security.Claims;
using GoatFarm.Application.Interfaces;
using GoatFarm.Application.ViewModels.Assistant;
using GoatFarm.Web.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace GoatFarm.Tests.Assistant;

public class AssistantControllerTests
{
    [Fact]
    public async Task Chat_EmptyMessage_IsBadRequest()
    {
        var controller = Create("u1", new FakeAssistant());
        var result = await controller.Chat(new AssistantChatRequest { Message = "  " }, default);
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Chat_Anonymous_IsUnauthorized()
    {
        var controller = Create(null, new FakeAssistant());
        var result = await controller.Chat(new AssistantChatRequest { Message = "Show milk." }, default);
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Chat_UsesAuthenticatedUser_NotRequestUserId()
    {
        var fake = new FakeAssistant();
        var controller = Create("real-user", fake);
        var json = Assert.IsType<JsonResult>(await controller.Chat(new AssistantChatRequest
        {
            Message = "Show milk production for last month.",
            ConversationId = "from-client"
        }, default));

        var body = Assert.IsType<AssistantTurnResponse>(json.Value);
        Assert.Equal("from-client", body.ConversationId);
        Assert.Equal("real-user", fake.LastUserId);
        Assert.Equal("Show milk production for last month.", fake.LastRequest?.Message);
        Assert.Contains(body.Blocks, b => b.Kind == AssistantBlockKind.Text);
    }

    private static AssistantController Create(string? userId, IAssistantService service)
    {
        var controller = new AssistantController(service, NullLogger<AssistantController>.Instance);
        var identity = userId is null
            ? new ClaimsIdentity()
            : new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId)], "test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        return controller;
    }

    private sealed class FakeAssistant : IAssistantService
    {
        public string? LastUserId { get; private set; }
        public AssistantChatRequest? LastRequest { get; private set; }

        public Task<AssistantTurnResponse> ChatAsync(string userId, AssistantChatRequest request, CancellationToken cancellationToken = default)
        {
            LastUserId = userId;
            LastRequest = request;
            return Task.FromResult(new AssistantTurnResponse
            {
                ConversationId = request.ConversationId ?? "new",
                Blocks = [new AssistantBlock { Kind = AssistantBlockKind.Text, Text = "ok" }]
            });
        }
    }
}
