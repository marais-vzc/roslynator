﻿// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp.Syntax;

namespace Roslynator.CSharp.Analysis;

internal static class AvoidBoxingOfValueTypeAnalysis
{
    public static void Analyze(SyntaxNodeAnalysisContext context, in SimpleMemberInvocationExpressionInfo invocationInfo)
    {
        IMethodSymbol methodSymbol = context.SemanticModel.GetMethodSymbol(invocationInfo.InvocationExpression, context.CancellationToken);

        if (methodSymbol is null)
            return;

        if (methodSymbol.IsExtensionMethod)
            return;

        if (methodSymbol.ContainingType?.HasMetadataName(MetadataNames.System_Text_StringBuilder) != true)
            return;

        ImmutableArray<IParameterSymbol> parameters = methodSymbol.Parameters;

        switch (parameters.Length)
        {
            case 1:
            {
                if (methodSymbol.IsName("Append"))
                {
                    ArgumentSyntax argument = invocationInfo.Arguments.SingleOrDefault(shouldThrow: false);

                    if (argument is not null)
                    {
                        ExpressionSyntax expression = argument.Expression;

                        if (!expression.IsKind(SyntaxKind.InterpolatedStringExpression, SyntaxKind.AddExpression)
                            && parameters[0].Type.IsObject()
                            && context.SemanticModel.GetTypeSymbol(expression, context.CancellationToken)?.IsValueType == true)
                        {
                            DiagnosticHelpers.ReportDiagnostic(context, DiagnosticRules.AvoidBoxingOfValueType, argument);
                            return;
                        }
                    }
                }

                break;
            }
            case 2:
            {
                if (methodSymbol.IsName("Insert")
                    && parameters[0].Type.SpecialType == SpecialType.System_Int32
                    && parameters[1].Type.SpecialType == SpecialType.System_Object)
                {
                    SeparatedSyntaxList<ArgumentSyntax> arguments = invocationInfo.Arguments;

                    if (arguments.Count == 2
                        && context.SemanticModel
                            .GetTypeSymbol(arguments[1].Expression, context.CancellationToken)
                            .IsValueType)
                    {
                        DiagnosticHelpers.ReportDiagnostic(context, DiagnosticRules.AvoidBoxingOfValueType, arguments[1]);
                    }
                }

                break;
            }
        }
    }
}
