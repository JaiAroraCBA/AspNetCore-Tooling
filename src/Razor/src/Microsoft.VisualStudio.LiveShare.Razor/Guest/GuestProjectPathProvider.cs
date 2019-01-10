﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel.Composition;
using System.Threading;
using Microsoft.CodeAnalysis.Razor;
using Microsoft.VisualStudio.Editor.Razor;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Threading;

namespace Microsoft.VisualStudio.LiveShare.Razor.Guest
{
    [System.Composition.Shared]
    [Export(typeof(LiveShareProjectPathProvider))]
    internal class GuestProjectPathProvider : LiveShareProjectPathProvider
    {
        private readonly ForegroundDispatcher _foregroundDispatcher;
        private readonly JoinableTaskFactory _joinableTaskFactory;
        private readonly ITextDocumentFactoryService _textDocumentFactory;
        private readonly ProxyAccessor _proxyAccessor;
        private readonly LiveShareClientProvider _liveShareClientProvider;
        private readonly RazorGuestRemoteSessionDetector _guestRemoteSessionDetector;

        [ImportingConstructor]
        public GuestProjectPathProvider(
            ForegroundDispatcher foregroundDispatcher,
            JoinableTaskContext joinableTaskContext,
            ITextDocumentFactoryService textDocumentFactory,
            ProxyAccessor proxyAccessor,
            LiveShareClientProvider liveShareClientProvider,
            RazorGuestRemoteSessionDetector guestRemoteSessionDetector)
        {
            if (foregroundDispatcher == null)
            {
                throw new ArgumentNullException(nameof(foregroundDispatcher));
            }

            if (joinableTaskContext == null)
            {
                throw new ArgumentNullException(nameof(joinableTaskContext));
            }

            if (textDocumentFactory == null)
            {
                throw new ArgumentNullException(nameof(textDocumentFactory));
            }

            if (proxyAccessor == null)
            {
                throw new ArgumentNullException(nameof(proxyAccessor));
            }

            if (liveShareClientProvider == null)
            {
                throw new ArgumentNullException(nameof(liveShareClientProvider));
            }

            if (guestRemoteSessionDetector == null)
            {
                throw new ArgumentNullException(nameof(guestRemoteSessionDetector));
            }

            _foregroundDispatcher = foregroundDispatcher;
            _joinableTaskFactory = joinableTaskContext.Factory;
            _textDocumentFactory = textDocumentFactory;
            _proxyAccessor = proxyAccessor;
            _liveShareClientProvider = liveShareClientProvider;
            _guestRemoteSessionDetector = guestRemoteSessionDetector;
        }

        public override bool TryGetProjectPath(ITextBuffer textBuffer, out string filePath)
        {
            if (!_guestRemoteSessionDetector.IsRemoteSession)
            {
                filePath = null;
                return false;
            }

            if (!_textDocumentFactory.TryGetTextDocument(textBuffer, out var textDocument))
            {
                filePath = null;
                return false;
            }

            var hostProjectPath = GetHostProjectPath(textDocument);
            if (hostProjectPath == null)
            {
                filePath = null;
                return false;
            }

            // Host always responds with a host-based path, convert back to a guest one.
            filePath = _liveShareClientProvider.ConvertToLocalPath(hostProjectPath);
            return true;
        }

        // Internal virtual for testing
        internal virtual Uri GetHostProjectPath(ITextDocument textDocument)
        {
            // The path we're given is from the guest so following other patterns we always ask the host information in its own form (aka convert on guest instead of on host).
            var ownerPath = _liveShareClientProvider.ConvertToSharedUri(textDocument.FilePath);

            var hostProjectPath = _joinableTaskFactory.Run(() =>
            {
                var projectHierarchyProxy = _proxyAccessor.GetProjectHierarchyProxy();

                // We need to block the foreground thread to get a proper project path. However, this is only done once on opening thedocument.
                return projectHierarchyProxy.GetProjectPathAsync(ownerPath, CancellationToken.None);
            });

            return hostProjectPath;
        }
    }
}