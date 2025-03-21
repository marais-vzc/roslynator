﻿// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslynator.CSharp;

internal static class CSharpOverriddenSymbolInfo
{
    private static OverriddenSymbolInfo Default { get; } = new();

    public static bool CanCreate(SyntaxNode node)
    {
        switch (node?.Kind())
        {
            case SyntaxKind.MethodDeclaration:
            case SyntaxKind.PropertyDeclaration:
            case SyntaxKind.IndexerDeclaration:
            case SyntaxKind.EventDeclaration:
            case SyntaxKind.GetAccessorDeclaration:
            case SyntaxKind.SetAccessorDeclaration:
            case SyntaxKind.AddAccessorDeclaration:
            case SyntaxKind.RemoveAccessorDeclaration:
            {
                return true;
            }
            case SyntaxKind.VariableDeclarator:
            {
                return node.IsParentKind(SyntaxKind.VariableDeclaration)
                    && node.Parent.IsParentKind(SyntaxKind.EventFieldDeclaration);
            }
        }

        return false;
    }

    internal static OverriddenSymbolInfo Create(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken = default)
    {
        if (node is null)
            return Default;

        if (semanticModel is null)
            throw new ArgumentNullException(nameof(semanticModel));

        switch (node)
        {
            case MethodDeclarationSyntax methodDeclaration:
                return CreateImpl(methodDeclaration, semanticModel, cancellationToken);
            case PropertyDeclarationSyntax propertyDeclaration:
                return CreateImpl(propertyDeclaration, semanticModel, cancellationToken);
            case IndexerDeclarationSyntax indexerDeclaration:
                return CreateImpl(indexerDeclaration, semanticModel, cancellationToken);
            case EventDeclarationSyntax eventDeclaration:
                return CreateImpl(eventDeclaration, semanticModel, cancellationToken);
            case VariableDeclaratorSyntax variableDeclarator:
                return CreateImpl(variableDeclarator, semanticModel, cancellationToken);
            case AccessorDeclarationSyntax accessorDeclaration:
                return CreateImpl(accessorDeclaration, semanticModel, cancellationToken);
            default:
                return Default;
        }
    }

    private static OverriddenSymbolInfo CreateImpl(
        MethodDeclarationSyntax methodDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        IMethodSymbol? methodSymbol = semanticModel.GetDeclaredSymbol(methodDeclaration, cancellationToken);

        if (methodSymbol is null)
            return Default;

        IMethodSymbol? overriddenMethod = methodSymbol.OverriddenMethod;

        if (overriddenMethod is null)
            return Default;

        return new OverriddenSymbolInfo(methodSymbol, overriddenMethod);
    }

    private static OverriddenSymbolInfo CreateImpl(
        BasePropertyDeclarationSyntax basePropertyDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var propertySymbol = (IPropertySymbol?)semanticModel.GetDeclaredSymbol(basePropertyDeclaration, cancellationToken);

        if (propertySymbol is null)
            return Default;

        IPropertySymbol? overriddenProperty = propertySymbol.OverriddenProperty;

        if (overriddenProperty is null)
            return Default;

        return new OverriddenSymbolInfo(propertySymbol, overriddenProperty);
    }

    private static OverriddenSymbolInfo CreateImpl(
        EventDeclarationSyntax eventDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        IEventSymbol? eventSymbol = semanticModel.GetDeclaredSymbol(eventDeclaration, cancellationToken);

        if (eventSymbol is null)
            return Default;

        IEventSymbol? overriddenEvent = eventSymbol.OverriddenEvent;

        if (overriddenEvent is null)
            return Default;

        return new OverriddenSymbolInfo(eventSymbol, overriddenEvent);
    }

    private static OverriddenSymbolInfo CreateImpl(
        VariableDeclaratorSyntax variableDeclarator,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (!variableDeclarator.IsParentKind(SyntaxKind.VariableDeclaration))
            return Default;

        if (!variableDeclarator.Parent.IsParentKind(SyntaxKind.EventFieldDeclaration))
            return Default;

        if (semanticModel.GetDeclaredSymbol(variableDeclarator, cancellationToken) is not IEventSymbol eventSymbol)
            return Default;

        IEventSymbol? overriddenEvent = eventSymbol.OverriddenEvent;

        if (overriddenEvent is null)
            return Default;

        return new OverriddenSymbolInfo(eventSymbol, overriddenEvent);
    }

    private static OverriddenSymbolInfo CreateImpl(
        AccessorDeclarationSyntax accessorDeclaration,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        IMethodSymbol? methodSymbol = semanticModel.GetDeclaredSymbol(accessorDeclaration, cancellationToken);

        if (methodSymbol is null)
            return Default;

        IMethodSymbol? overriddenMethod = methodSymbol.OverriddenMethod;

        if (overriddenMethod is null)
            return Default;

        return new OverriddenSymbolInfo(methodSymbol, overriddenMethod);
    }
}
