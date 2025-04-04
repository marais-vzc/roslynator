﻿// Copyright (c) .NET Foundation and Contributors. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Roslynator.CSharp.CodeStyle;
using Roslynator.CSharp.Syntax;
using static Roslynator.CSharp.Analysis.ConvertHasFlagCallToBitwiseOperationAnalysis;

namespace Roslynator.CSharp.Analysis;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class UseHasFlagMethodOrBitwiseOperatorAnalyzer : BaseDiagnosticAnalyzer
{
    private static ImmutableArray<DiagnosticDescriptor> _supportedDiagnostics;

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
    {
        get
        {
            if (_supportedDiagnostics.IsDefault)
            {
                Immutable.InterlockedInitialize(ref _supportedDiagnostics, DiagnosticRules.UseHasFlagMethodOrBitwiseOperator);
            }

            return _supportedDiagnostics;
        }
    }

    public override void Initialize(AnalysisContext context)
    {
        base.Initialize(context);

        context.RegisterSyntaxNodeAction(
            c =>
            {
                EnumFlagOperationStyle style = c.GetEnumHasFlagStyle();

                if (style == EnumFlagOperationStyle.HasFlagMethod)
                    AnalyzeBitwiseAndExpression(c);
            },
            SyntaxKind.BitwiseAndExpression);

        context.RegisterSyntaxNodeAction(
            c =>
            {
                EnumFlagOperationStyle style = c.GetEnumHasFlagStyle();

                if (style == EnumFlagOperationStyle.BitwiseOperator)
                    AnalyzeInvocationExpression(c);
            },
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeBitwiseAndExpression(SyntaxNodeAnalysisContext context)
    {
        var bitwiseAnd = (BinaryExpressionSyntax)context.Node;

        ExpressionSyntax expression = bitwiseAnd.WalkUpParentheses();

        if (!expression.IsParentKind(SyntaxKind.EqualsExpression, SyntaxKind.NotEqualsExpression))
            return;

        var equalsOrNotEquals = (BinaryExpressionSyntax)expression.Parent;

        ExpressionSyntax otherExpression = (ReferenceEquals(equalsOrNotEquals.Left, expression))
            ? equalsOrNotEquals.Right
            : equalsOrNotEquals.Left;

        otherExpression = otherExpression.WalkDownParentheses();

        ExpressionSyntax right = bitwiseAnd.Right.WalkDownParentheses();

        SemanticModel semanticModel = context.SemanticModel;
        CancellationToken cancellationToken = context.CancellationToken;

        if (otherExpression.IsNumericLiteralExpression("0"))
        {
            var enumTypeSymbol = (INamedTypeSymbol)semanticModel.GetTypeSymbol(right, cancellationToken);

            if (enumTypeSymbol?.EnumUnderlyingType is not null)
            {
                Optional<object> constantValue = semanticModel.GetConstantValue(right, cancellationToken);

                if (!constantValue.HasValue)
                    return;

                ulong value = SymbolUtility.GetEnumValueAsUInt64(constantValue.Value!, enumTypeSymbol);

                if (FlagsUtility<ulong>.Instance.IsComposite(value))
                    return;
            }
        }
        else if (!CSharpFactory.AreEquivalent(right, otherExpression))
        {
            return;
        }

        IMethodSymbol methodSymbol = semanticModel.GetMethodSymbol(bitwiseAnd, cancellationToken);

        if (methodSymbol?.MethodKind != MethodKind.BuiltinOperator
            || methodSymbol.Name != WellKnownMemberNames.BitwiseAndOperatorName
            || methodSymbol.ContainingType?.TypeKind != TypeKind.Enum)
        {
            return;
        }

        ExpressionSyntax left = bitwiseAnd.Left.WalkDownParentheses();

        if (!IsSuitableAsExpressionOfHasFlag(left))
            return;

        if (!IsSuitableAsArgumentOfHasFlag(right))
            return;

        DiagnosticHelpers.ReportDiagnostic(
            context,
            DiagnosticRules.UseHasFlagMethodOrBitwiseOperator,
            equalsOrNotEquals,
            "'HasFlag' method");

        bool IsSuitableAsExpressionOfHasFlag(ExpressionSyntax expression)
        {
            if (expression.IsKind(
                SyntaxKind.IdentifierName,
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxKind.InvocationExpression,
                SyntaxKind.ElementAccessExpression))
            {
                return semanticModel.GetTypeSymbol(expression, cancellationToken)?.TypeKind == TypeKind.Enum;
            }

            return false;
        }

        bool IsSuitableAsArgumentOfHasFlag(ExpressionSyntax expression)
        {
            expression = expression.WalkDownParentheses();

            if (expression.IsKind(
                SyntaxKind.BitwiseAndExpression,
                SyntaxKind.BitwiseOrExpression,
                SyntaxKind.SimpleMemberAccessExpression,
                SyntaxKind.IdentifierName))
            {
                return semanticModel.GetTypeSymbol(expression, cancellationToken)?.TypeKind == TypeKind.Enum;
            }

            return false;
        }
    }

    private static void AnalyzeInvocationExpression(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.ContainsDiagnostics)
            return;

        if (invocation.SpanContainsDirectives())
            return;

        SimpleMemberInvocationExpressionInfo invocationInfo = SyntaxInfo.SimpleMemberInvocationExpressionInfo(invocation);

        if (invocationInfo.Arguments.Count != 1)
            return;

        if (!invocationInfo.Name.IsKind(SyntaxKind.IdentifierName))
            return;

        if (invocationInfo.NameText != "HasFlag")
            return;

        if (!IsFixable(invocationInfo, context.SemanticModel, context.CancellationToken))
            return;

        DiagnosticHelpers.ReportDiagnostic(
            context,
            DiagnosticRules.UseHasFlagMethodOrBitwiseOperator,
            invocation,
            "bitwise operator");
    }
}
