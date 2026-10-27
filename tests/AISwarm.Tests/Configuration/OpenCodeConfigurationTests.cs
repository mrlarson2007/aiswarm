using AISwarm.Infrastructure.OpenCode;
using AISwarm.Server.Delegation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;

namespace AISwarm.Tests.Configuration;

public class OpenCodeConfigurationTests
{
    [Fact]
    public async Task WhenConfiguringDelegation_ShouldManageTheSameSupervisorAsAHostedService()
    {
        var services = new ServiceCollection();
        services.AddOpenCodeDelegation(new ConfigurationBuilder().Build());
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IOpenCodeCli>().ShouldBeOfType<OpenCodeCli>();
        provider.GetServices<IHostedService>().ShouldContain(provider.GetRequiredService<DelegationService>());
        provider.GetRequiredService<LocalLifecycleHook>().ShouldNotBeNull();
    }
}
