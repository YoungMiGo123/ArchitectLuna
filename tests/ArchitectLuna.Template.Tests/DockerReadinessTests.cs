using ArchitectLuna.Cli.Scaffolding;
using ArchitectLuna.Core.Model;
using Xunit;

namespace ArchitectLuna.Template.Tests;

/// <summary>
/// docs/requirements/003-improvements.md §12: generated Docker Compose setups include health
/// checks so `docker compose up --build` produces containers that report readiness, and the api
/// service waits on a healthy db instead of merely a started one.
/// </summary>
public sealed class DockerReadinessTests
{
    [Fact]
    public void Dockerfile_HasAHealthCheckAgainstTheHealthEndpoint()
    {
        var dockerfile = InfrastructureFiles.Dockerfile("BillingService");

        Assert.Contains("HEALTHCHECK", dockerfile);
        Assert.Contains("http://localhost:8080/health", dockerfile);
    }

    [Theory]
    [InlineData(PersistenceProvider.None)]
    [InlineData(PersistenceProvider.InMemory)]
    public void DockerCompose_ApiHasHealthCheck_EvenWithoutADatabase(PersistenceProvider provider)
    {
        var compose = InfrastructureFiles.DockerCompose("BillingService", provider);

        Assert.Contains("healthcheck:", compose);
        Assert.Contains("http://localhost:8080/health", compose);
        Assert.DoesNotContain("db:", compose);
    }

    [Fact]
    public void DockerCompose_Postgres_AddsPgIsReadyHealthCheckAndServiceHealthyDependency()
    {
        var compose = InfrastructureFiles.DockerCompose("BillingService", PersistenceProvider.EfCorePostgres);

        Assert.Contains("pg_isready", compose);
        Assert.Contains("condition: service_healthy", compose);
        // A database is configured, so the api service's own healthcheck should hit the readiness
        // probe (DB-tagged checks), not just liveness — reporting healthy before the app can
        // actually serve a request against its database defeats the point of `depends_on: service_healthy`.
        Assert.Contains("http://localhost:8080/health/ready", compose);
    }

    [Fact]
    public void DockerCompose_SqlServer_AddsSqlcmdHealthCheckAndServiceHealthyDependency()
    {
        var compose = InfrastructureFiles.DockerCompose("BillingService", PersistenceProvider.EfCoreSqlServer);

        Assert.Contains("sqlcmd", compose);
        Assert.Contains("condition: service_healthy", compose);
    }

    [Fact]
    public void DockerCompose_Marten_AlsoGetsPostgresHealthCheck()
    {
        var compose = InfrastructureFiles.DockerCompose("BillingService", PersistenceProvider.Marten);

        Assert.Contains("pg_isready", compose);
        Assert.Contains("condition: service_healthy", compose);
    }
}
