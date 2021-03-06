﻿using System;
using System.Collections.Generic;
using System.Linq;
using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace LanguageExt.CodeGen
{
    internal static class CodeGenUtil
    {
        public static (ClassDeclarationSyntax PartialClass, TypeSyntax ReturnType, List<FieldDeclarationSyntax> Fields) GetState(TransformationContext context)
        {
            // Our generator is applied to any class that our attribute is applied to.
            var applyToClass = (ClassDeclarationSyntax)context.ProcessingNode;

            var classModifiers = SyntaxFactory.TokenList(
                    Enumerable.Concat(
                        applyToClass.Modifiers
                                    .Where(t => !t.IsKind(SyntaxKind.PartialKeyword)).AsEnumerable(),
                        new[] { SyntaxFactory.Token(SyntaxKind.PartialKeyword) }));

            // Apply a suffix to the name of a copy of the class.
            var partialClass = SyntaxFactory.ClassDeclaration($"{applyToClass.Identifier}")
                                            .WithModifiers(classModifiers);

            if (applyToClass.TypeParameterList != null)
            {
                partialClass = partialClass.WithTypeParameterList(applyToClass.TypeParameterList);
            }

            if (applyToClass.ConstraintClauses != null)
            {
                partialClass = partialClass.WithConstraintClauses(applyToClass.ConstraintClauses);
            }

            var returnType = CodeGenUtil.TypeFromClass(applyToClass);

            var fields = applyToClass.Members
                                     .Where(m => m is FieldDeclarationSyntax)
                                     .Select(m => m as FieldDeclarationSyntax)
                                     .Where(f => f.Declaration.Variables.Count > 0)
                                     .Where(f => FirstCharIsUpper(f.Declaration.Variables[0].Identifier.ToString()))
                                     .Where(f => f.Modifiers.Any(SyntaxKind.PublicKeyword))
                                     .Where(f => f.Modifiers.Any(SyntaxKind.ReadOnlyKeyword))
                                     .Where(f => !f.Modifiers.Any(SyntaxKind.StaticKeyword))
                                     .ToList();

            return (partialClass, returnType, fields);
        }

        internal static bool ForAll<A>(this IEnumerable<A> ma, Func<A, bool> f)
        {
            foreach(var a in ma)
            {
                if (!f(a)) return false;
            }
            return true;
        }

        internal static bool Exists<A>(this IEnumerable<A> ma, Func<A, bool> f)
        {
            foreach (var a in ma)
            {
                if (f(a)) return true;
            }
            return false;
        }

        public static ClassDeclarationSyntax AddLenses(ClassDeclarationSyntax partialClass, TypeSyntax returnType, System.Collections.Generic.List<FieldDeclarationSyntax> fields)
        {
            foreach (var field in fields)
            {
                partialClass = AddLens(partialClass, returnType, field);
            }
            return partialClass;
        }

        public static ClassDeclarationSyntax AddLens(ClassDeclarationSyntax partialClass, TypeSyntax returnType, FieldDeclarationSyntax field)
        {
            var lfield = SyntaxFactory.FieldDeclaration(
                SyntaxFactory.VariableDeclaration(
                    SyntaxFactory.GenericName(SyntaxFactory.Identifier("Lens"))
                                 .WithTypeArgumentList(
                                    SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList<TypeSyntax>(new[] { returnType, field.Declaration.Type }))))
                             .WithVariables(
                                SyntaxFactory.SingletonSeparatedList<VariableDeclaratorSyntax>(
                                    SyntaxFactory.VariableDeclarator(MakeFirstCharLower(field.Declaration.Variables[0].Identifier))
                                                 .WithInitializer(
                                                    SyntaxFactory.EqualsValueClause(
                                                        SyntaxFactory.InvocationExpression(
                                                                            SyntaxFactory.MemberAccessExpression(
                                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                                SyntaxFactory.GenericName("Lens")
                                                                                    .WithTypeArgumentList(
                                                                                        SyntaxFactory.TypeArgumentList(
                                                                                            SyntaxFactory.SeparatedList<TypeSyntax>(new[] { returnType, field.Declaration.Type }))),
                                                                                    SyntaxFactory.IdentifierName("New")))
                                                                     .WithArgumentList(
                                                                        SyntaxFactory.ArgumentList(
                                                                            SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                                                new SyntaxNodeOrToken[] {
                                                                                    SyntaxFactory.Argument(
                                                                                        SyntaxFactory.SimpleLambdaExpression(
                                                                                            SyntaxFactory.Parameter(
                                                                                                SyntaxFactory.Identifier("_x")),
                                                                                            SyntaxFactory.MemberAccessExpression(
                                                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                                                SyntaxFactory.IdentifierName("_x"),
                                                                                                SyntaxFactory.IdentifierName(field.Declaration.Variables[0].Identifier.ToString())))),
                                                                                    SyntaxFactory.Token(SyntaxKind.CommaToken),
                                                                                    SyntaxFactory.Argument(
                                                                                        SyntaxFactory.SimpleLambdaExpression(
                                                                                            SyntaxFactory.Parameter(
                                                                                                SyntaxFactory.Identifier("_x")),
                                                                                                SyntaxFactory.SimpleLambdaExpression(
                                                                                                    SyntaxFactory.Parameter(
                                                                                                        SyntaxFactory.Identifier("_y")),
                                                                                                        SyntaxFactory.InvocationExpression(
                                                                                                            SyntaxFactory.MemberAccessExpression(
                                                                                                                SyntaxKind.SimpleMemberAccessExpression,
                                                                                                                SyntaxFactory.IdentifierName("_y"),
                                                                                                                SyntaxFactory.IdentifierName("With")))
                                                                                                            .WithArgumentList(
                                                                                                                SyntaxFactory.ArgumentList(
                                                                                                                    SyntaxFactory.SingletonSeparatedList<ArgumentSyntax>(
                                                                                                                        SyntaxFactory.Argument(
                                                                                                                            SyntaxFactory.IdentifierName("_x"))
                                                                                                                            .WithNameColon(
                                                                                                                                SyntaxFactory.NameColon(field.Declaration.Variables[0].Identifier.ToString()))))))))

                                                                                }))))))));

            lfield = lfield.WithModifiers(
                SyntaxFactory.TokenList(
                    Enumerable.Concat(
                        field.Modifiers.Where(m => m.IsKind(SyntaxKind.PublicKeyword) || m.IsKind(SyntaxKind.PrivateKeyword) || m.IsKind(SyntaxKind.ProtectedKeyword)),
                        new[] { SyntaxFactory.Token(SyntaxKind.StaticKeyword), SyntaxFactory.Token(SyntaxKind.ReadOnlyKeyword) })));

            return partialClass.AddMembers(lfield);
        }

        public static ClassDeclarationSyntax AddWith(TransformationContext context, ClassDeclarationSyntax partialClass, TypeSyntax returnType, List<FieldDeclarationSyntax> fields)
        {
            var withParms = fields.Where(f => f.Declaration.Variables.Count > 0)
                                  .Select(f => (Field: f, Type: context.SemanticModel.GetTypeInfo(f.Declaration.Type)))
                                  .Select(f => (Id: f.Field.Declaration.Variables[0].Identifier, 
                                                Type: f.Field.Declaration.Type,
                                                Info: f.Type))
                                  .Select(f => (f.Id, 
                                                f.Type, 
                                                f.Info,
                                                IsGeneric: !f.Info.Type.IsValueType && !f.Info.Type.IsReferenceType,
                                                ParamType: f.Info.Type.IsValueType 
                                                    ? SyntaxFactory.NullableType(f.Type)
                                                    : f.Type))
                                  .Select(f =>
                                       SyntaxFactory.Parameter(MakeFirstCharUpper(f.Id))
                                                    .WithType(f.ParamType)
                                                    .WithDefault(
                                                        f.IsGeneric
                                                            ? SyntaxFactory.EqualsValueClause(SyntaxFactory.DefaultExpression(f.Type))
                                                            : SyntaxFactory.EqualsValueClause(SyntaxFactory.LiteralExpression(SyntaxKind.NullLiteralExpression))))
                                  .ToArray();

            var withMethod = SyntaxFactory.MethodDeclaration(returnType, "With")
                                          .WithParameterList(SyntaxFactory.ParameterList(SyntaxFactory.SeparatedList<ParameterSyntax>(withParms)))
                                          .WithModifiers(SyntaxFactory.TokenList(
                                              SyntaxFactory.Token(SyntaxKind.PublicKeyword)))
                                          .WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken))
                                          .WithExpressionBody(
                                              SyntaxFactory.ArrowExpressionClause(
                                                  SyntaxFactory.ObjectCreationExpression(
                                                      returnType,
                                                      SyntaxFactory.ArgumentList(
                                                          SyntaxFactory.SeparatedList<ArgumentSyntax>(
                                                              withParms.Select(wa =>
                                                                SyntaxFactory.Argument(
                                                                    SyntaxFactory.BinaryExpression(
                                                                        SyntaxKind.CoalesceExpression,
                                                                        SyntaxFactory.IdentifierName(wa.Identifier),
                                                                        SyntaxFactory.MemberAccessExpression(
                                                                            SyntaxKind.SimpleMemberAccessExpression,
                                                                            SyntaxFactory.ThisExpression(),
                                                                            SyntaxFactory.IdentifierName(wa.Identifier))))))), null)));

            partialClass = partialClass.AddMembers(withMethod);
            return partialClass;
        }

        public static TypeSyntax TypeFromClass(ClassDeclarationSyntax decl) =>
            SyntaxFactory.ParseTypeName($"{decl.Identifier}{decl.TypeParameterList}");// SyntaxFactory.IdentifierName(decl.Identifier);

        public static SyntaxToken MakeFirstCharUpper(SyntaxToken identifier)
        {
            var id = identifier.ToString();
            var id2 = $"{Char.ToUpper(id[0])}{id.Substring(1)}";
            return SyntaxFactory.Identifier(id2);
        }

        public static SyntaxToken MakeFirstCharLower(SyntaxToken identifier)
        {
            var id = identifier.ToString();
            var id2 = $"{Char.ToLower(id[0])}{id.Substring(1)}";
            return SyntaxFactory.Identifier(id2);
        }

        static bool FirstCharIsUpper(string name) =>
            name.Length > 0 && Char.IsUpper(name[0]);
    }
}
