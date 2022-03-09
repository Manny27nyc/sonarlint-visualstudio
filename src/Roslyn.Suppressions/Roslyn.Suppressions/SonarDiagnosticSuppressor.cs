/*
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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarQube.Client;
using SonarQube.Client.Models;

namespace SonarLint.VisualStudio.Roslyn.Suppressions
{
    /// <summary>
    /// Diagnostic suppressor that can suppress all Sonar C# and VB.NET diagnostics
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal class SonarDiagnosticSuppressor : DiagnosticSuppressor
    {
        public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions => SupportedSuppressionsBuilder.Instance.Descriptors;

        public override void ReportSuppressions(SuppressionAnalysisContext context)
        {
            var settingsKey = new SuppressionExecutionContext(context.Options).SettingsKey;

            if (settingsKey == null)
            {
                return;
            }

            // todo: for testability, can we inject Func<IContainer> getContainer ? or IContainer and change the container to never be null
            var sonarQubeIssues = Container.Instance?.SettingsCache.GetSettings(settingsKey).ToArray();

            if (sonarQubeIssues == null || !sonarQubeIssues.Any())
            {
                return;
            }

            foreach (var diagnostic in context.ReportedDiagnostics)
            {
                var isSuppressed = Container.Instance.SuppressionsChecker.IsSuppressed(sonarQubeIssues, diagnostic);

                if (isSuppressed)
                {
                    // Find the appropriate suppression
                    var suppressionDesc = SupportedSuppressions.First(x => x.SuppressedDiagnosticId == diagnostic.Id);
                    context.ReportSuppression(Suppression.Create(suppressionDesc, diagnostic));
                }
            }
        }
    }

    internal interface ISuppressionsChecker
    {
        bool IsSuppressed(IList<SonarQubeIssue> suppressions, Diagnostic diagnostic);
    }

    internal class SuppressionsChecker : ISuppressionsChecker
    {
        private readonly IChecksumCalculator checksumCalculator;

        public SuppressionsChecker()
            : this(new ChecksumCalculator())
        {
        }

        internal SuppressionsChecker(IChecksumCalculator checksumCalculator)
        {
            this.checksumCalculator = checksumCalculator;
        }

        public bool IsSuppressed(IList<SonarQubeIssue> suppressions, Diagnostic diagnostic)
        {
            var matchFound = suppressions.Any(s => IsMatch(diagnostic, s));

            return matchFound;
        }

        /// <summary>
        /// Based on SuppressedIssueMatcher.IsMatch and RoslynLiveIssueFactory.Create
        /// </summary>
        private bool IsMatch(Diagnostic diagnostic, SonarQubeIssue serverIssue)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(diagnostic.Id, serverIssue.RuleId))
            {
                return false;
            }

            // todo: check file name SonarQubeIssuesProvider.GetSuppressedIssues(project, fileName) -- endsWith logic

            var lineSpan = diagnostic.Location.GetLineSpan();

            var isFileLevelIssue = lineSpan.StartLinePosition.Line == 0 &&
                                   lineSpan.StartLinePosition.Character == 0 &&
                                   lineSpan.EndLinePosition.Line == 0 &&
                                   lineSpan.EndLinePosition.Character == 0;

            if (isFileLevelIssue)
            {
                return serverIssue.TextRange == null;
            }

            var syntaxTree = diagnostic.Location.SourceTree;
            var lineText = syntaxTree.GetText().Lines[lineSpan.EndLinePosition.Line].ToString();
            var hash = checksumCalculator.Calculate(lineText);

            // Non-file level issue
            return lineSpan.StartLinePosition.Line == serverIssue.TextRange?.StartLine ||
                   StringComparer.Ordinal.Equals(hash, serverIssue.Hash);
        }
    }
}
