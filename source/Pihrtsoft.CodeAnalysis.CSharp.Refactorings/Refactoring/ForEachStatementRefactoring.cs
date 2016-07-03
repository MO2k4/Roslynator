﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pihrtsoft.CodeAnalysis.CSharp.Analysis;

namespace Pihrtsoft.CodeAnalysis.CSharp.Refactoring
{
    internal static class ForEachStatementRefactoring
    {
        public static async Task ComputeRefactoringsAsync(RefactoringContext context, ForEachStatementSyntax forEachStatement)
        {
            if (context.SupportsSemanticModel)
            {
                if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.ChangeTypeAccordingToExpression))
                    await ChangeTypeAccordingToExpressionAsync(context, forEachStatement);

                if (context.Settings.IsAnyRefactoringEnabled(
                    RefactoringIdentifiers.ChangeExplicitTypeToVar,
                    RefactoringIdentifiers.ChangeVarToExplicitType))
                {
                    await ChangeTypeAsync(context, forEachStatement);
                }

                if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.RenameIdentifierAccordingToTypeName))
                    await RenameIdentifierAccordingToTypeNameAsync(context, forEachStatement);

                if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.ReplaceForeachWithFor)
                    && ReplaceForeachWithForRefactoring.CanRefactor(forEachStatement, await context.GetSemanticModelAsync(), context.CancellationToken))
                {
                    context.RegisterRefactoring(
                        "Replace foreach with for",
                        cancellationToken => ReplaceForeachWithForRefactoring.RefactorAsync(context.Document, forEachStatement, cancellationToken));
                }
            }
        }

        internal static async Task ChangeTypeAsync(
            RefactoringContext context,
            ForEachStatementSyntax forEachStatement)
        {
            TypeSyntax type = forEachStatement.Type;

            if (type?.Span.Contains(context.Span) != true)
                return;

            SemanticModel semanticModel = await context.GetSemanticModelAsync();

            TypeAnalysisResult result = ForEachStatementAnalysis.AnalyzeType(
                forEachStatement,
                semanticModel,
                context.CancellationToken);

            if (result == TypeAnalysisResult.Explicit)
            {
                if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.ChangeExplicitTypeToVar))
                {
                    context.RegisterRefactoring(
                        "Change type to 'var'",
                        cancellationToken => TypeSyntaxRefactoring.ChangeTypeToImplicitAsync(context.Document, type, cancellationToken));
                }
            }
            else if (result == TypeAnalysisResult.ImplicitButShouldBeExplicit)
            {
                if (context.Settings.IsRefactoringEnabled(RefactoringIdentifiers.ChangeVarToExplicitType))
                {
                    ITypeSymbol typeSymbol = semanticModel.GetTypeInfo(type, context.CancellationToken).Type;

                    context.RegisterRefactoring(
                        $"Change type to '{typeSymbol.ToDisplayString(TypeSyntaxRefactoring.SymbolDisplayFormat)}'",
                        cancellationToken => TypeSyntaxRefactoring.ChangeTypeToExplicitAsync(context.Document, type, typeSymbol, cancellationToken));
                }
            }
        }

        internal static async Task RenameIdentifierAccordingToTypeNameAsync(
            RefactoringContext context,
            ForEachStatementSyntax forEachStatement)
        {
            if (forEachStatement.Type != null
                && forEachStatement.Identifier.Span.Contains(context.Span))
            {
                SemanticModel semanticModel = await context.GetSemanticModelAsync();

                string newName = NamingHelper.CreateIdentifierName(
                    forEachStatement.Type,
                    semanticModel,
                    firstCharToLower: true);

                if (!string.IsNullOrEmpty(newName)
                    && !string.Equals(newName, forEachStatement.Identifier.ValueText, StringComparison.Ordinal))
                {
                    ISymbol symbol = semanticModel.GetDeclaredSymbol(forEachStatement, context.CancellationToken);

                    context.RegisterRefactoring(
                        $"Rename foreach variable to '{newName}'",
                        cancellationToken => symbol.RenameAsync(newName, context.Document, cancellationToken));
                }
            }
        }

        internal static async Task ChangeTypeAccordingToExpressionAsync(
            RefactoringContext context,
            ForEachStatementSyntax forEachStatement)
        {
            if (forEachStatement.Type?.IsVar == false
                && forEachStatement.Type.Span.Contains(context.Span))
            {
                SemanticModel semanticModel = await context.GetSemanticModelAsync();

                ForEachStatementInfo info = semanticModel.GetForEachStatementInfo(forEachStatement);

                if (info.ElementType != null)
                {
                    ITypeSymbol typeSymbol = semanticModel.GetTypeInfo(forEachStatement.Type).ConvertedType;

                    if (!info.ElementType.Equals(typeSymbol))
                    {
                        context.RegisterRefactoring(
                            $"Change type to '{info.ElementType.ToDisplayString(TypeSyntaxRefactoring.SymbolDisplayFormat)}'",
                            cancellationToken =>
                            {
                                return TypeSyntaxRefactoring.ChangeTypeToExplicitAsync(
                                    context.Document,
                                    forEachStatement.Type,
                                    info.ElementType,
                                    cancellationToken);
                            });
                    }
                }
            }
        }
    }
}