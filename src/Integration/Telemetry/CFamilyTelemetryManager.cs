﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2021 SonarSource SA
 * mailto:info AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using System;
using System.ComponentModel.Composition;
using System.Diagnostics;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Core.CFamily;
using SonarLint.VisualStudio.Integration.Helpers;

namespace SonarLint.VisualStudio.Integration.Telemetry
{
    /// <summary>
    /// Captures telemetry about a C/C++ project types when they are opened
    /// </summary>
    public interface ICFamilyTelemetryManager : IDisposable
    {
    }

    [Export(typeof(ICFamilyTelemetryManager))]
    [PartCreationPolicy(CreationPolicy.Shared)]
    internal sealed class CFamilyTelemetryManager : ICFamilyTelemetryManager
    {
        private readonly ICFamilyProjectTypeIndicator projectTypeIndicator;
        private readonly ICompilationDatabaseLocator compilationDatabaseLocator;
        private readonly IActiveSolutionTracker activeSolutionTracker;
        private readonly ITelemetryDataRepository telemetryDataRepository;
        private readonly ILogger logger;

        [ImportingConstructor]
        public CFamilyTelemetryManager(ICFamilyProjectTypeIndicator projectTypeIndicator,
            ICompilationDatabaseLocator compilationDatabaseLocator,
            IActiveSolutionTracker activeSolutionTracker,
            ITelemetryDataRepository telemetryDataRepository,
            ILogger logger)
        {
            this.projectTypeIndicator = projectTypeIndicator;
            this.compilationDatabaseLocator = compilationDatabaseLocator;
            this.activeSolutionTracker = activeSolutionTracker;
            this.telemetryDataRepository = telemetryDataRepository;
            this.logger = logger;

            activeSolutionTracker.BeforeSolutionClosed += ActiveSolutionTracker_BeforeSolutionClosed;
            activeSolutionTracker.ActiveSolutionChanged += ActiveSolutionTracker_ActiveSolutionChanged;
        }

        private void ActiveSolutionTracker_BeforeSolutionClosed(object sender, EventArgs e)
        {
            // todo: hacky bug fix: for open-as-folder projects, SolutionOpened event is not being raised.
            UpdateTelemetry();
        }

        private void ActiveSolutionTracker_ActiveSolutionChanged(object sender, ActiveSolutionChangedEventArgs e)
        {
            if (!e.IsSolutionOpen)
            {
                return;
            }

            UpdateTelemetry();
        }

        private void UpdateTelemetry()
        {
            try
            {
                Debug.Assert(telemetryDataRepository.Data != null);

                var isCMake = projectTypeIndicator.IsCMake();

                if (isCMake)
                {
                    var compilationDatabaseLocation = compilationDatabaseLocator.Locate();
                    var isCMakeAnalyzable = !string.IsNullOrEmpty(compilationDatabaseLocation);

                    if (isCMakeAnalyzable)
                    {
                        telemetryDataRepository.Data.CFamilyProjectTypes.IsCMakeAnalyzable = true;
                    }
                    else
                    {
                        telemetryDataRepository.Data.CFamilyProjectTypes.IsCMakeNonAnalyzable = true;
                    }

                    telemetryDataRepository.Save();
                }
            }
            catch (Exception ex) when (!ErrorHandler.IsCriticalException(ex))
            {
                logger.LogDebug("[CFamilyTelemetryManager] Failed to calculate cfamily project types: {0}", ex);
            }
        }

        public void Dispose()
        {
            activeSolutionTracker.ActiveSolutionChanged -= ActiveSolutionTracker_ActiveSolutionChanged;
            activeSolutionTracker.BeforeSolutionClosed -= ActiveSolutionTracker_BeforeSolutionClosed;
        }
    }
}
