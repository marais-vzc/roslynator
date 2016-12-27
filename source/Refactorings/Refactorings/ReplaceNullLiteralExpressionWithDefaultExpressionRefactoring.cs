﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Roslynator.CSharp.Refactorings
{
    internal static class ReplaceNullLiteralExpressionWithDefaultExpressionRefactoring
    {
        public static async Task ComputeRefactoringAsync(RefactoringContext context, ExpressionSyntax expression)
        {
            if (expression?.IsKind(SyntaxKind.NullLiteralExpression) == true
                && context.Span.IsContainedInSpanOrBetweenSpans(expression))
            {
                TextSpan span = context.Span;

                if ((span.IsEmpty && expression.Span.Contains(span))
                    || span.IsBetweenSpans(expression))
                {
                    SemanticModel semanticModel = await context.GetSemanticModelAsync().ConfigureAwait(false);

                    ITypeSymbol typeSymbol = semanticModel.GetTypeInfo(expression).ConvertedType;

                    if (typeSymbol != null
                        && SymbolAnalyzer.SupportsExplicitDeclaration(typeSymbol))
                    {
                        context.RegisterRefactoring(
                            $"Replace 'null' with 'default({typeSymbol.ToMinimalDisplayString(semanticModel, expression.Span.Start, DefaultSymbolDisplayFormat.Value)})'",
                            cancellationToken =>
                            {
                                return RefactorAsync(
                                    context.Document,
                                    expression,
                                    typeSymbol,
                                    cancellationToken);
                            });
                    }
                }
            }
        }

        public static async Task<Document> RefactorAsync(
            Document document,
            ExpressionSyntax expression,
            ITypeSymbol typeSymbol,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            SemanticModel semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

            TypeSyntax type = CSharpFactory.Type(typeSymbol, semanticModel, expression.SpanStart);

            DefaultExpressionSyntax defaultExpression = SyntaxFactory.DefaultExpression(type)
                .WithTriviaFrom(expression);

            return await document.ReplaceNodeAsync(expression, defaultExpression, cancellationToken).ConfigureAwait(false);
        }
    }
}

