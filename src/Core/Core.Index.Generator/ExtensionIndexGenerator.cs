using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Core.Index.Generator;

[Generator(LanguageNames.CSharp)]
public class ExtensionIndexGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        if (!Debugger.IsAttached)
        {
            //Debugger.Launch();
        }
        context.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddEmbeddedAttributeDefinition();
            postInitializationContext.AddSource(
                hintName: "ExtensionIndexAttribute.g.cs",
                source: SourceText.From(
                    """
                    using System;
                    using Microsoft.CodeAnalysis;

                    namespace Core.Abstractions;

                    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false), Embedded]
                    internal sealed class ExtensionIndexAttribute : Attribute
                    {
                        public ExtensionIndexAttribute()
                        {
                        }
                    }
                    """,
                    encoding: Encoding.UTF8
                ).ToString()
            );
        });

        var commandCollectionPipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "Core.Abstractions.CommandInfoAttribute",
            predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            transform: static (context, _) =>
            {
                var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;
                var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
                var namespaceSymbol = classSymbol.ContainingNamespace;

                var commandInfoAttributeData = classSymbol.GetAttributes()
                    .First(ad => ad.AttributeClass?.ToDisplayString() == "Core.Abstractions.CommandInfoAttribute");

                var name = commandInfoAttributeData.NamedArguments
                    .FirstOrDefault(kv => kv.Key == "Name").Value.Value as string ?? "";

                var description = commandInfoAttributeData.NamedArguments
                    .FirstOrDefault(kv => kv.Key == "Description").Value.Value as string ?? "";

                return new
                {
                    NamespaceName = namespaceSymbol.ToDisplayString(),
                    ClassName = classSymbol.ToDisplayParts().Last().ToString(),
                    Name = name,
                    Description = description
                };
            }
        ).Collect();

        var extensionDiscoveryPipeline = context.SyntaxProvider.ForAttributeWithMetadataName(
            fullyQualifiedMetadataName: "Core.Abstractions.ExtensionIndexAttribute",
            predicate: static (syntaxNode, _) => syntaxNode is ClassDeclarationSyntax,
            transform: static (context, _) =>
            {
                var classDeclaration = (ClassDeclarationSyntax)context.TargetNode;
                var classSymbol = (INamedTypeSymbol)context.TargetSymbol;
                var namespaceSymbol = classSymbol.ContainingNamespace;

                var containingAssembly = classSymbol.ContainingAssembly;

                return new
                {
                    AssemblyName = containingAssembly.Name,
                    NamespaceName = namespaceSymbol.ToDisplayString(),
                    ClassName = classSymbol.ToDisplayParts().Last().ToString()
                };
            }
        );

        var combinedExtensionDiscoveryPipeline = extensionDiscoveryPipeline
            .Combine(commandCollectionPipeline)
            .Select((pair, cancellationToken) =>
            {
                var (extensionInfo, commands) = pair;
                return new
                {
                    ExtensionInfo = extensionInfo,
                    Commands = commands
                };
            });

        context.RegisterSourceOutput(combinedExtensionDiscoveryPipeline, static (sourceProductionContext, data) =>
        {
            var dedupedNamespace = data.Commands
                .Select(cmd => cmd.NamespaceName)
                .Distinct()
                .OrderBy(ns => ns)
                .ToList();
            var cliTableEntries = string.Join("\n        ", data.Commands.Select(cmd =>
                $"cliTable.Add(\"{cmd.Name}\", new CommandMetadata(\n" +
                $"            name: \"{cmd.ClassName}\",\n" +
                $"            fullName: \"{cmd.NamespaceName}.{cmd.ClassName}\",\n" +
                $"            assemblyName: \"{data.ExtensionInfo.AssemblyName}\",\n" +
                $"            commandType: typeof({cmd.ClassName})\n" +
                $"        ));"));
            var sourceText = SourceText.From(
                $$"""
                // <auto-generated />
                using System;
                using System.Collections.Generic;
                using Core.Abstractions;
                {{string.Join("\n", dedupedNamespace.Select(ns => $"using {ns};"))}}

                namespace {{data.ExtensionInfo.NamespaceName}};
                public partial class {{data.ExtensionInfo.ClassName}}
                {
                    public void FillCliTable(IDictionary<string, ICommandMetadata> cliTable)
                    {
                        {{cliTableEntries}}
                    }
                }
                """,
                encoding: Encoding.UTF8
            );
            sourceProductionContext.AddSource(
                hintName: $"{data.ExtensionInfo.ClassName}_ExtensionIndex.g.cs",
                source: sourceText.ToString()
            );
        });
    }
}
