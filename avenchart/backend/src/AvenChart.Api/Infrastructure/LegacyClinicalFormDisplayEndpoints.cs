using AvenChart.Api.Data;

namespace AvenChart.Api.Infrastructure;

public static class LegacyClinicalFormDisplayEndpoints
{
    public static RouteGroupBuilder MapLegacyClinicalFormDisplayEndpoints(
        this RouteGroupBuilder group)
    {
        group.MapGet("/patients/{patientId}/legacy-snapshots", async (
                LegacyClinicalFormDisplayRepository repository,
                string patientId,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    return Results.Ok(await repository.ListAsync(
                        patientId,
                        cancellationToken));
                }
                catch (ArgumentException exception)
                {
                    return Results.BadRequest(new { error = exception.Message });
                }
            })
            .WithName("GetPatientLegacyClinicalFormSnapshots");

        group.MapGet("/legacy-snapshots/{snapshotId:guid}", async (
                LegacyClinicalFormDisplayRepository repository,
                Guid snapshotId,
                CancellationToken cancellationToken) =>
            {
                var result = await repository.GetAsync(snapshotId, cancellationToken);
                return result is null ? Results.NotFound() : Results.Ok(result);
            })
            .WithName("GetLegacyClinicalFormSnapshot");

        group.MapGet(
                "/patients/{patientId}/legacy-migration-manifests/{stableKey}",
                async (
                    LegacyClinicalFormDisplayRepository repository,
                    string patientId,
                    string stableKey,
                    CancellationToken cancellationToken) =>
                {
                    try
                    {
                        var result = await repository.GetMigrationManifestAsync(
                            patientId,
                            stableKey,
                            cancellationToken);
                        return result is null
                            ? Results.NotFound()
                            : Results.Ok(result);
                    }
                    catch (ArgumentException exception)
                    {
                        return Results.BadRequest(new { error = exception.Message });
                    }
                })
            .WithName("GetPatientLegacyClinicalFormMigrationManifest");

        return group;
    }
}
