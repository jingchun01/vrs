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
using System.Text;
using System.Threading.Tasks;
using InterfaceFactory;
using VirtualRadar.Interface.Owin;

namespace VirtualRadar.Owin.Middleware
{
    using AppFunc = Func<IDictionary<string, object>, Task>;

    /// <summary>
    /// The default implementation of <see cref="IRedirectionFilter"/>.
    /// </summary>
    class RedirectionFilter : IRedirectionFilter
    {
        private IRedirectionConfiguration _RedirectionConfiguration;

        /// <summary>
        /// Creates a new object.
        /// </summary>
        public RedirectionFilter()
        {
            _RedirectionConfiguration = Factory.ResolveSingleton<IRedirectionConfiguration>();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="next"></param>
        /// <returns></returns>
        public AppFunc FilterRequest(AppFunc next)
        {
            AppFunc appFunc = async(IDictionary<string, object> environment) => {
                if(!Redirect(environment)) {
                    await next.Invoke(environment);
                }
            };

            return appFunc;
        }

        /// <summary>
        /// Examines the request, if it needs to be redirected then the response is set up and true
        /// returned otherwise false is returned.
        /// </summary>
        /// <param name="environment"></param>
        /// <returns></returns>
        private bool Redirect(IDictionary<string, object> environment)
        {
            var context = PipelineContext.GetOrCreate(environment);
            var request = context.Request;
            var response = context.Response;

            var redirectionRequestContext = new RedirectionRequestContext() {
                IsMobile = request.IsMobileUserAgentString,
            };

            var newPath = _RedirectionConfiguration.RedirectToPathFromRoot(request.PathNormalised.Value, redirectionRequestContext);
            var result = newPath != null;
            if(result) {
                response.StatusCode = (int)HttpStatusCode.Redirect;
                response.Headers.Add("Location", new string[] { BuildNewPath(request, newPath) });
            }

            return result;
        }

        /// <summary>
        /// Constructs the URL to jump to.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="newPath"></param>
        /// <returns></returns>
        private string BuildNewPath(PipelineRequest request, string newPath)
        {
            var isHttp = String.Equals(request.Scheme, "http", StringComparison.OrdinalIgnoreCase);
            var isHttps = !isHttp && String.Equals(request.Scheme, "https", StringComparison.OrdinalIgnoreCase);

            var host = request.Host.Value ?? "";
            if(isHttp && host.EndsWith(":80")) {
                host = host.Substring(0, host.Length - 3);
            } else if(isHttps && host.EndsWith(":443")) {
                host = host.Substring(0, host.Length - 4);
            }

            return PipelineContext.ConstructUrl(
                request.Scheme,
                host,
                request.PathBase.Value,
                newPath,
                request.QueryString.Value
            );
        }
    }
}
