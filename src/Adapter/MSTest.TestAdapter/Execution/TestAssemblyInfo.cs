// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Defines TestAssembly Info object.
/// </summary>
public class TestAssemblyInfo
{
    private readonly object _assemblyInfoExecuteSyncObject;

    private MethodInfo? _assemblyInitializeMethod;
    private MethodInfo? _assemblyCleanupMethod;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestAssemblyInfo"/> class.
    /// </summary>
    /// <param name="assembly">Sets the <see cref="Assembly"/> this class is representing. </param>
    internal TestAssemblyInfo(Assembly assembly)
    {
        _assemblyInfoExecuteSyncObject = new object();
        Assembly = assembly;
    }

    /// <summary>
    /// Gets <c>AssemblyInitialize</c> method for the assembly.
    /// </summary>
    public MethodInfo? AssemblyInitializeMethod
    {
        get => _assemblyInitializeMethod;

        internal set
        {
            if (_assemblyInitializeMethod != null)
            {
                DebugEx.Assert(_assemblyInitializeMethod.DeclaringType?.FullName is not null, "AssemblyInitializeMethod.DeclaringType.FullName is null");
                var message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiAssemblyInit, _assemblyInitializeMethod.DeclaringType.FullName);
                throw new TypeInspectionException(message);
            }

            _assemblyInitializeMethod = value;
        }
    }

    /// <summary>
    /// Gets <c>AssemblyCleanup</c> method for the assembly.
    /// </summary>
    public MethodInfo? AssemblyCleanupMethod
    {
        get => _assemblyCleanupMethod;

        internal set
        {
            if (_assemblyCleanupMethod != null)
            {
                DebugEx.Assert(_assemblyCleanupMethod.DeclaringType?.FullName is not null, "AssemblyCleanupMethod.DeclaringType.FullName is null");
                string message = string.Format(CultureInfo.CurrentCulture, Resource.UTA_ErrorMultiAssemblyClean, _assemblyCleanupMethod.DeclaringType.FullName);
                throw new TypeInspectionException(message);
            }

            _assemblyCleanupMethod = value;
        }
    }

    /// <summary>
    /// Gets a value indicating whether <c>AssemblyInitialize</c> has been executed.
    /// </summary>
    public bool IsAssemblyInitializeExecuted { get; internal set; }

    /// <summary>
    /// Gets the assembly initialization exception.
    /// </summary>
    public Exception? AssemblyInitializationException { get; internal set; }

    /// <summary>
    /// Gets a value indicating whether this assembly has an executable <c>AssemblyCleanup</c> method.
    /// </summary>
    public bool HasExecutableCleanupMethod
    {
        get
        {
            // If no assembly cleanup, then continue with the next one.
            if (AssemblyCleanupMethod == null)
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Gets the <see cref="Assembly"/> this class represents.
    /// </summary>
    internal Assembly Assembly { get; }

    /// <summary>
    /// Runs assembly initialize method.
    /// </summary>
    /// <param name="testContext"> The test context. </param>
    /// <exception cref="TestFailedException"> Throws a test failed exception if the initialization method throws an exception. </exception>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public void RunAssemblyInitialize(TestContext testContext)
    {
        // No assembly initialize => nothing to do.
        if (AssemblyInitializeMethod == null)
        {
            return;
        }

        if (testContext == null)
        {
            throw new NullReferenceException(Resource.TestContextIsNull);
        }

        // If assembly initialization is not done, then do it.
        if (!IsAssemblyInitializeExecuted)
        {
            // Acquiring a lock is usually a costly operation which does not need to be
            // performed every time if the assembly initialization is already executed.
            lock (_assemblyInfoExecuteSyncObject)
            {
                // Perform a check again.
                if (!IsAssemblyInitializeExecuted)
                {
                    try
                    {
                        AssemblyInitializeMethod.InvokeAsSynchronousTask(null, testContext);
                    }
                    catch (Exception ex)
                    {
                        AssemblyInitializationException = ex;
                    }
                    finally
                    {
                        IsAssemblyInitializeExecuted = true;
                    }
                }
            }
        }

        // If assemblyInitialization was successful, then don't do anything
        if (AssemblyInitializationException == null)
        {
            return;
        }

        // Cache and return an already created TestFailedException.
        if (AssemblyInitializationException is TestFailedException)
        {
            throw AssemblyInitializationException;
        }

        var realException = AssemblyInitializationException.InnerException ?? AssemblyInitializationException;

        var outcome = realException is AssertInconclusiveException ? UnitTestOutcome.Inconclusive : UnitTestOutcome.Failed;

        // Do not use StackTraceHelper.GetExceptionMessage(realException) as it prefixes the message with the exception type name.
        var exceptionMessage = realException.TryGetMessage();
        DebugEx.Assert(AssemblyInitializeMethod.DeclaringType?.FullName is not null, "AssemblyInitializeMethod.DeclaringType.FullName is null");
        var errorMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_AssemblyInitMethodThrows,
            AssemblyInitializeMethod.DeclaringType.FullName,
            AssemblyInitializeMethod.Name,
            realException.GetType().ToString(),
            exceptionMessage);
        var exceptionStackTraceInfo = StackTraceHelper.GetStackTraceInformation(realException);

        var testFailedException = new TestFailedException(outcome, errorMessage, exceptionStackTraceInfo, realException);
        AssemblyInitializationException = testFailedException;

        throw testFailedException;
    }

    /// <summary>
    /// Run assembly cleanup methods.
    /// </summary>
    /// <returns>
    /// Any exception that can be thrown as part of a assembly cleanup as warning messages.
    /// </returns>
    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
    public string? RunAssemblyCleanup()
    {
        if (AssemblyCleanupMethod == null)
        {
            return null;
        }

        lock (_assemblyInfoExecuteSyncObject)
        {
            try
            {
                AssemblyCleanupMethod.InvokeAsSynchronousTask(null);

                return null;
            }
            catch (Exception ex)
            {
                var realException = ex.InnerException ?? ex;

                string errorMessage;

                // special case AssertFailedException to trim off part of the stack trace
                if (realException is AssertFailedException or AssertInconclusiveException)
                {
                    errorMessage = realException.Message;
                }
                else
                {
                    errorMessage = StackTraceHelper.GetExceptionMessage(realException);
                }

                DebugEx.Assert(AssemblyCleanupMethod.DeclaringType?.Name is not null, "AssemblyCleanupMethod.DeclaringType.Name is null");
                return string.Format(
                    CultureInfo.CurrentCulture,
                    Resource.UTA_AssemblyCleanupMethodWasUnsuccesful,
                    AssemblyCleanupMethod.DeclaringType.Name,
                    AssemblyCleanupMethod.Name,
                    errorMessage,
                    StackTraceHelper.GetStackTraceInformation(realException)?.ErrorStackTrace);
            }
        }
    }

    /// <summary>
    /// Calls the assembly cleanup method in a thread-safe.
    /// </summary>
    /// <remarks>
    /// It is a replacement for RunAssemblyCleanup but as we are in a bug-fix version, we do not want to touch
    /// public API and so we introduced this method.
    /// </remarks>
    internal void ExecuteAssemblyCleanup()
    {
        if (AssemblyCleanupMethod == null)
        {
            return;
        }

        lock (_assemblyInfoExecuteSyncObject)
        {
            try
            {
                AssemblyCleanupMethod.InvokeAsSynchronousTask(null);
            }
            catch (Exception ex)
            {
                var realException = ex.InnerException ?? ex;

                string errorMessage;

                // special case AssertFailedException to trim off part of the stack trace
                if (realException is AssertFailedException or AssertInconclusiveException)
                {
                    errorMessage = realException.Message;
                }
                else
                {
                    errorMessage = StackTraceHelper.GetExceptionMessage(realException);
                }

                var exceptionStackTraceInfo = StackTraceHelper.GetStackTraceInformation(realException);
                DebugEx.Assert(AssemblyCleanupMethod.DeclaringType?.Name is not null, "AssemblyCleanupMethod.DeclaringType.Name is null");

                throw new TestFailedException(
                    UnitTestOutcome.Failed,
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.UTA_AssemblyCleanupMethodWasUnsuccesful,
                        AssemblyCleanupMethod.DeclaringType.Name,
                        AssemblyCleanupMethod.Name,
                        errorMessage,
                        exceptionStackTraceInfo?.ErrorStackTrace),
                    exceptionStackTraceInfo,
                    realException);
            }
        }
    }
}
