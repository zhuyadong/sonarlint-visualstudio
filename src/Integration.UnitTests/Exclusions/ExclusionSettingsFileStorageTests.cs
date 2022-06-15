﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2022 SonarSource SA
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
using System.IO.Abstractions;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Core.Analysis;
using SonarLint.VisualStudio.Core.Binding;
using SonarLint.VisualStudio.Integration.Exclusions;
using SonarLint.VisualStudio.Integration.NewConnectedMode;

namespace SonarLint.VisualStudio.Integration.UnitTests.Exclusions
{
    [TestClass]
    public class ExclusionSettingsFileStorageTests
    {
        private const string BindingFolder = "C:\\SolutionPath";
        private const string ExpectedExclusionsFilePath = "C:\\SolutionPath\\sonar.settings.json";
        private const string SerializedExclusions = "{\"Exclusions\":[\"exclusion\"],\"GlobalExclusions\":[\"globalExclusion\"],\"Inclusions\":[\"inclusion1\",\"inclusion2\"]}";
       
        [TestMethod]
        public void MefCtor_CheckIsExported()
        {
            MefTestHelpers.CheckTypeCanBeImported<ExclusionSettingsFileStorage, IExclusionSettingsFileStorage>(null, new[]
            {
                MefTestHelpers.CreateExport<IConfigurationProvider>(Mock.Of<IConfigurationProvider>()),
                MefTestHelpers.CreateExport<ILogger>(Mock.Of<ILogger>()),
            });
        }

        [TestMethod]
        public void GetSettings_StandaloneMode_ReturnsNull()
        {
            var file = new Mock<IFile>();
            file.Setup(f => f.ReadAllText(ExpectedExclusionsFilePath)).Returns(SerializedExclusions);
            file.Setup(f => f.Exists(ExpectedExclusionsFilePath)).Returns(true);

            var bindingConfiguration = BindingConfiguration.Standalone;

            var testSubject = CreateTestSubject(file.Object, bindingConfiguration);

            var settings = testSubject.GetSettings();

            settings.Should().BeNull();
            file.Invocations.Count.Should().Be(0);
        }

        [TestMethod]
        public void GetSettings_SettingFileExists_ReadsSettings()
        {
            var file = new Mock<IFile>();
            file.Setup(f => f.ReadAllText(ExpectedExclusionsFilePath)).Returns(SerializedExclusions);
            file.Setup(f => f.Exists(ExpectedExclusionsFilePath)).Returns(true);

            var testSubject = CreateTestSubject(file.Object);

            var settings = testSubject.GetSettings();

            settings.Should().NotBeNull();
            settings.Inclusions.Should().BeEquivalentTo("inclusion1", "inclusion2");
            settings.Exclusions.Should().BeEquivalentTo("exclusion");
            settings.GlobalExclusions.Should().BeEquivalentTo("globalExclusion");
        }

        [TestMethod]
        public void GetSettings_SettingFileDoesNotExist_ReturnsNull()
        {
            var file = new Mock<IFile>();
            file.Setup(f => f.Exists(ExpectedExclusionsFilePath)).Returns(false);

            var testSubject = CreateTestSubject(file.Object);

            var settings = testSubject.GetSettings();

            settings.Should().BeNull();
            file.VerifyAll();
            file.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void GetSettings_FileReadError_ReturnsNull()
        {
            var file = new Mock<IFile>();
            file.Setup(f => f.Exists(ExpectedExclusionsFilePath)).Returns(true);
            file.Setup(f => f.ReadAllText(ExpectedExclusionsFilePath)).Throws(new Exception("File Read Error"));

            var testSubject = CreateTestSubject(file.Object);

            var settings = testSubject.GetSettings();

            settings.Should().BeNull();
            file.VerifyAll();
            file.VerifyNoOtherCalls();
        }

        [TestMethod]
        public void GetSettings_SerializationError_ReturnsNull()
        {
            var file = new Mock<IFile>();
            file.Setup(f => f.Exists(ExpectedExclusionsFilePath)).Returns(true);
            file.Setup(f => f.ReadAllText(ExpectedExclusionsFilePath)).Returns("Wrong String");

            var testSubject = CreateTestSubject(file.Object);

            var settings = testSubject.GetSettings();

            settings.Should().BeNull();
            file.VerifyAll();
            file.VerifyNoOtherCalls();
        }

        private ExclusionSettingsFileStorage CreateTestSubject(IFile file, BindingConfiguration bindingConfiguration = null)
        {
            var fileSystem = CreateFileSystem(file);

            bindingConfiguration ??= new BindingConfiguration(null, SonarLintMode.Connected, BindingFolder);
            var configurationProviderService = CreateConfigurationProvider(bindingConfiguration);

            return new ExclusionSettingsFileStorage(configurationProviderService, Mock.Of<ILogger>(), fileSystem);
        }

        private static IConfigurationProvider CreateConfigurationProvider(BindingConfiguration bindingConfiguration)
        {
            var configurationProviderService = new Mock<IConfigurationProvider>();
            configurationProviderService.Setup(c => c.GetConfiguration()).Returns(bindingConfiguration);
            
            return configurationProviderService.Object;
        }

        private IFileSystem CreateFileSystem(IFile file)
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.SetupGet(fs => fs.File).Returns(file);

            return fileSystem.Object;
        }
    }
}
