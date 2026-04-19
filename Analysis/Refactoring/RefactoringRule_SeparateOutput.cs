using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_SeparateOutput : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "SEP001" };

        public Document Apply(Document document)
        {
            SyntaxNode root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
            SemanticModel semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
            DocumentEditor editor = DocumentEditor.CreateAsync(document).GetAwaiter().GetResult();
            bool changed = false;

            // Находит все вызовы Console.Write/WriteLine
            List<InvocationExpressionSyntax> consoleWrites = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                              ma.Expression is IdentifierNameSyntax { Identifier.Text: "Console" } &&
                              (ma.Name.Identifier.Text == "WriteLine" || ma.Name.Identifier.Text == "Write"))
                .ToList();

            if (!consoleWrites.Any()) return document;

            ClassDeclarationSyntax? classDecl = consoleWrites.First().FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDecl == null) return document;

            // Проверяет, существует ли уже метод DisplayOutput в классе
            bool hasDisplayOutput = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.Text == "DisplayOutput");
            if (hasDisplayOutput) return document;

            // Определяет, является ли вызывающий метод статическим
            bool isCallerStatic = false;
            InvocationExpressionSyntax firstWrite = consoleWrites.First();
            MethodDeclarationSyntax? containingMethod = firstWrite.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod != null && containingMethod.Modifiers.Any(SyntaxKind.StaticKeyword))
                isCallerStatic = true;

            // Создаёт метод DisplayOutput с соответствующей статичностью
            SyntaxTokenList modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
            if (isCallerStatic)
                modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

            MethodDeclarationSyntax outputMethod = SyntaxFactory.MethodDeclaration(
                    SyntaxFactory.PredefinedType(SyntaxFactory.Token(SyntaxKind.VoidKeyword)),
                    "DisplayOutput")
                .WithModifiers(modifiers)
                .WithParameterList(SyntaxFactory.ParameterList(
                    SyntaxFactory.SingletonSeparatedList(
                        SyntaxFactory.Parameter(SyntaxFactory.Identifier("message"))
                            .WithType(SyntaxFactory.ParseTypeName("object")))))
                .WithBody(SyntaxFactory.Block(
                    SyntaxFactory.ExpressionStatement(
                        SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName("Console"),
                                SyntaxFactory.IdentifierName("WriteLine")),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SingletonSeparatedList(
                                    SyntaxFactory.Argument(SyntaxFactory.IdentifierName("message"))))))))
                .NormalizeWhitespace();

            editor.AddMember(classDecl, outputMethod);
            changed = true;

            // Заменяет все вызовы Console.WriteLine/Write на DisplayOutput
            foreach (InvocationExpressionSyntax write in consoleWrites)
            {
                ArgumentSyntax? arg = write.ArgumentList.Arguments.FirstOrDefault();
                if (arg == null) continue;

                ExpressionSyntax argumentExpression = arg.Expression;
                ITypeSymbol? argType = semanticModel.GetTypeInfo(argumentExpression).Type;
                if (argType != null && argType.SpecialType != SpecialType.System_String)
                {
                    argumentExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            argumentExpression,
                            SyntaxFactory.IdentifierName("ToString")));
                }

                ArgumentSyntax newArg = arg.WithExpression(argumentExpression);
                InvocationExpressionSyntax newCall = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.IdentifierName("DisplayOutput"),
                        SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(newArg)))
                    .WithTriviaFrom(write);
                editor.ReplaceNode(write, newCall);
                changed = true;
            }

            return changed ? editor.GetChangedDocument() : document;
        }
    }
}