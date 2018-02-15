﻿// Copyright (c) Josef Pihrt. All rights reserved. Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Roslynator.CSharp.Refactorings
{
    internal static class AbstractTypeShouldNotHavePublicConstructorsRefactoring
    {
        public static void AnalyzeConstructorDeclaration(SyntaxNodeAnalysisContext context)
        {
            var constructorDeclaration = (ConstructorDeclarationSyntax)context.Node;

            if (CSharpAccessibility.GetExplicitAccessibility(constructorDeclaration).Is(Accessibility.Public, Accessibility.ProtectedOrInternal)
                && constructorDeclaration.IsParentKind(SyntaxKind.ClassDeclaration))
            {
                var classDeclaration = (ClassDeclarationSyntax)constructorDeclaration.Parent;

                SyntaxTokenList modifiers = classDeclaration.Modifiers;

                bool isAbstract = modifiers.Contains(SyntaxKind.AbstractKeyword);

                if (!isAbstract
                    && modifiers.Contains(SyntaxKind.PartialKeyword))
                {
                    INamedTypeSymbol classSymbol = context.SemanticModel.GetDeclaredSymbol(classDeclaration, context.CancellationToken);

                    if (classSymbol != null)
                        isAbstract = classSymbol.IsAbstract;
                }

                if (isAbstract)
                {
                    context.ReportDiagnostic(
                        DiagnosticDescriptors.AbstractTypeShouldNotHavePublicConstructors,
                        constructorDeclaration.Identifier);
                }
            }
        }

        public static Task<Document> RefactorAsync(
            Document document,
            ConstructorDeclarationSyntax constructorDeclaration,
            CancellationToken cancellationToken)
        {
            ConstructorDeclarationSyntax newNode = CSharpAccessibility.ChangeExplicitAccessibility(constructorDeclaration, Accessibility.Protected);

            return document.ReplaceNodeAsync(constructorDeclaration, newNode, cancellationToken);
        }
    }
}
