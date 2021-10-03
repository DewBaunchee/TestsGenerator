using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Lib
{
    public class TestsGenerator
    {
        private readonly struct TestFile
        {
            public string Name { get; init; }
            public SyntaxNode SyntaxNode { get; init; }
        }

        private readonly string _outputFolder;
        private readonly int _maxDegreeOfParallelism;

        public TestsGenerator(string outputFolder)
        {
            if (!Directory.Exists(outputFolder))
                throw new ArgumentException("No such folder: " + outputFolder);
            _outputFolder = outputFolder;
        }

        public TestsGenerator(string outputFolder, int maxDegreeOfParallelism) : this(outputFolder)
        {
            _maxDegreeOfParallelism = maxDegreeOfParallelism;
        }

        public Task Generate(List<string> sources)
        {
            return Task.Run(() =>
            {
                var buffer = new BufferBlock<string>(
                    new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _maxDegreeOfParallelism}
                );
                var reader = new TransformBlock<string, string>(
                    path => File.ReadAllTextAsync(path),
                    new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _maxDegreeOfParallelism}
                );
                var generator = new TransformBlock<string, List<TestFile>>(source =>
                    {
                        if (source == null)
                            return new List<TestFile>();
                        try
                        {
                            List<TestFile> testFiles = new List<TestFile>();
                            SyntaxNode root = CSharpSyntaxTree.ParseText(source).GetRoot();
                            root.DescendantNodes()
                                .Where(node => node is NamespaceDeclarationSyntax)
                                .ToList()
                                .ForEach(aNamespace => aNamespace.DescendantNodes()
                                    .Where(node => node is ClassDeclarationSyntax)
                                    .ToList()
                                    .ForEach(aClass =>
                                        testFiles.Add(
                                            GenerateTestClass(aNamespace.GetFirstToken().GetNextToken().ToString(),
                                                aClass)
                                        )
                                    )
                                );
                            return testFiles;
                        }
                        catch (Exception)
                        {
                            Console.WriteLine("[WARNING] Cannot parse file: " + source);
                        }

                        return new List<TestFile>();
                    },
                    new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _maxDegreeOfParallelism}
                );
                var writer = new ActionBlock<List<TestFile>>(testFiles =>
                    {
                        testFiles.ForEach(testFile =>
                        {
                            string outputPath = Path.Combine(new[] {_outputFolder, testFile.Name + ".cs"});
                            File.WriteAllTextAsync(
                                outputPath,
                                testFile.SyntaxNode.ToFullString()
                            );
                            Console.WriteLine("Tests generated at " + outputPath);
                        });
                    },
                    new ExecutionDataflowBlockOptions {MaxDegreeOfParallelism = _maxDegreeOfParallelism}
                );

                buffer.LinkTo(reader, new DataflowLinkOptions {PropagateCompletion = true});
                reader.LinkTo(generator, new DataflowLinkOptions {PropagateCompletion = true});
                generator.LinkTo(writer, new DataflowLinkOptions {PropagateCompletion = true});

                sources.ForEach(source => buffer.Post(source));

                buffer.Complete();
                writer.Completion.Wait();
            });
        }

        private TestFile GenerateTestClass(string aNamespace, SyntaxNode aClass)
        {
            return new TestFile
            {
                Name = GetFirstNonKeywordToken(aClass) + "Tests",
                SyntaxNode = CompilationUnit()
                    .WithUsings(
                        new SyntaxList<UsingDirectiveSyntax>(
                            new[]
                            {
                                UsingDirective(QualifiedName(IdentifierName("NUnit"), IdentifierName("Framework")))
                            }
                        )
                    ).WithMembers(
                        SingletonList<MemberDeclarationSyntax>(
                            NamespaceDeclaration(QualifiedName(IdentifierName(aNamespace), IdentifierName("Tests")))
                                .WithMembers(
                                    new SyntaxList<MemberDeclarationSyntax>(
                                        ClassDeclaration(GetFirstNonKeywordToken(aClass) + "Tests")
                                            .WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                                            .WithMembers(
                                                GetTestMethods(aClass.DescendantNodes()
                                                    .Where(node => node is MethodDeclarationSyntax)
                                                    .ToList()
                                                )
                                            )
                                    )
                                )
                        )
                    ).NormalizeWhitespace()
            };
        }

        private SyntaxList<MemberDeclarationSyntax> GetTestMethods(List<SyntaxNode> methods)
        {
            return new SyntaxList<MemberDeclarationSyntax>(methods.Select(method =>
                GetNextTestMethod(
                    GetFirstNonKeywordToken(method).ToString()
                )
            ));
        }

        private MemberDeclarationSyntax GetNextTestMethod(string methodName)
        {
            var method = MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), Identifier(methodName))
                .WithAttributeLists(
                    SingletonList(
                        AttributeList(
                            SingletonSeparatedList(
                                Attribute(IdentifierName("Test"))
                            )
                        )
                    )
                ).WithModifiers(TokenList(Token(SyntaxKind.PublicKeyword)))
                .WithBody(Block(
                        SingletonList<StatementSyntax>(
                            ExpressionStatement(
                                InvocationExpression(
                                    MemberAccessExpression(
                                        SyntaxKind.SimpleMemberAccessExpression,
                                        IdentifierName("Assert"),
                                        IdentifierName("Fail")
                                    )
                                ).WithArgumentList(
                                    ArgumentList(
                                        SingletonSeparatedList(
                                            Argument(LiteralExpression(
                                                    SyntaxKind.StringLiteralExpression, Literal("autogenerated")
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );
            return method;
        }

        private SyntaxToken GetFirstNonKeywordToken(SyntaxNode aClass)
        {
            return aClass.ChildTokens().First(token => !token.IsKeyword());
        }
    }
}