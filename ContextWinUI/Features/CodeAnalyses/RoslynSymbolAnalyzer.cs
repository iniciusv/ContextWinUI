using ContextWinUI.Core.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ContextWinUI.Features.CodeAnalyses;

public class RoslynSymbolAnalyzer
{
    private readonly DependencyGraph _graph;

    public RoslynSymbolAnalyzer(DependencyGraph graph)
    {
        _graph = graph;
    }

    public async Task<SymbolNode?> ResolveSymbolWithRoslynAsync(CSharpCompilation compilation, string filePath, int absolutePosition)
    {
        if (compilation == null) return null;

        var tree = compilation.SyntaxTrees.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));

        if (tree == null) return null;

        return await Task.Run(() =>
        {
            try
            {
                var root = tree.GetRoot();
                var token = root.FindToken(absolutePosition);
                var node = token.Parent;

                if (node == null) return null;

                var model = compilation.GetSemanticModel(tree);
                var symbolInfo = model.GetSymbolInfo(node);
                var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

                if (symbol != null)
                {
                    if (!symbol.IsDefinition)
                    {
                        symbol = symbol.OriginalDefinition;
                    }

                    if (symbol.Locations.Length > 0 && symbol.Locations[0].IsInSource)
                    {
                        var loc = symbol.Locations[0];
                        var sourceTree = loc.SourceTree;
                        var sourcePath = sourceTree?.FilePath;

                        if (!string.IsNullOrEmpty(sourcePath))
                        {
                            int fileId = _graph.GetOrAddFileId(sourcePath);
                            SymbolType myType = MapSymbolKind(symbol);

                            return new SymbolNode(0, fileId)
                            {
                                Name = symbol.Name,
                                Type = myType,
                                StartPosition = loc.SourceSpan.Start,
                                Length = loc.SourceSpan.Length
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in ResolveSymbolWithRoslynAsync: {ex.Message}");
            }

            return null;
        });
    }

    public async Task<List<SymbolNode>> FindImplementationsWithRoslynAsync(CSharpCompilation compilation, string filePath, int absolutePosition)
    {
        var results = new List<SymbolNode>();
        if (compilation == null) return results;

        var tree = compilation.SyntaxTrees.FirstOrDefault(t =>
            string.Equals(t.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        if (tree == null) return results;

        await Task.Run(() =>
        {
            try
            {
                var root = tree.GetRoot();
                var token = root.FindToken(absolutePosition);
                var node = token.Parent;
                if (node == null) return;

                var model = compilation.GetSemanticModel(tree);
                var symbolInfo = model.GetSymbolInfo(node);
                var symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

                if (symbol == null) return;

                ITypeSymbol? targetType = null;

                if (symbol is ITypeSymbol ts)
                {
                    targetType = ts;
                }
                else if (symbol is IFieldSymbol fs)
                {
                    targetType = fs.Type;
                }
                else if (symbol is IPropertySymbol ps)
                {
                    targetType = ps.Type;
                }
                else if (symbol is IParameterSymbol pars)
                {
                    targetType = pars.Type;
                }
                else if (symbol is ILocalSymbol ls)
                {
                    targetType = ls.Type;
                }

                if (targetType != null)
                {
                    if (targetType.TypeKind == TypeKind.Interface && targetType is INamedTypeSymbol namedInterface)
                    {
                        FindImplementationsOfInterface(compilation, namedInterface, results);
                        return;
                    }
                }

                symbol = symbol.OriginalDefinition;

                if (symbol.Kind == SymbolKind.NamedType && symbol is INamedTypeSymbol namedType2 && namedType2.TypeKind == TypeKind.Interface)
                {
                    FindImplementationsOfInterface(compilation, namedType2, results);
                }
                else if (symbol.ContainingType != null && symbol.ContainingType.TypeKind == TypeKind.Interface)
                {
                    FindImplementationsOfInterfaceMember(compilation, symbol, results);
                }
                else if (symbol.IsVirtual || symbol.IsAbstract || symbol.IsOverride)
                {
                    FindOverridesOfMember(compilation, symbol, results);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in FindImplementations: {ex.Message}");
            }
        });

        return results;
    }

    private void FindImplementationsOfInterface(CSharpCompilation compilation, INamedTypeSymbol interfaceSymbol, List<SymbolNode> results)
    {
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in types)
            {
                var typeSymbol = model.GetDeclaredSymbol(typeDecl);
                if (typeSymbol != null && typeSymbol.AllInterfaces.Contains(interfaceSymbol, SymbolEqualityComparer.Default))
                {
                    AddSymbolToResults(typeSymbol, results);
                }
            }
        }
    }

    private void FindImplementationsOfInterfaceMember(CSharpCompilation compilation, ISymbol interfaceMember, List<SymbolNode> results)
    {
        var interfaceType = interfaceMember.ContainingType;

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in types)
            {
                var typeSymbol = model.GetDeclaredSymbol(typeDecl);
                if (typeSymbol != null && typeSymbol.AllInterfaces.Contains(interfaceType, SymbolEqualityComparer.Default))
                {
                    var implementation = typeSymbol.FindImplementationForInterfaceMember(interfaceMember);
                    if (implementation != null)
                    {
                        AddSymbolToResults(implementation, results);
                    }
                }
            }
        }
    }

    private void FindOverridesOfMember(CSharpCompilation compilation, ISymbol memberSymbol, List<SymbolNode> results)
    {
        var containingType = memberSymbol.ContainingType;

        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            var root = tree.GetRoot();
            var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>();

            foreach (var typeDecl in types)
            {
                var typeSymbol = model.GetDeclaredSymbol(typeDecl);
                if (typeSymbol != null && typeSymbol != containingType && InheritsFrom(typeSymbol, containingType))
                {
                    var member = typeSymbol.GetMembers(memberSymbol.Name).FirstOrDefault(m => m.IsOverride);
                    if (member != null)
                    {
                        AddSymbolToResults(member, results);
                    }
                }
            }
        }
    }

    private bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType)) return true;
            current = current.BaseType;
        }
        return false;
    }

    private void AddSymbolToResults(ISymbol symbol, List<SymbolNode> results)
    {
        if (symbol.Locations.Length > 0 && symbol.Locations[0].IsInSource)
        {
            var loc = symbol.Locations[0];
            var sourcePath = loc.SourceTree?.FilePath;
            if (!string.IsNullOrEmpty(sourcePath))
            {
                int fileId = _graph.GetOrAddFileId(sourcePath);
                results.Add(new SymbolNode(0, fileId)
                {
                    Name = symbol.ContainingType?.Name + "." + symbol.Name,
                    Type = MapSymbolKind(symbol),
                    StartPosition = loc.SourceSpan.Start,
                    Length = loc.SourceSpan.Length
                });
            }
        }
    }

    private SymbolType MapSymbolKind(ISymbol symbol)
    {
        if (symbol == null) return SymbolType.Class;

        if (symbol is INamedTypeSymbol namedType)
        {
            return namedType.TypeKind switch
            {
                TypeKind.Class => SymbolType.Class,
                TypeKind.Interface => SymbolType.Interface,
                TypeKind.Struct => SymbolType.Struct,
                TypeKind.Enum => SymbolType.Enum,
                TypeKind.Delegate => SymbolType.Class,
                _ => SymbolType.Class
            };
        }

        return symbol.Kind switch
        {
            SymbolKind.Method => SymbolType.Method,
            SymbolKind.Property => SymbolType.Property,
            SymbolKind.Field => SymbolType.Field,
            SymbolKind.Event => SymbolType.Property,
            _ => SymbolType.Class
        };
    }
}
