using BattleTanks.Contracts.Responses.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BattleTanks.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/mqtt")]
public sealed class MqttConfigController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public MqttConfigController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("config")]
    public ActionResult<MqttClientConfigResponse> GetClientConfig()
    {
        var password = _configuration["Mqtt:FrontendPassword"]
            ?? Environment.GetEnvironmentVariable("MQTT_FRONTEND_PASSWORD");
        if (string.IsNullOrWhiteSpace(password))
        {
            return Problem(
                "MQTT frontend credentials are not configured.",
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return Ok(new MqttClientConfigResponse(
            _configuration["Mqtt:WebSocketUrl"] ?? "ws://localhost:8083/mqtt",
            "battle-tanks-frontend",
            password));
    }
}
