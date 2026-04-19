namespace PicoCfg.Gen;

// Discovers supported entry points and maps invocations to bind operations.
public sealed partial class PicoCfgBindGenerator
{
    private static bool IsCandidateInvocation(SyntaxNode node)
    {
        if (node is not InvocationExpressionSyntax invocation)
            return false;

        return invocation.Expression switch
        {
            MemberAccessExpressionSyntax { Name: { } simpleName }
                => IsTargetMethodName(simpleName.Identifier.ValueText),
            SimpleNameSyntax simpleName => IsTargetMethodName(simpleName.Identifier.ValueText),
            _ => false,
        };
    }

    private static BindCall? TransformInvocation(GeneratorSyntaxContext context)
    {
        if (context.Node is not InvocationExpressionSyntax invocation)
            return null;

        if (context.SemanticModel.Compilation.AssemblyName == "PicoCfg.Gen")
            return null;

        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return null;

        if (!TryGetOperation(method, out var operation))
            return null;

        if (method.TypeArguments.Length != 1)
            return null;

        return new BindCall(method.TypeArguments[0], operation, invocation.GetLocation());
    }

    private static bool TryGetOperation(IMethodSymbol method, out BindOperation operation)
    {
        operation = default;
        if (!method.IsGenericMethod || method.TypeArguments.Length != 1)
            return false;

        if (
            (method.ContainingType.Name == "PicoCfgBind" || method.ContainingType.Name == "CfgBind")
            && method.ContainingType.ContainingNamespace.ToDisplayString() == "PicoCfg"
        )
        {
            switch (method.Name)
            {
                case "Bind":
                    operation = BindOperation.Bind;
                    return true;
                case "TryBind":
                    operation = BindOperation.TryBind;
                    return true;
                case "BindInto":
                    operation = BindOperation.BindInto;
                    return true;
                default:
                    return false;
            }
        }

        if (
            method.ContainingType.Name != "SvcContainerExtensions"
            || method.ContainingType.ContainingNamespace.ToDisplayString() != "PicoCfg.DI"
        )
            return false;
        switch (method.Name)
        {
            case "RegisterCfgTransient":
            case "RegisterCfgScoped":
            case "RegisterCfgSingleton":
            case "RegisterPicoCfgTransient":
            case "RegisterPicoCfgScoped":
            case "RegisterPicoCfgSingleton":
                operation = BindOperation.Bind;
                return true;
        }

        return false;
    }

    private static bool IsTargetMethodName(string methodName) =>
        methodName
            is "Bind"
                or "TryBind"
                or "BindInto"
                or "RegisterCfgTransient"
                or "RegisterCfgScoped"
                or "RegisterCfgSingleton"
                or "RegisterPicoCfgTransient"
                or "RegisterPicoCfgScoped"
                or "RegisterPicoCfgSingleton";
}
