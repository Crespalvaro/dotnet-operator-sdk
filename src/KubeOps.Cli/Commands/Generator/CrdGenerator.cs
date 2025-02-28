﻿using System.CommandLine;
using System.CommandLine.Help;
using System.CommandLine.Invocation;

using KubeOps.Abstractions.Kustomize;
using KubeOps.Cli.Output;
using KubeOps.Cli.Roslyn;

using Spectre.Console;

namespace KubeOps.Cli.Commands.Generator;

internal static class CrdGenerator
{
    public static Command Command
    {
        get
        {
            var cmd = new Command("crd", "Generates CRDs for Kubernetes based on a solution or project.")
            {
                Options.OutputFormat,
                Options.OutputPath,
                Options.SolutionProjectRegex,
                Options.TargetFramework,
                Arguments.SolutionOrProjectFile,
            };
            cmd.AddAlias("crds");
            cmd.AddAlias("c");
            cmd.SetHandler(ctx => Handler(AnsiConsole.Console, ctx));

            return cmd;
        }
    }

    internal static async Task Handler(IAnsiConsole console, InvocationContext ctx)
    {
        var file = ctx.ParseResult.GetValueForArgument(Arguments.SolutionOrProjectFile);
        var outPath = ctx.ParseResult.GetValueForOption(Options.OutputPath);
        var format = ctx.ParseResult.GetValueForOption(Options.OutputFormat);

        var parser = file.Extension switch
        {
            ".csproj" => await AssemblyParser.ForProject(console, file),
            ".sln" => await AssemblyParser.ForSolution(
                console,
                file,
                ctx.ParseResult.GetValueForOption(Options.SolutionProjectRegex),
                ctx.ParseResult.GetValueForOption(Options.TargetFramework)),
            _ => throw new NotSupportedException("Only *.csproj and *.sln files are supported."),
        };
        var result = new ResultOutput(console, format);

        console.WriteLine($"Generate CRDs for {file.Name}.");
        var crds = Transpiler.Crds.Transpile(parser.Entities()).ToList();
        foreach (var crd in crds)
        {
            result.Add($"{crd.Metadata.Name.Replace('.', '_')}.{format.ToString().ToLowerInvariant()}", crd);
        }

        result.Add(
            $"kustomization.{format.ToString().ToLowerInvariant()}",
            new KustomizationConfig
            {
                Resources = crds
                    .ConvertAll(crd => $"{crd.Metadata.Name.Replace('.', '_')}.{format.ToString().ToLower()}"),
                CommonLabels = new Dictionary<string, string> { { "operator-element", "crd" } },
            });

        if (outPath is not null)
        {
            await result.Write(outPath);
        }
        else
        {
            result.Write();
        }

        ctx.ExitCode = ExitCodes.Success;
    }
}
