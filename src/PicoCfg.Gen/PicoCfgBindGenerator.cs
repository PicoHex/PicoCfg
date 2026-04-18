namespace PicoCfg.Gen.Generator;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

[Generator(LanguageNames.CSharp)]
public sealed partial class PicoCfgBindGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var calls = context.SyntaxProvider
            .CreateSyntaxProvider(static (node, _) => IsCandidateInvocation(node), static (ctx, _) => TransformInvocation(ctx))
            .Where(static call => call is not null)
            .Select(static (call, _) => call!);

        context.RegisterSourceOutput(calls.Collect(), static (spc, collectedCalls) => Execute(spc, collectedCalls));
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<BindCall> calls)
    {
        if (calls.IsDefaultOrEmpty)
            return;

        var targets = new Dictionary<ITypeSymbol, TargetRegistration>(SymbolEqualityComparer.Default);

        foreach (var call in calls)
        {
            if (!targets.TryGetValue(call.TargetType, out var registration))
            {
                registration = new TargetRegistration(call.TargetType);
                targets.Add(call.TargetType, registration);
            }

            registration.Operations |= call.Operation;
            registration.Locations.Add(call.Location);
        }

        var validTargets = new List<TargetModel>(targets.Count);
        foreach (var registration in targets.Values)
        {
            if (!TryAnalyzeTarget(context, registration, out var model))
                continue;

            validTargets.Add(model);
        }

        if (validTargets.Count == 0)
            return;

        context.AddSource("PicoCfgBindRegistrations.g.cs", SourceText.From(Render(validTargets), Encoding.UTF8));
    }
}
