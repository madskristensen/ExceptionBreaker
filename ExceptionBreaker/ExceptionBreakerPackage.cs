﻿using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.ComponentModel.Design;
using EnvDTE;
using ExceptionBreaker.Implementation;
using ExceptionBreaker.Implementation.VersionSpecific;
using ExceptionBreaker.Options;
using ExceptionBreaker.Options.ImprovedComponentModel;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.Shell;

namespace ExceptionBreaker
{
    /// <summary>
    /// This is the class that implements the package exposed by this assembly.
    ///
    /// The minimum requirement for a class to be considered a valid package for Visual Studio
    /// is to implement the IVsPackage interface and register itself with the shell.
    /// This package uses the helper classes defined inside the Managed Package Framework (MPF)
    /// to do it: it derives from the Package class that provides the implementation of the 
    /// IVsPackage interface and uses the registration attributes defined in the framework to 
    /// register itself and its components with the shell.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true)]
    [InstalledProductRegistration("#110", "#112", "1.0", IconResourceID = 400)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists)]
    [ProvideAutoLoad(UIContextGuids80.Debugging)]
    [ProvideMenuResource("Menus.2012.ctmenu", 1)]
    [ProvideOptionPageInExistingCategory(typeof(OptionsPageData), "Debugger", "ExceptionBreaker", 110)]
    [Guid(GuidList.PackageString)]
    public sealed class ExceptionBreakerPackage : Package {
        private CommandController _controller;
        private DTE _dte;

        public IDiagnosticLogger Logger { get; private set; }
        public ExceptionBreakManager ExceptionBreakManager { get; private set; }

        public static ExceptionBreakerPackage Current { get; private set; }

        /// <summary>
        /// Default constructor of the package.
        /// Inside this method you can place any initialization code that does not require 
        /// any Visual Studio service because at this point the package object is created but 
        /// not sited yet inside Visual Studio environment. The place to do all the other 
        /// initialization is the Initialize method.
        /// </summary>
        public ExceptionBreakerPackage()
        {
            Trace.WriteLine(string.Format(CultureInfo.CurrentCulture, "Entering constructor for: {0}", this));
            Current = this;
        }
        
        /// <summary>
        /// Initialization of the package; this method is called right after the package is sited, so this is the place
        /// where you can put all the initialization code that rely on services provided by VisualStudio.
        /// </summary>
        protected override void Initialize()
        {
            Trace.WriteLine (string.Format(CultureInfo.CurrentCulture, "Entering Initialize() of: {0}", this));
            base.Initialize();
            Logger = new ExtensionLogger("ExceptionBreaker", paneCaption => GetOutputPane(GuidList.OutputPane, paneCaption));

            _dte = (DTE)GetService(typeof(DTE));
            SetupExceptionBreakManager();

            // Add our command handlers for menu (commands must exist in the .vsct file)
            SetupCommandController();
        }

        private void SetupExceptionBreakManager() {
            var versionSpecificFactory = new VersionSpecificAdapterFactory(_dte);
            var debugger = GetGlobalService(typeof (SVsShellDebugger));
            var sessionManager = new DebugSessionManager(versionSpecificFactory.AdaptDebuggerInternal(debugger), Logger);
            var optionsPage = new Lazy<OptionsPageData>(() => (OptionsPageData)GetDialogPage(typeof (OptionsPageData)));
            ExceptionBreakManager = new ExceptionBreakManager(
                sessionManager,
                name => optionsPage.Value.Ignored.Any(p => p.Matches(name)),
                Logger
            );
        }

        private void SetupCommandController() {
            var menuCommandService = GetService(typeof(IMenuCommandService)) as OleMenuCommandService;
            Func<EventHandler, MenuCommand> initBreakOnAllCommand = callback => {
                var command = new OleMenuCommand(id: CommandIDs.BreakOn, invokeHandler: callback);
                menuCommandService.AddCommand(command);

                return command;
            };

            var monitorSelection = (IVsMonitorSelection)this.GetService(typeof(IVsMonitorSelection));
            _controller = new CommandController(_dte, initBreakOnAllCommand, monitorSelection, ExceptionBreakManager, Logger);
        }
    }
}
