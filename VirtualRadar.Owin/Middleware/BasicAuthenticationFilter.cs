﻿// Copyright © 2017 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;
using InterfaceFactory;
using Microsoft.Owin;
using VirtualRadar.Interface.Owin;
using VirtualRadar.Interface.Settings;


namespace VirtualRadar.Owin.Middleware
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    /// <summary>
    /// Default implementation of <see cref="IBasicAuthenticationFilter"/>.
    /// </summary>
    class BasicAuthenticationFilter : IBasicAuthenticationFilter
    {
        /// <summary>
        /// The shared configuration that we'll be using.
        /// </summary>
        private ISharedConfiguration _SharedConfiguration;

        /// <summary>
        /// The authentication configuration that supplies administrator paths etc.
        /// </summary>
        private IAuthenticationConfiguration _AuthenticationConfiguration;

        /// <summary>
        /// The object that holds all of the common code for basic authentication.
        /// </summary>
        private BasicAuthenticationHelper _BasicAuthentication;

        /// <summary>
        /// Creates a new object.
        /// </summary>
        public BasicAuthenticationFilter()
        {
            _SharedConfiguration = Factory.ResolveSingleton<ISharedConfiguration>();
            _AuthenticationConfiguration = Factory.ResolveSingleton<IAuthenticationConfiguration>();
            _BasicAuthentication = new BasicAuthenticationHelper();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        public AppFunc FilterRequest(AppFunc next)
        {
            AppFunc appFunc = async(IDictionary<string, object> environment) => {
                if(Authenticated(environment)) {
                    await next.Invoke(environment);
                }
            };

            return appFunc;
        }

        /// <summary>
        /// Returns true if the request is authenticated, false otherwise. If the request has not been
        /// authenticated then pipeline processing should be stopped.
        /// </summary>
        /// <param name="environment"></param>
        /// <returns></returns>
        private bool Authenticated(IDictionary<string, object> environment)
        {
            var result = true;

            var sharedConfig = _SharedConfiguration.Get();
            var context = PipelineContext.GetOrCreate(environment);
            var request = context.Request;

            var isAdminOnlyPath = _AuthenticationConfiguration.IsAdministratorPath(request.PathNormalised.Value);
            var isGlobalAuthenticationEnabled = sharedConfig.WebServerSettings.AuthenticationScheme == AuthenticationSchemes.Basic;

            if(isAdminOnlyPath || isGlobalAuthenticationEnabled) {
                result = false;

                string userName = null;
                string password = null;
                if(ExtractCredentials(request, ref userName, ref password)) {
                    var cachedUser = _BasicAuthentication.GetCachedUser(userName);
                    var cachedUserTag = _BasicAuthentication.GetCachedUserTag(cachedUser);
                    var isPasswordValid = _BasicAuthentication.IsPasswordValid(cachedUser, cachedUserTag, password);

                    result = isPasswordValid && (!isAdminOnlyPath || cachedUser.IsAdministrator);
                    if(result) {
                        request.User = _BasicAuthentication.CreatePrincipal(cachedUser, cachedUserTag);
                    }
                }

                if(!result) {
                    SendNeedsAuthenticationResponse(environment);
                }
            }

            return result;
        }

        /// <summary>
        /// Extracts the username and password from a basic Authorize header. Returns false if the header
        /// is missing or badly formatted.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="userName"></param>
        /// <param name="password"></param>
        /// <returns></returns>
        private bool ExtractCredentials(OwinRequest request, ref string userName, ref string password)
        {
            var authorizationMime64 = request.Headers["Authorization"] ?? "";
            return _BasicAuthentication.ExtractCredentials(authorizationMime64, ref userName, ref password);
        }

        /// <summary>
        /// Responds with a request for authentication details.
        /// </summary>
        /// <param name="environment"></param>
        private void SendNeedsAuthenticationResponse(IDictionary<string, object> environment)
        {
            var response = new OwinResponse(environment);
            response.StatusCode = (int)HttpStatusCode.Unauthorized;
            response.Headers.Add("WWW-Authenticate", new string[] { "Basic Realm=\"Virtual Radar Server\", charset=\"UTF-8\"" });
        }
    }
}
