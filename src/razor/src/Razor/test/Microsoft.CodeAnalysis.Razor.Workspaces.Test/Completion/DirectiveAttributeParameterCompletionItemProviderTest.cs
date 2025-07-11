﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Razor.Language;
using Microsoft.AspNetCore.Razor.Language.IntegrationTests;
using Microsoft.AspNetCore.Razor.Language.Syntax;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.CodeAnalysis.Razor.Completion;

public class DirectiveAttributeParameterCompletionItemProviderTest : RazorToolingIntegrationTestBase
{
    private readonly DirectiveAttributeParameterCompletionItemProvider _provider;
    private readonly TagHelperDocumentContext _defaultTagHelperContext;

    internal override RazorFileKind? FileKind => RazorFileKind.Component;
    internal override bool UseTwoPhaseCompilation => true;

    public DirectiveAttributeParameterCompletionItemProviderTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _provider = new DirectiveAttributeParameterCompletionItemProvider();

        // Most of these completions rely on stuff in the web namespace.
        ImportItems.Add(CreateProjectItem(
            "_Imports.razor",
            "@using Microsoft.AspNetCore.Components.Web"));

        var codeDocument = GetCodeDocument(string.Empty);
        _defaultTagHelperContext = codeDocument.GetRequiredTagHelperContext();
    }

    private RazorCodeDocument GetCodeDocument(string content)
    {
        var result = CompileToCSharp(content, throwOnFailure: false);
        return result.CodeDocument;
    }

    [Fact]
    public void GetCompletionItems_OnNonAttributeArea_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 3, "<input @  />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_OnDirectiveAttributeName_ReturnsEmptyCollection()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 8, "<input @bind:fo  />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetCompletionItems_OnDirectiveAttributeParameter_ReturnsCompletions()
    {
        // Arrange
        var context = CreateRazorCompletionContext(absoluteIndex: 14, "<input @bind:fo  />");

        // Act
        var completions = _provider.GetCompletionItems(context);

        // Assert
        Assert.Equal(6, completions.Length);
        AssertContains(completions, "culture");
        AssertContains(completions, "event");
        AssertContains(completions, "format");
        AssertContains(completions, "get");
        AssertContains(completions, "set");
        AssertContains(completions, "after");
    }

    [Fact]
    public void GetAttributeParameterCompletions_NoDescriptorsForTag_ReturnsEmptyCollection()
    {
        // Arrange
        var documentContext = TagHelperDocumentContext.Create(string.Empty, tagHelpers: []);

        // Act
        var completions = DirectiveAttributeParameterCompletionItemProvider.GetAttributeParameterCompletions("@bin", string.Empty, "foobarbaz", [], documentContext);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetAttributeParameterCompletions_NoDirectiveAttributesForTag_ReturnsEmptyCollection()
    {
        // Arrange
        var descriptor = TagHelperDescriptorBuilder.Create("CatchAll", "TestAssembly");
        descriptor.BoundAttributeDescriptor(boundAttribute => boundAttribute.Name = "Test");
        descriptor.TagMatchingRule(rule => rule.RequireTagName("*"));
        var documentContext = TagHelperDocumentContext.Create(string.Empty, [descriptor.Build()]);

        // Act
        var completions = DirectiveAttributeParameterCompletionItemProvider.GetAttributeParameterCompletions("@bin", string.Empty, "input", [], documentContext);

        // Assert
        Assert.Empty(completions);
    }

    [Fact]
    public void GetAttributeParameterCompletions_SelectedDirectiveAttributeParameter_IsExcludedInCompletions()
    {
        // Arrange
        var attributeNames = ImmutableArray.Create("@bind");

        // Act
        var completions = DirectiveAttributeParameterCompletionItemProvider.GetAttributeParameterCompletions("@bind", "format", "input", attributeNames, _defaultTagHelperContext);

        // Assert
        AssertDoesNotContain(completions, "format");
    }

    [Fact]
    public void GetAttributeParameterCompletions_ReturnsCompletion()
    {
        // Arrange

        // Act
        var completions = DirectiveAttributeParameterCompletionItemProvider.GetAttributeParameterCompletions("@bind", string.Empty, "input", [], _defaultTagHelperContext);

        // Assert
        AssertContains(completions, "format");
    }

    [Fact]
    public void GetAttributeParameterCompletions_BaseDirectiveAttributeAndParameterVariationsExist_ExcludesCompletion()
    {
        // Arrange
        var attributeNames = ImmutableArray.Create(
            "@bind",
            "@bind:format",
            "@bind:event",
            "@");

        // Act
        var completions = DirectiveAttributeParameterCompletionItemProvider.GetAttributeParameterCompletions("@bind", string.Empty, "input", attributeNames, _defaultTagHelperContext);

        // Assert
        AssertDoesNotContain(completions, "format");
    }

    private static void AssertContains(IReadOnlyList<RazorCompletionItem> completions, string insertText)
    {
        Assert.Contains(completions, completion => insertText == completion.InsertText &&
                insertText == completion.DisplayText &&
                RazorCompletionItemKind.DirectiveAttributeParameter == completion.Kind);
    }

    private static void AssertDoesNotContain(IReadOnlyList<RazorCompletionItem> completions, string insertText)
    {

        Assert.DoesNotContain(completions, completion => insertText == completion.InsertText &&
               insertText == completion.DisplayText &&
               RazorCompletionItemKind.DirectiveAttributeParameter == completion.Kind);
    }

    private RazorCompletionContext CreateRazorCompletionContext(int absoluteIndex, string documentContent)
    {
        var codeDocument = GetCodeDocument(documentContent);
        var syntaxTree = codeDocument.GetRequiredSyntaxTree();
        var tagHelperContext = codeDocument.GetRequiredTagHelperContext();

        var owner = syntaxTree.Root.FindInnermostNode(absoluteIndex);
        owner = AbstractRazorCompletionFactsService.AdjustSyntaxNodeForWordBoundary(owner, absoluteIndex);
        return new RazorCompletionContext(absoluteIndex, owner, syntaxTree, tagHelperContext);
    }
}
