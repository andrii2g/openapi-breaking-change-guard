using OpenApiBreakingChangeGuard;

if (args is ["--help"] or ["-h"])
{
    Console.WriteLine(
        $"""
        {ProjectMarker.ProductName}

        Implementation scaffold. Follow PLAN.md to implement the CLI contract.

        Intended usage:
          openapi-guard <old-spec> <new-spec> [options]
        """);

    return ExitCodes.Success;
}

Console.Error.WriteLine(
    "The compatibility engine is not implemented yet. Follow PLAN.md in repository order.");

return ExitCodes.InvalidInput;

