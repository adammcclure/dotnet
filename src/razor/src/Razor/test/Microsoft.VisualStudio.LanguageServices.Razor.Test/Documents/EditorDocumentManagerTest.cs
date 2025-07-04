﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Razor.Test.Common;
using Microsoft.AspNetCore.Razor.Test.Common.Editor;
using Microsoft.AspNetCore.Razor.Test.Common.VisualStudio;
using Microsoft.CodeAnalysis.Razor.ProjectSystem;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.VisualStudio.Razor.Documents;

public class EditorDocumentManagerTest : VisualStudioTestBase
{
    private readonly TestEditorDocumentManager _manager;
    private readonly ProjectKey _projectKey1;
    private readonly ProjectKey _projectKey2;
    private readonly string _projectFile1;
    private readonly string _projectFile2;
    private readonly string _file1;
    private readonly string _file2;
    private readonly TestTextBuffer _textBuffer;

    public EditorDocumentManagerTest(ITestOutputHelper testOutput)
        : base(testOutput)
    {
        _manager = new TestEditorDocumentManager(JoinableTaskFactory.Context);
        _projectKey1 = TestProjectData.SomeProject.Key;
        _projectKey2 = TestProjectData.AnotherProject.Key;
        _projectFile1 = TestProjectData.SomeProject.FilePath;
        _projectFile2 = TestProjectData.AnotherProject.FilePath;
        _file1 = TestProjectData.SomeProjectFile1.FilePath;
        _file2 = TestProjectData.AnotherProjectFile2.FilePath;
        _textBuffer = new TestTextBuffer(new StringTextSnapshot("HI"));
    }

    [UIFact]
    public void GetOrCreateDocument_CreatesAndCachesDocument()
    {
        // Arrange
        var expected = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);

        // Act
        _manager.TryGetDocument(new DocumentKey(_projectKey1, _file1), out var actual);

        // Assert
        Assert.Same(expected, actual);
    }

    [UIFact]
    public void GetOrCreateDocument_NoOp()
    {
        // Arrange
        var expected = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);

        // Act
        var actual = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);

        // Assert
        Assert.Same(expected, actual);
    }

    [UIFact]
    public void GetOrCreateDocument_SameFile_MulipleProjects()
    {
        // Arrange
        var document1 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);

        // Act
        var document2 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey2, _file1), _projectFile2, _projectKey2, null, null, null, null);

        // Assert
        Assert.NotSame(document1, document2);
    }

    [UIFact]
    public void GetOrCreateDocument_MulipleFiles_SameProject()
    {
        // Arrange
        var document1 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);

        // Act
        var document2 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file2), _projectFile1, _projectKey1, null, null, null, null);

        // Assert
        Assert.NotSame(document1, document2);
    }

    [UIFact]
    public void GetOrCreateDocument_WithBuffer_AttachesBuffer()
    {
        // Arrange
        _manager.Buffers.Add(_file1, _textBuffer);

        // Act
        var document = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);

        // Assert
        Assert.True(document.IsOpenInEditor);
        Assert.NotNull(document.EditorTextBuffer);

        Assert.Same(document, Assert.Single(_manager.Opened));
        Assert.Empty(_manager.Closed);
    }

    [UIFact]
    public void TryGetMatchingDocuments_MultipleDocuments()
    {
        // Arrange
        var document1 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);
        var document2 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey2, _file1), _projectFile2, _projectKey2, null, null, null, null);

        // Act
        _manager.TryGetMatchingDocuments(_file1, out var documents);

        // Assert
        Assert.Collection(
            documents.OrderBy(d => d.ProjectFilePath),
            d => Assert.Same(document2, d),
            d => Assert.Same(document1, d));
    }

    [UIFact]
    public void RemoveDocument_MultipleDocuments_RemovesOne()
    {
        // Arrange
        var document1 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);
        var document2 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey2, _file1), _projectFile2, _projectKey2, null, null, null, null);

        // Act
        _manager.RemoveDocument(document1);

        // Assert
        _manager.TryGetMatchingDocuments(_file1, out var documents);
        Assert.Collection(
            documents.OrderBy(d => d.ProjectFilePath),
            d => Assert.Same(document2, d));
    }

    [UIFact]
    public void DocumentOpened_MultipleDocuments_OpensAll()
    {
        // Arrange
        var document1 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);
        var document2 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey2, _file1), _projectFile2, _projectKey2, null, null, null, null);

        // Act
        _manager.DocumentOpened(_file1, _textBuffer);

        // Assert
        Assert.Collection(
            _manager.Opened.OrderBy(d => d.ProjectFilePath),
            d => Assert.Same(document2, d),
            d => Assert.Same(document1, d));
    }

    [UIFact]
    public void DocumentOpened_MultipleDocuments_ClosesAll()
    {
        // Arrange
        var document1 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey1, _file1), _projectFile1, _projectKey1, null, null, null, null);
        var document2 = _manager.GetOrCreateDocument(new DocumentKey(_projectKey2, _file1), _projectFile2, _projectKey2, null, null, null, null);
        _manager.DocumentOpened(_file1, _textBuffer);

        // Act
        _manager.DocumentClosed(_file1);

        // Assert
        Assert.Collection(
            _manager.Closed.OrderBy(d => d.ProjectFilePath),
            d => Assert.Same(document2, d),
            d => Assert.Same(document1, d));
    }

    private class TestEditorDocumentManager(
        JoinableTaskContext joinableTaskContext)
        : EditorDocumentManager(CreateFileChangeTrackerFactory(), joinableTaskContext)
    {
        public List<EditorDocument> Opened { get; } = new List<EditorDocument>();

        public List<EditorDocument> Closed { get; } = new List<EditorDocument>();

        public Dictionary<string, ITextBuffer> Buffers { get; } = new Dictionary<string, ITextBuffer>();

        private static IFileChangeTrackerFactory CreateFileChangeTrackerFactory()
        {
            var mock = new StrictMock<IFileChangeTrackerFactory>();

            mock.Setup(x => x.Create(It.IsAny<string>()))
                .Returns((string filePath) =>
                {
                    var mock = new StrictMock<IFileChangeTracker>();
                    mock.SetupGet(x => x.FilePath)
                        .Returns(filePath);
                    mock.Setup(x => x.StartListening());
                    mock.Setup(x => x.StopListening());

                    return mock.Object;
                });

            return mock.Object;
        }

        public new void DocumentOpened(string filePath, ITextBuffer textBuffer)
        {
            base.DocumentOpened(filePath, textBuffer);
        }

        public new void DocumentClosed(string filePath)
        {
            base.DocumentClosed(filePath);
        }

        protected override ITextBuffer GetTextBufferForOpenDocument(string filePath)
        {
            Buffers.TryGetValue(filePath, out var buffer);
            return buffer;
        }

        protected override void OnDocumentOpened(EditorDocument document)
        {
            Opened.Add(document);
        }

        protected override void OnDocumentClosed(EditorDocument document)
        {
            Closed.Add(document);
        }
    }
}
