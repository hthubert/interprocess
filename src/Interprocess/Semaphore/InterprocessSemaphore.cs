﻿using System.IO;
using Cloudtoid.Interprocess.Semaphore.Unix;
using Microsoft.Extensions.Logging;
using WinSemaphore = Cloudtoid.Interprocess.Semaphore.Windows.Semaphore;

namespace Cloudtoid.Interprocess
{
    /// <summary>
    /// This class opens or creates platform agnostic named semaphore. Named
    /// semaphores are synchronization constructs accessible across processes.
    /// </summary>
    /// <remarks>
    /// .NET Core 3.1  and .NET 5 do not have support for named semaphores on
    /// Unix based OSs (Linux, macOS, etc.). To replicate a named semaphore in
    /// the most efficient possible way, we are using Unix Domain Sockets to send
    /// signals between processes.
    ///
    /// It is worth mentioning that we support multiple signal publishers and
    /// receivers; therefore, you will find some logic on Unix to utilize multiple
    /// named sockets. We also use a file system watcher to keep track of the
    /// addition and removal of signal publishers (Unix Domain Sockets use backing
    /// files).
    ///
    /// The domain socket implementation should be removed and replaced with
    /// <see cref="System.Threading.Semaphore"/> once named semaphores are
    /// supported on all platforms.
    /// </remarks>
    internal static class InterprocessSemaphore
    {
        internal static IInterprocessSemaphoreWaiter CreateWaiter(
            SharedAssetsIdentifier identifier,
            ILoggerFactory loggerFactory)
        {
            if (Util.IsUnixBased)
            {
                identifier = CreateUnixIdentifier(identifier);
                return new SemaphoreWaiter(identifier, loggerFactory);
            }

            return new WinSemaphore(identifier);
        }

        internal static IInterprocessSemaphoreReleaser CreateReleaser(
            SharedAssetsIdentifier identifier,
            ILoggerFactory loggerFactory)
        {
            if (Util.IsUnixBased)
            {
                identifier = CreateUnixIdentifier(identifier);
                return new SemaphoreReleaser(identifier, loggerFactory);
            }

            return new WinSemaphore(identifier);
        }

        private static SharedAssetsIdentifier CreateUnixIdentifier(this SharedAssetsIdentifier identifier)
        {
            const string PathSuffix = ".cloudtoid/semaphore";
            var path = Path.Combine(identifier.Path, PathSuffix);
            Directory.CreateDirectory(path);
            return new SharedAssetsIdentifier(identifier.Name, path);
        }
    }
}
