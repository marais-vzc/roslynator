﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Roslynator.CSharp.Refactorings
{
    internal static class RemoveBracesRefactoring
    {
        public static void ComputeRefactoring(RefactoringContext context, StatementSyntax statement)
        {
            BlockSyntax block = null;

            if (statement.IsKind(SyntaxKind.Block))
            {
                block = (BlockSyntax)statement;
            }
            else if (statement.IsParentKind(SyntaxKind.Block))
            {
                block = (BlockSyntax)statement.Parent;
            }

            if (block != null)
            {
                ComputeRefactoring(context, block);
            }
        }

        private static void ComputeRefactoring(RefactoringContext context, BlockSyntax block)
        {
            if (context.IsAnyRefactoringEnabled(
                    RefactoringIdentifiers.RemoveBraces,
                    RefactoringIdentifiers.RemoveBracesFromIfElse)
                && CanRefactor(context, block))
            {
                if (context.IsRefactoringEnabled(RefactoringIdentifiers.RemoveBraces))
                {
                    context.RegisterRefactoring(
                        "Remove braces",
                        cancellationToken => RefactorAsync(context.Document, block, cancellationToken));
                }

                if (context.IsRefactoringEnabled(RefactoringIdentifiers.RemoveBracesFromIfElse))
                {
                    IfStatementSyntax topmostIf = GetTopmostIf(block);

                    if (topmostIf?.Else != null
                        && CanRefactorIfElse(block, topmostIf))
                    {
                        context.RegisterRefactoring(
                            "Remove braces from if-else",
                            cancellationToken =>
                            {
                                return RemoveBracesFromIfElseElseRefactoring.RefactorAsync(
                                    context.Document,
                                    topmostIf,
                                    cancellationToken);
                            });
                    }
                }
            }
        }

        private static bool CanRefactorIfElse(BlockSyntax selectedBlock, IfStatementSyntax topmostIf)
        {
            bool success = false;

            foreach (BlockSyntax block in GetBlocks(topmostIf))
            {
                if (block == selectedBlock)
                {
                    continue;
                }
                else if (CSharpUtility.IsEmbeddableBlock(block))
                {
                    success = true;
                }
                else
                {
                    return false;
                }
            }

            return success;
        }

        private static bool CanRefactor(RefactoringContext context, BlockSyntax block)
        {
            if (context.Span.IsEmptyAndContainedInSpanOrBetweenSpans(block)
                && CSharpUtility.IsEmbeddableBlock(block))
            {
                StatementSyntax statement = CSharpUtility.GetEmbeddedStatement(block.Statements[0]);

                return statement == null
                    || !statement.FullSpan.Contains(context.Span);
            }

            return false;
        }

        private static IEnumerable<BlockSyntax> GetBlocks(IfStatementSyntax topmostIf)
        {
            foreach (SyntaxNode node in IfElseChain.GetChain(topmostIf))
            {
                switch (node.Kind())
                {
                    case SyntaxKind.IfStatement:
                        {
                            var ifStatement = (IfStatementSyntax)node;

                            StatementSyntax statement = ifStatement.Statement;

                            if (statement?.IsKind(SyntaxKind.Block) == true)
                                yield return (BlockSyntax)statement;

                            break;
                        }
                    case SyntaxKind.ElseClause:
                        {
                            var elseClause = (ElseClauseSyntax)node;

                            StatementSyntax statement = elseClause.Statement;

                            if (statement?.IsKind(SyntaxKind.Block) == true)
                                yield return (BlockSyntax)statement;

                            break;
                        }
                }
            }
        }

        private static IfStatementSyntax GetTopmostIf(BlockSyntax block)
        {
            SyntaxNode parent = block.Parent;

            switch (parent?.Kind())
            {
                case SyntaxKind.IfStatement:
                    return IfElseChain.GetTopmostIf((IfStatementSyntax)parent);
                case SyntaxKind.ElseClause:
                    return IfElseChain.GetTopmostIf((ElseClauseSyntax)parent);
                default:
                    return null;
            }
        }

        public static async Task<Document> RefactorAsync(
            Document document,
            BlockSyntax block,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            StatementSyntax statement = block.Statements[0];

            if (block.IsParentKind(SyntaxKind.ElseClause)
                && statement.IsKind(SyntaxKind.IfStatement))
            {
                var elseClause = (ElseClauseSyntax)block.Parent;

                ElseClauseSyntax newElseClause = elseClause
                    .WithStatement(statement)
                    .WithElseKeyword(elseClause.ElseKeyword.WithoutTrailingTrivia())
                    .WithFormatterAnnotation();

                return await document.ReplaceNodeAsync(elseClause, newElseClause, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                StatementSyntax newNode = statement.TrimLeadingTrivia()
                    .WithFormatterAnnotation();

                return await document.ReplaceNodeAsync(block, newNode, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
