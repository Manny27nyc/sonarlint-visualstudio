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
using System.Net;
using System.Threading.Tasks;
using Microsoft.Owin;
using SonarLint.VisualStudio.Integration;
using SonarLint.VisualStudio.IssueVisualization.Security.OpenInIDE.Contract;

namespace SonarLint.VisualStudio.IssueVisualization.Security.OpenInIDE.Http
{
    /// <summary>
    /// Handles low-level HTTP request to open a hotspot
    /// </summary>
    [Export(typeof(IOwinPathRequestHandler))]
    internal class ShowHotspotOwinRequestHandler : IOwinPathRequestHandler
    {
        private const string ParamName_Server = "server";
        private const string ParamName_Project = "project";
        private const string ParamName_Hotspot = "hotspot";
        private const string ParamName_Organization = "organization";

        private readonly IOpenInIDERequestHandler openInIDERequestHandler;
        private readonly ILogger logger;

        [ImportingConstructor]
        internal ShowHotspotOwinRequestHandler(IOpenInIDERequestHandler openInIDERequestHandler, ILogger logger)
        {
            this.openInIDERequestHandler = openInIDERequestHandler ?? throw new ArgumentNullException(nameof(openInIDERequestHandler));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public string ApiPath => "hotspots/show";

        public Task ProcessRequest(IOwinContext context)
        {
            var request = BuildRequest(context);
            if (request == null)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return Task.CompletedTask;
            }

            // TODO - make async?
            openInIDERequestHandler.ShowHotspot(request);
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            return Task.CompletedTask;
        }

        private IShowHotspotRequest BuildRequest(IOwinContext context)
        {
            // Try to get the required parameters.
            // Note: we're not using the short-circuit operator here so all of the
            // missing parameters will be logged.
            if (TryGetParameter(context, ParamName_Server, out var server) &
                TryGetParameter(context, ParamName_Project, out var project) &
                TryGetParameter(context, ParamName_Hotspot, out var hotspot))
            {
                TryGetParameter(context, ParamName_Organization, out var organization); // optional
                return new ShowHotspotRequest(server, project, hotspot, organization);
            }

            return null;
        }

        private bool TryGetParameter(IOwinContext context, string paramName, out string value)
        {
            value = context.Request.Query.Get(paramName);

            var found = value != null;
            if (!found)
            {
                logger.WriteLine(OpenInIDEResources.OpenHotspot_MissingParameter, paramName);
            }

            return found;
        }

        private class ShowHotspotRequest : IShowHotspotRequest
        {
            public ShowHotspotRequest(string server, string project, string hotspot, string organization)
            {
                ServerUrl = server;
                ProjectKey = project;
                HotspotKey = hotspot;
                OrganizationKey = organization;
            }

            public string ServerUrl { get; }

            public string OrganizationKey { get; }

            public string ProjectKey { get; }

            public string HotspotKey { get; }
        }
    }
}