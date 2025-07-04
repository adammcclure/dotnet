﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using Microsoft.CodeAnalysis.Razor.Logging;
using Microsoft.VisualStudio.Razor.Extensions;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.Razor.Documents;

internal class VisualStudioFileChangeTracker : IFileChangeTracker, IVsFreeThreadedFileChangeEvents2
{
    private const _VSFILECHANGEFLAGS FileChangeFlags = _VSFILECHANGEFLAGS.VSFILECHG_Time | _VSFILECHANGEFLAGS.VSFILECHG_Size | _VSFILECHANGEFLAGS.VSFILECHG_Del | _VSFILECHANGEFLAGS.VSFILECHG_Add;

    private readonly ILogger _logger;
    private readonly IVsAsyncFileChangeEx _fileChangeService;
    private readonly JoinableTaskContext _joinableTaskContext;

    private readonly object _gate = new();

    // Internal for testing
    internal JoinableTask<uint>? _fileChangeAdviseTask;
    internal JoinableTask? _fileChangeUnadviseTask;
    internal JoinableTask? _fileChangedTask;

    public string FilePath { get; }

    public event EventHandler<FileChangeEventArgs>? Changed;

    public VisualStudioFileChangeTracker(
        string filePath,
        ILoggerFactory loggerFactory,
        IVsAsyncFileChangeEx fileChangeService,
        JoinableTaskContext joinableTaskContext)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            throw new ArgumentException(SR.ArgumentCannotBeNullOrEmpty, nameof(filePath));
        }

        FilePath = filePath;
        _logger = loggerFactory.GetOrCreateLogger<VisualStudioFileChangeTracker>();
        _fileChangeService = fileChangeService;
        _joinableTaskContext = joinableTaskContext;
    }

    public void StartListening()
    {
        lock (_gate)
        {
            if (_fileChangeAdviseTask is not null)
            {
                // Already listening
                return;
            }

            if (_fileChangeUnadviseTask is not { IsCompleted: false } fileChangeUnadviseTaskToJoin)
            {
                fileChangeUnadviseTaskToJoin = null;
            }

            _fileChangeAdviseTask = _joinableTaskContext.Factory.RunAsync(async () =>
            {
                try
                {
                    // If an unadvise operation is still processing, we don't start listening until it completes.
                    if (fileChangeUnadviseTaskToJoin is not null)
                        await fileChangeUnadviseTaskToJoin.JoinAsync().ConfigureAwait(true);

                    return await _fileChangeService.AdviseFileChangeAsync(FilePath, FileChangeFlags, this).ConfigureAwait(true);
                }
                catch (PathTooLongException)
                {
                    // Don't report PathTooLongExceptions but don't fault either.
                }
                catch (Exception exception)
                {
                    // Don't explode on actual exceptions, just report gracefully.
                    _logger.LogError(exception);
                }

                return VSConstants.VSCOOKIE_NIL;
            });
        }
    }

    public void StopListening()
    {
        lock (_gate)
        {
            if (_fileChangeAdviseTask is null || _fileChangeUnadviseTask?.IsCompleted == false)
            {
                // Already not listening or trying to stop listening
                return;
            }

            _fileChangeUnadviseTask = _joinableTaskContext.Factory.RunAsync(async () =>
            {
                try
                {
                    var fileChangeCookie = await _fileChangeAdviseTask;

                    if (fileChangeCookie == VSConstants.VSCOOKIE_NIL)
                    {
                        // Wasn't able to listen for file change events. This typically happens when some sort of exception (i.e. access exceptions)
                        // is thrown when attempting to listen for file changes.
                        return;
                    }

                    await _fileChangeService.UnadviseFileChangeAsync(fileChangeCookie).ConfigureAwait(true);
                    _fileChangeAdviseTask = null;
                }
                catch (PathTooLongException)
                {
                    // Don't report PathTooLongExceptions but don't fault either.
                }
                catch (Exception ex)
                {
                    // Don't explode on actual exceptions, just report gracefully.
                    _logger.LogError(ex);
                }
            });
        }
    }

    public int FilesChanged(uint fileCount, string[] filePaths, uint[] fileChangeFlags)
    {
        // Capturing task for testing purposes
        _fileChangedTask = _joinableTaskContext.Factory.RunAsync(async () =>
        {
            await _joinableTaskContext.Factory.SwitchToMainThreadAsync();

            foreach (var fileChangeFlag in fileChangeFlags)
            {
                var fileChangeKind = FileChangeKind.Changed;
                var changeFlag = (_VSFILECHANGEFLAGS)fileChangeFlag;
                if ((changeFlag & _VSFILECHANGEFLAGS.VSFILECHG_Del) == _VSFILECHANGEFLAGS.VSFILECHG_Del)
                {
                    fileChangeKind = FileChangeKind.Removed;
                }
                else if ((changeFlag & _VSFILECHANGEFLAGS.VSFILECHG_Add) == _VSFILECHANGEFLAGS.VSFILECHG_Add)
                {
                    fileChangeKind = FileChangeKind.Added;
                }

                // Purposefully not passing through the file paths here because we know this change has to do with this trackers FilePath.
                // We use that FilePath instead so any path normalization the file service did does not impact callers.
                OnChanged(fileChangeKind);
            }
        });

        return VSConstants.S_OK;
    }

    public int DirectoryChanged(string pszDirectory) => VSConstants.S_OK;

    public int DirectoryChangedEx(string pszDirectory, string pszFile) => VSConstants.S_OK;

    public int DirectoryChangedEx2(string pszDirectory, uint cChanges, string[] rgpszFile, uint[] rggrfChange) => VSConstants.S_OK;

    private void OnChanged(FileChangeKind changeKind)
    {
        _joinableTaskContext.AssertUIThread();

        if (Changed is null)
        {
            return;
        }

        var args = new FileChangeEventArgs(FilePath, changeKind);
        Changed.Invoke(this, args);
    }
}
