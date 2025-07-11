﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.AspNetCore.Razor.Language.CodeGeneration;
using Microsoft.AspNetCore.Razor.Language.Intermediate;
using Xunit;
using static Microsoft.AspNetCore.Razor.Language.CommonMetadata;

namespace Microsoft.AspNetCore.Razor.Language.Extensions;

public class PreallocatedAttributeTargetExtensionTest
{
    [Fact]
    public void WriteTagHelperHtmlAttributeValue_RendersCorrectly()
    {
        // Arrange
        var extension = new PreallocatedAttributeTargetExtension();
        using var context = TestCodeRenderingContext.CreateRuntime();

        var node = new PreallocatedTagHelperHtmlAttributeValueIntermediateNode()
        {
            AttributeName = "Foo",
            Value = "Bar",
            AttributeStructure = AttributeStructure.DoubleQuotes,
            VariableName = "MyProp"
        };

        // Act
        extension.WriteTagHelperHtmlAttributeValue(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"private static readonly global::Microsoft.AspNetCore.Razor.TagHelpers.TagHelperAttribute MyProp = new global::Microsoft.AspNetCore.Razor.TagHelpers.TagHelperAttribute(""Foo"", new global::Microsoft.AspNetCore.Html.HtmlString(""Bar""), global::Microsoft.AspNetCore.Razor.TagHelpers.HtmlAttributeValueStyle.DoubleQuotes);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteTagHelperHtmlAttributeValue_Minimized_RendersCorrectly()
    {
        // Arrange
        var extension = new PreallocatedAttributeTargetExtension();
        using var context = TestCodeRenderingContext.CreateRuntime();

        var node = new PreallocatedTagHelperHtmlAttributeValueIntermediateNode()
        {
            AttributeName = "Foo",
            Value = "Bar",
            AttributeStructure = AttributeStructure.Minimized,
            VariableName = "_tagHelper1"
        };

        // Act
        extension.WriteTagHelperHtmlAttributeValue(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"private static readonly global::Microsoft.AspNetCore.Razor.TagHelpers.TagHelperAttribute _tagHelper1 = new global::Microsoft.AspNetCore.Razor.TagHelpers.TagHelperAttribute(""Foo"");
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteTagHelperHtmlAttribute_RendersCorrectly()
    {
        // Arrange
        var extension = new PreallocatedAttributeTargetExtension();
        using var context = TestCodeRenderingContext.CreateRuntime();

        var tagHelperNode = new TagHelperIntermediateNode();
        var node = new PreallocatedTagHelperHtmlAttributeIntermediateNode()
        {
            VariableName = "_tagHelper1"
        };
        tagHelperNode.Children.Add(node);
        Push(context, tagHelperNode);

        // Act
        extension.WriteTagHelperHtmlAttribute(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"__tagHelperExecutionContext.AddHtmlAttribute(_tagHelper1);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteTagHelperPropertyValue_RendersCorrectly()
    {
        // Arrange
        var extension = new PreallocatedAttributeTargetExtension();
        using var context = TestCodeRenderingContext.CreateRuntime();

        var node = new PreallocatedTagHelperPropertyValueIntermediateNode()
        {
            AttributeName = "Foo",
            Value = "Bar",
            AttributeStructure = AttributeStructure.DoubleQuotes,
            VariableName = "_tagHelper1",
        };

        // Act
        extension.WriteTagHelperPropertyValue(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"private static readonly global::Microsoft.AspNetCore.Razor.TagHelpers.TagHelperAttribute _tagHelper1 = new global::Microsoft.AspNetCore.Razor.TagHelpers.TagHelperAttribute(""Foo"", ""Bar"", global::Microsoft.AspNetCore.Razor.TagHelpers.HtmlAttributeValueStyle.DoubleQuotes);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteTagHelperProperty_RendersCorrectly()
    {
        // Arrange
        var extension = new PreallocatedAttributeTargetExtension();
        using var context = TestCodeRenderingContext.CreateRuntime();

        var tagHelperBuilder = new TagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "FooTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("FooTagHelper"));

        var builder = new BoundAttributeDescriptorBuilder(tagHelperBuilder);
        builder
            .Name("Foo")
            .TypeName("System.String")
            .Metadata(PropertyName("FooProp"));

        var descriptor = builder.Build();

        var tagHelperNode = new TagHelperIntermediateNode();
        var node = new PreallocatedTagHelperPropertyIntermediateNode()
        {
            AttributeName = descriptor.Name,
            BoundAttribute = descriptor,
            FieldName = "__FooTagHelper",
            PropertyName = "FooProp",
            VariableName = "_tagHelper1",
        };
        tagHelperNode.Children.Add(node);
        Push(context, tagHelperNode);

        // Act
        extension.WriteTagHelperProperty(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"__FooTagHelper.FooProp = (string)_tagHelper1.Value;
__tagHelperExecutionContext.AddTagHelperAttribute(_tagHelper1);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteSetPreallocatedTagHelperProperty_IndexerAttribute_RendersCorrectly()
    {
        // Arrange
        var extension = new PreallocatedAttributeTargetExtension();
        using var context = TestCodeRenderingContext.CreateRuntime();

        var tagHelperBuilder = new TagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "FooTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("FooTagHelper"));

        var builder = new BoundAttributeDescriptorBuilder(tagHelperBuilder);
        builder
            .Name("Foo")
            .TypeName("System.Collections.Generic.Dictionary<System.String, System.String>")
            .AsDictionaryAttribute("pre-", "System.String")
            .Metadata(PropertyName("FooProp"));

        var descriptor = builder.Build();

        var tagHelperNode = new TagHelperIntermediateNode();
        var node = new PreallocatedTagHelperPropertyIntermediateNode()
        {
            AttributeName = "pre-Foo",
            FieldName = "__FooTagHelper",
            VariableName = "_tagHelper1",
            BoundAttribute = descriptor,
            IsIndexerNameMatch = true,
            PropertyName = "FooProp",
            TagHelper = tagHelperBuilder.Build(),
        };
        tagHelperNode.Children.Add(node);
        Push(context, tagHelperNode);

        // Act
        extension.WriteTagHelperProperty(context, node);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"if (__FooTagHelper.FooProp == null)
{
    throw new InvalidOperationException(InvalidTagHelperIndexerAssignment(""pre-Foo"", ""FooTagHelper"", ""FooProp""));
}
__FooTagHelper.FooProp[""Foo""] = (string)_tagHelper1.Value;
__tagHelperExecutionContext.AddTagHelperAttribute(_tagHelper1);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    [Fact]
    public void WriteSetPreallocatedTagHelperProperty_IndexerAttribute_MultipleValues()
    {
        // Arrange
        var extension = new PreallocatedAttributeTargetExtension();
        using var context = TestCodeRenderingContext.CreateRuntime();

        var tagHelperBuilder = new TagHelperDescriptorBuilder(TagHelperConventions.DefaultKind, "FooTagHelper", "Test");
        tagHelperBuilder.Metadata(TypeName("FooTagHelper"));

        var builder = new BoundAttributeDescriptorBuilder(tagHelperBuilder);
        builder
            .Name("Foo")
            .TypeName("System.Collections.Generic.Dictionary<System.String, System.String>")
            .AsDictionaryAttribute("pre-", "System.String")
            .Metadata(PropertyName("FooProp"));

        var boundAttribute = builder.Build();
        var tagHelper = tagHelperBuilder.Build();

        var tagHelperNode = new TagHelperIntermediateNode();
        var node1 = new PreallocatedTagHelperPropertyIntermediateNode()
        {
            AttributeName = "pre-Bar",
            FieldName = "__FooTagHelper",
            VariableName = "_tagHelper0s",
            BoundAttribute = boundAttribute,
            IsIndexerNameMatch = true,
            PropertyName = "FooProp",
            TagHelper = tagHelper,
        };
        var node2 = new PreallocatedTagHelperPropertyIntermediateNode()
        {
            AttributeName = "pre-Foo",
            FieldName = "__FooTagHelper",
            VariableName = "_tagHelper1",
            BoundAttribute = boundAttribute,
            IsIndexerNameMatch = true,
            PropertyName = "FooProp",
            TagHelper = tagHelper,
        };
        tagHelperNode.Children.Add(node1);
        tagHelperNode.Children.Add(node2);
        Push(context, tagHelperNode);

        // Act
        extension.WriteTagHelperProperty(context, node2);

        // Assert
        var csharp = context.CodeWriter.GetText().ToString();
        Assert.Equal(
@"__FooTagHelper.FooProp[""Foo""] = (string)_tagHelper1.Value;
__tagHelperExecutionContext.AddTagHelperAttribute(_tagHelper1);
",
            csharp,
            ignoreLineEndingDifferences: true);
    }

    private static void Push(CodeRenderingContext context, TagHelperIntermediateNode node)
    {
        context.PushAncestor(node);
    }
}
