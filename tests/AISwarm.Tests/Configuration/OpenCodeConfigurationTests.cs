using AISwarm.Infrastructure.OpenCode;
using AISwarm.Infrastructure;
using AISwarm.Server.Delegation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Moq;

namespace AISwarm.Tests.Configuration;

public class OpenCodeConfigurationTests
{
    [Fact]
    public async Task WhenConfiguringDelegation_ShouldShareNeutralExecutorWithShutdownDrainingService()
    {
        var services = new ServiceCollection();
        services.AddOpenCodeDelegation(new ConfigurationBuilder().Build());
        services.AddSingleton(Mock.Of<IHostApplicationLifetime>());
        await using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IAgentService>().ShouldBeOfType<OpenCodeCli>();
        provider.GetServices<IHostedService>().ShouldContain(provider.GetRequiredService<DelegationService>());
    }
}
