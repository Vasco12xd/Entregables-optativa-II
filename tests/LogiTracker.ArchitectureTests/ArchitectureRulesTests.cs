using System.Reflection;
using LogiTracker.Api.Controllers;
using LogiTracker.Application.Deliveries.AssignCourier;
using LogiTracker.Domain.Deliveries;
using LogiTracker.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Xunit.Abstractions;

namespace LogiTracker.ArchitectureTests;

/// <summary>
/// Fitness functions (video 27): convierten las reglas de la arquitectura en comprobaciones que fallan
/// si un cambio las rompe. Protegen las decisiones del ADR-002 (D-01, D-02) y los criterios CV-04 y CV-05.
/// </summary>
public sealed class ArchitectureRulesTests
{
    private static readonly Assembly Domain = typeof(Delivery).Assembly;
    private static readonly Assembly Application = typeof(AssignCourierUseCase).Assembly;
    private static readonly Assembly Infrastructure = typeof(DependencyInjection).Assembly;
    private static readonly Assembly Api = typeof(DeliveriesController).Assembly;

    private static readonly string[] FrameworkAndDatabasePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Microsoft.AspNetCore",
    ];

    private readonly ITestOutputHelper _output;

    public ArchitectureRulesTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Domain_depends_on_nothing_but_the_base_class_library()
    {
        var forbidden = References(Domain)
            .Where(name => name.StartsWith("LogiTracker.", StringComparison.Ordinal)
                        || name.StartsWith("Microsoft.Extensions", StringComparison.Ordinal)
                        || FrameworkAndDatabasePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.True(forbidden.Count == 0, $"Domain must not reference: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Application_depends_only_on_the_domain_and_never_on_infrastructure_or_frameworks()
    {
        var forbidden = References(Application)
            .Where(name => name is "LogiTracker.Infrastructure" or "LogiTracker.Api"
                        || FrameworkAndDatabasePrefixes.Any(prefix => name.StartsWith(prefix, StringComparison.Ordinal)))
            .ToList();

        Assert.True(forbidden.Count == 0, $"Application must not reference: {string.Join(", ", forbidden)}");
    }

    [Fact]
    public void Infrastructure_does_not_depend_on_the_entry_layer()
    {
        Assert.DoesNotContain("LogiTracker.Api", References(Infrastructure));
    }

    [Fact]
    public void Use_cases_receive_only_abstractions_through_their_constructor()
    {
        var useCases = Application.GetTypes().Where(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal) || t.Name.EndsWith("Query", StringComparison.Ordinal));

        var concreteDependencies =
            from useCase in useCases
            from constructor in useCase.GetConstructors()
            from parameter in constructor.GetParameters()
            let type = parameter.ParameterType
            where !type.IsInterface && !type.Name.EndsWith("Policy", StringComparison.Ordinal)
            select $"{useCase.Name}({type.Name})";

        Assert.Empty(concreteDependencies);
    }

    [Fact]
    public void Domain_state_cannot_be_changed_from_outside_the_aggregate()
    {
        var publicSetters =
            from type in Domain.GetTypes()
            where type.IsClass && type.Namespace == typeof(Delivery).Namespace
            from property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            where property.SetMethod is { IsPublic: true } && !IsInitOnly(property)
            select $"{type.Name}.{property.Name}";

        Assert.Empty(publicSetters);
    }

    [Fact]
    public void Controllers_talk_to_use_cases_and_never_to_infrastructure_adapters()
    {
        var controllers = Api.GetTypes().Where(t => t.IsSubclassOf(typeof(ControllerBase)));

        var leaks =
            from controller in controllers
            from method in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            from parameter in method.GetParameters()
            where parameter.ParameterType.Assembly == Infrastructure
            select $"{controller.Name}.{method.Name}({parameter.ParameterType.Name})";

        Assert.Empty(leaks);
    }

    /// <summary>
    /// Métricas cuantitativas de la arquitectura (video 13). No fallan: registran la línea base
    /// para comparar cuando el sistema evolucione.
    /// </summary>
    [Fact]
    public void Architecture_metrics_baseline()
    {
        var ports = Application.GetTypes().Count(t => t.IsInterface && t.IsPublic);
        var useCases = Application.GetTypes().Count(t => t.Name.EndsWith("UseCase", StringComparison.Ordinal));
        var domainTypes = Domain.GetTypes().Count(t => t.IsPublic);
        var adapters = Infrastructure.GetTypes().Count(t => t.IsClass && t.GetInterfaces().Any(i => i.Assembly == Application));
        var domainExternalReferences = References(Domain).Count(name => !name.StartsWith("System", StringComparison.Ordinal) && name != "netstandard");

        _output.WriteLine($"Tipos públicos del dominio: {domainTypes}");
        _output.WriteLine($"Puertos (interfaces) en aplicación: {ports}");
        _output.WriteLine($"Casos de uso: {useCases}");
        _output.WriteLine($"Adaptadores que implementan puertos: {adapters}");
        _output.WriteLine($"Referencias externas del dominio: {domainExternalReferences}");

        Assert.Equal(0, domainExternalReferences);
    }

    private static List<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

    private static bool IsInitOnly(PropertyInfo property) =>
        property.SetMethod!.ReturnParameter.GetRequiredCustomModifiers()
            .Any(m => m.FullName == "System.Runtime.CompilerServices.IsExternalInit");
}
