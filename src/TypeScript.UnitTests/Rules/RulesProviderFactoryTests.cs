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
using System.IO;
using System.Linq;
using FluentAssertions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using SonarLint.VisualStudio.Core;
using SonarLint.VisualStudio.Integration.UnitTests;
using SonarLint.VisualStudio.TypeScript.Rules;

namespace SonarLint.VisualStudio.TypeScript.UnitTests.Rules
{
    [TestClass]
    public class RulesProviderFactoryTests
    {
        private static readonly IUserSettingsProvider ValidUserSettingsProvider = Mock.Of<IUserSettingsProvider>();

        [TestMethod]
        public void MefCtor_CheckIsExported()
        {
            var jsonFilePath = GetRuleDefinitionFilePath("RuleDefns_Valid.json");

            MefTestHelpers.CheckTypeCanBeImported<RulesProviderFactory, IRulesProviderFactory>(null, new[]
            {
                MefTestHelpers.CreateExport<string>(jsonFilePath, RulesProviderFactory.RuleDefinitionsFilePathContractName),
                MefTestHelpers.CreateExport<IUserSettingsProvider>(Mock.Of<IUserSettingsProvider>())
            });
        }

        [TestMethod]
        [DataRow("")]
        [DataRow(null)]
        public void Create_InvalidPrefix_Throws(string prefix)
        {
            var jsonFilePath = GetRuleDefinitionFilePath("RuleDefns_Valid.json");

            var testSubject = new RulesProviderFactory(jsonFilePath, ValidUserSettingsProvider);

            Action act = () => testSubject.Create(prefix);

            act.Should().ThrowExactly<ArgumentNullException>().And.ParamName.Should().Be("repoKey");
        }

        [TestMethod]
        public void Create_ReturnsExpectedRulesForLanguage()
        {
            var jsonFilePath = GetRuleDefinitionFilePath("RuleDefns_CheckLanguageFiltering.json");

            var testSubject = new RulesProviderFactory(jsonFilePath, ValidUserSettingsProvider);

            // 1. TypeScript
            var tsRuleKeys = testSubject.Create("typescript").GetDefinitions()
                .Select(x => x.RuleKey);

            tsRuleKeys.Should().BeEquivalentTo("typescript:S2092", "typescript:S3524", "TypeSCRIPT:S1135");

            // 2. JavaScript
            var jsRuleKeys = testSubject.Create("javascript").GetDefinitions()
                .Select(x => x.RuleKey);

            jsRuleKeys.Should().BeEquivalentTo("javascript:S1135", "JAVASCRIPT:xyz");

            // 3. Unrecognized language
            var result = testSubject.Create("unknown");
            result.GetDefinitions().Should().BeEmpty();
        }

        [TestMethod]
        public void Create_GetDefinitions_ReturnsExpectedProperties()
        {
            // Checking the detailed definition properties for a single language
            var jsonFilePath = GetRuleDefinitionFilePath("RuleDefns_CheckDetailedProperties.json");
            var testSubject = new RulesProviderFactory(jsonFilePath, ValidUserSettingsProvider);

            var result = testSubject.Create("typescript")
                .GetDefinitions().ToArray();

            result.Should().HaveCount(2);

            // Rule 0 - no tags, params or default params
            result[0].RuleKey.Should().Be("typescript:S2092");
            result[0].Type.Should().Be(RuleType.SECURITY_HOTSPOT);
            result[0].Name.Should().Be("Creating cookies without the 'secure' flag is security-sensitive");
            result[0].Severity.Should().Be(RuleSeverity.MINOR);
            result[0].Tags.Should().BeEmpty();
            result[0].Params.Should().BeEmpty();
            result[0].DefaultParams.Should().HaveCount(0);
            result[0].Scope.Should().Be(RuleScope.MAIN);
            result[0].EslintKey.Should().Be("insecure-cookie");
            result[0].ActivatedByDefault.Should().BeTrue();

            // Rule 1 - has tags, params and default params
            result[1].RuleKey.Should().Be("typescript:S3524");
            result[1].Type.Should().Be(RuleType.CODE_SMELL);
            result[1].Name.Should().Be("Braces and parentheses should be used consistently with arrow functions");
            result[1].Severity.Should().Be(RuleSeverity.INFO);
            result[1].Tags.Should().BeEquivalentTo("convention", "es2015");
            result[1].Params.Should().HaveCount(2);
            result[1].DefaultParams.Should().HaveCount(1);
            result[1].Scope.Should().Be(RuleScope.BOTH);
            result[1].EslintKey.Should().Be("arrow-function-convention");
            result[1].ActivatedByDefault.Should().BeFalse();
        }

        [TestMethod]
        public void Create_GetDefinitions_WithDefaultParams_ReturnsExpectedConfigs()
        {
            // This test is illustrative of the different types of default configs that 
            // are used. The actual values are largely opaque as far as SLVS is concerned,
            // so this test just does a quick sanity check that the expected number of
            // configs are being fetched.
            var jsonFilePath = GetRuleDefinitionFilePath("RuleDefns_CheckDefaultParams.json");
            var testSubject = new RulesProviderFactory(jsonFilePath, ValidUserSettingsProvider );

            var result = testSubject.Create("javascript")
                .GetDefinitions().ToArray();

            result.Should().HaveCount(4);

            result[0].DefaultParams.Should().HaveCount(0);
            result[1].DefaultParams.Should().HaveCount(3);
            result[2].DefaultParams.Should().HaveCount(1);
            result[3].DefaultParams.Should().HaveCount(2);
        }

        [TestMethod]
        public void GetActiveRulesConfig_ReturnsExpected()
        {
            // Sanity check that factory returns a provider that returns the active config
            var jsonFilePath = GetRuleDefinitionFilePath("RuleDefns_CheckActiveRulesConfig.json");

            var settingsProvider = new Mock<IUserSettingsProvider>();
            settingsProvider.Setup(x => x.UserSettings).Returns(new UserSettings(new RulesSettings()));

            var testSubject = new RulesProviderFactory(jsonFilePath, settingsProvider.Object);

            var result = testSubject.Create("typescript")
                .GetActiveRulesConfiguration().ToArray();

            result.Should().HaveCount(2);
        }

        private static string GetRuleDefinitionFilePath(string fileName)
        {
            var sampleJsonFile = Path.Combine(
                Path.GetDirectoryName(typeof(RulesProviderFactoryTests).Assembly.Location),
                "" +
                "Rules", fileName);

            File.Exists(sampleJsonFile).Should().BeTrue("Test setup error: could not find sample data file: " + sampleJsonFile);
            return sampleJsonFile;
        }

    }
}
