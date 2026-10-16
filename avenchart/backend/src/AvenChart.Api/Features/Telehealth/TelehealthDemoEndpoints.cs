// SPDX-FileCopyrightText: 2026 Neil Kimber and AvenChart contributors
// SPDX-License-Identifier: GPL-3.0-or-later

namespace AvenChart.Api.Features.Telehealth;

public static class TelehealthDemoEndpoints
{
    public static WebApplication MapTelehealthDemoEndpoints(this WebApplication app)
    {
        if (!app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<TelehealthOptions>>().Value.DemoLauncherEnabled) return app;
        var group = app.MapGroup("/api/telehealth/v1/demo").WithTags("Synthetic telehealth connection demo");
        group.AddEndpointFilter(async (invocation, next) =>
        {
            try
            {
                invocation.HttpContext.RequestServices.GetRequiredService<TelehealthDemoService>().RequireEnabled(invocation.HttpContext);
                return await next(invocation);
            }
            catch (TelehealthProblem problem) { return Results.Problem(statusCode: problem.StatusCode, title: problem.Title, detail: problem.Message, extensions: new Dictionary<string, object?> { ["code"] = problem.Code }); }
        });
        group.MapPost("/runs", (TelehealthDemoService service, HttpContext context, CreateTelehealthDemoRequest request, CancellationToken ct) =>
            service.CreateAsync(request, context.Request.Headers[TelehealthEndpoints.IdempotencyHeader].ToString(), ct)).WithName("CreateSyntheticTelehealthDemo");
        group.MapGet("/runs/{runId:guid}", (TelehealthDemoService service, HttpContext context, Guid runId, CancellationToken ct) => service.StatusAsync(runId, Key(context), ct)).WithName("GetSyntheticTelehealthDemoStatus");
        group.MapPost("/runs/{runId:guid}/end", (TelehealthDemoService service, HttpContext context, Guid runId, CancellationToken ct) => service.EndAsync(runId, Key(context), ct)).WithName("EndSyntheticTelehealthDemo");
        group.MapPost("/runs/{runId:guid}/{role}/redeem", (TelehealthDemoService service, Guid runId, string role, RedeemTelehealthDemoRequest request, CancellationToken ct) => service.RedeemAsync(runId, role, request, ct));
        group.MapPost("/runs/{runId:guid}/{role}/heartbeat", (TelehealthDemoService service, HttpContext context, Guid runId, string role, TelehealthDemoHeartbeatRequest request, CancellationToken ct) => service.HeartbeatAsync(runId, role, Key(context), request, ct));
        group.MapPost("/runs/{runId:guid}/{role}/calling", (TelehealthDemoService service, HttpContext context, Guid runId, string role, CancellationToken ct) => service.CallingAsync(runId, role, Key(context), ct));
        group.MapPost("/runs/{runId:guid}/{role}/signals/read", (TelehealthDemoService service, HttpContext context, Guid runId, string role, TelehealthDemoSignalRequest request, CancellationToken ct) => service.ReadAsync(runId, role, Key(context), request, ct));
        group.MapPost("/runs/{runId:guid}/{role}/signals/write", (TelehealthDemoService service, HttpContext context, Guid runId, string role, TelehealthDemoSignalRequest request, CancellationToken ct) => service.WriteAsync(runId, role, Key(context), request, ct));
        return app;
    }

    private static string Key(HttpContext context) => TelehealthDemoPolicy.RequireKey(context.Request.Headers["X-AvenChart-Telehealth-Demo-Key"].ToString());
}
