﻿/*
 * SonarLint for Visual Studio
 * Copyright (C) 2016-2020 SonarSource SA
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
using System.Threading.Tasks;
using SonarLint.VisualStudio.Integration;
using SonarLint.VisualStudio.IssueVisualization.Security.OpenInIDE.Contract;
using SonarQube.Client;

namespace SonarLint.VisualStudio.IssueVisualization.Security.OpenInIDE.Api
{
    [Export(typeof(IOpenInIDERequestHandler))]
    internal class OpenInIDERequestHandler : IOpenInIDERequestHandler
    {
        private readonly IOpenInIDEStateValidator ideStateValidator;
        private readonly ISonarQubeService sonarQubeService;
        private readonly ILogger logger;

        [ImportingConstructor]
        public OpenInIDERequestHandler(IOpenInIDEStateValidator ideStateValidator, ISonarQubeService sonarQubeService, ILogger logger)
        {
            // MEF-created so the arguments should never be null
            this.ideStateValidator = ideStateValidator;
            this.sonarQubeService = sonarQubeService;
            this.logger = logger;
        }

        Task<IStatusResponse> IOpenInIDERequestHandler.GetStatusAsync()
        {
            throw new NotImplementedException();
        }

        Task IOpenInIDERequestHandler.ShowHotspotAsync(IShowHotspotRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // TODO: change IShowHostpotRequest server property to be a URI
            if (!ideStateValidator.CanHandleOpenInIDERequest(new Uri(request.ServerUrl), request.ProjectKey, request.OrganizationKey))
            {
                return Task.CompletedTask;
            }

            // TODO
            // * fetch hotspot data
            // * push to the data source
            // * set the selection
            return Task.CompletedTask;
        }
    }
}
