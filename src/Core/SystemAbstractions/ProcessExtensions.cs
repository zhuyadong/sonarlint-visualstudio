/*
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

using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SonarLint.VisualStudio.Core.SystemAbstractions
{
    internal static class ProcessExtensions
    {
        public static Task<bool> WaitForExitAsync(this Process process, int timeoutInMs)
        {
            bool result = process.WaitForExit(timeoutInMs);
            return Task.FromResult(result);
        }

        public async static Task<int> WaitForExitAsync2(this Process process, CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<int>();

            try
            {
                process.EnableRaisingEvents = true;
                process.Exited += Process_Exited;

                // The process might already have finished
                if (process.HasExited)
                {
                    tcs.TrySetResult(process.ExitCode);
                }
                using(cancellationToken.Register(OnCancellationTokenCancelled))
                {
                    return await tcs.Task.ConfigureAwait(continueOnCapturedContext: false);
                }
            }
            finally
            {
                process.Exited -= Process_Exited;
            }

            void Process_Exited(object sender, System.EventArgs e) => tcs.TrySetResult(process.ExitCode);

            void OnCancellationTokenCancelled() => tcs.TrySetCanceled(cancellationToken);
        }
    }
}
