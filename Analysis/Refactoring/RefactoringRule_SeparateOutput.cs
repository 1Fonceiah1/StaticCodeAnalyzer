using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Editing;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StaticCodeAnalyzer.Analysis.Refactoring
{
    public class RefactoringRule_SeparateOutput : IRefactoringRule
    {
        public IEnumerable<string> TargetIssueCodes => new[] { "SEP001" };

        public async Task<Document> ApplyAsync(Document document, CancellationToken cancellationToken)
        {
            var root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
            var semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            var editor = await DocumentEditor.CreateAsync(document, cancellationToken).ConfigureAwait(false);
            bool changed = false;

            var consoleWrites = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(inv => inv.Expression is MemberAccessExpressionSyntax ma &&
                              ma.Expression is IdentifierNameSyntax { Identifier.Text: "Console" } &&
                              (ma.Name.Identifier.Text == "WriteLine" || ma.Name.Identifier.Text == "Write"))
                .ToList();

            if (!consoleWrites.Any()) return document;

            var classDecl = consoleWrites.First().FirstAncestorOrSelf<ClassDeclarationSyntax>();
            if (classDecl == null) return document;

            // Проверяем, есть ли уже метод DisplayOutput в классе (с любой статичностью)
            bool hasDisplayOutput = classDecl.Members
                .OfType<MethodDeclarationSyntax>()
                .Any(m => m.Identifier.Text == "DisplayOutput");
            if (hasDisplayOutput) return document;

            // Определяем, является ли вызывающий метод статическим
            bool isCallerStatic = false;
            var firstWrite = consoleWrites.First();
            var containingMethod = firstWrite.FirstAncestorOrSelf<MethodDeclarationSyntax>();
            if (containingMethod != null && containingMethod.Modifiers.Any(SyntaxKind.StaticKeyword))
                isCallerStatic = true;

            // Создаём метод DisplayOutput с правильной статичностью
            var modifiers = SyntaxFactory.TokenList(SyntaxFactory.Token(SyntaxKind.PrivateKeyword));
            if (isCallerStatic)
                modifiers = modifiers.Add(SyntaxFactory.Token(SyntaxKind.StaticKeyword));

            var outputMethod = SyntaxFactory.MethodDeclaration(
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

            // Заменяем вызовы Console.WriteLine на DisplayOutput
            foreach (var write in consoleWrites)
            {
                var arg = write.ArgumentList.Arguments.FirstOrDefault();
                if (arg == null) continue;

                ExpressionSyntax argumentExpression = arg.Expression;
                var argType = semanticModel.GetTypeInfo(argumentExpression, cancellationToken).Type;
                if (argType != null && argType.SpecialType != SpecialType.System_String)
                {
                    argumentExpression = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            argumentExpression,
                            SyntaxFactory.IdentifierName("ToString")));
                }

                var newArg = arg.WithExpression(argumentExpression);
                var newCall = SyntaxFactory.InvocationExpression(
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