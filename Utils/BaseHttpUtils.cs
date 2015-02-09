﻿using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using ManyWho.Flow.SDK;
using ManyWho.Flow.SDK.Security;
using ManyWho.Flow.SDK.Utils;

///*!

//Copyright 2013 Manywho, Inc.

//Licensed under the Manywho License, Version 1.0 (the "License"); you may not use this
//file except in compliance with the License.

//You may obtain a copy of the License at: http://manywho.com/sharedsource

//Unless required by applicable law or agreed to in writing, software distributed under
//the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
//KIND, either express or implied. See the License for the specific language governing
//permissions and limitations under the License.

//*/

namespace ManyWho.Service.Salesforce.Utils
{
    public class BaseHttpUtils
    {
        public const Int32 MAXIMUM_RETRIES = 3;
        public const Int32 TIMEOUT_SECONDS = 20;
        public const Int32 SYSTEM_TIMEOUT_SECONDS = 500;

        public const String HEADER_AUTHORIZATION = "Authorization";
        public const String HEADER_MANYWHO_STATE = "ManyWhoState";
        public const String HEADER_MANYWHO_TENANT = "ManyWhoTenant";
        public const String HEADER_CULTURE = "Culture";

        /// <summary>
        /// Utility method for getting the authenticated who from the header.
        /// </summary>
        public static IAuthenticatedWho GetWho(String authorizationHeader)
        {
            IAuthenticatedWho authenticatedWho = null;

            // Check to see if it's null - it can be in some situations
            if (authorizationHeader != null &&
                authorizationHeader.Trim().Length > 0)
            {
                // Deserialize into an object
                authenticatedWho = AuthenticationUtils.Deserialize(Uri.EscapeDataString(authorizationHeader));
            }

            return authenticatedWho;
        }

        public static WebException HandleUnsuccessfulHttpResponseMessage(INotifier notifier, IAuthenticatedWho authenticatedWho, Int32 iteration, HttpResponseMessage httpResponseMessage, String endpointUrl)
        {
            WebException webException = null;

            if (iteration >= (MAXIMUM_RETRIES - 1))
            {
                // The the alert email the fault
                ErrorUtils.SendAlert(notifier, authenticatedWho, "Fault", "The system has attempted multiple retries (" + MAXIMUM_RETRIES + ") with no luck on: " + endpointUrl + ". The status code is: " + httpResponseMessage.StatusCode + ". The reason is: " + httpResponseMessage.ReasonPhrase);

                // Throw the fault up to the caller
                webException = new WebException(httpResponseMessage.ReasonPhrase);
            }
            else
            {
                // Alert the admin that a retry has happened
                ErrorUtils.SendAlert(notifier, authenticatedWho, "Warning", "The system is attempting a retry (" + iteration + ") on: " + endpointUrl + ". The status code is: " + httpResponseMessage.StatusCode + ". The reason is: " + httpResponseMessage.ReasonPhrase);
            }

            return webException;
        }

        public static WebException HandleHttpException(INotifier notifier, IAuthenticatedWho authenticatedWho, Int32 iteration, Exception exception, String endpointUrl)
        {
            WebException webException = null;

            if (iteration >= (MAXIMUM_RETRIES - 1))
            {
                // The the alert email the fault
                ErrorUtils.SendAlert(notifier, authenticatedWho, "Fault", "The system has attempted multiple retries (" + MAXIMUM_RETRIES + ") with no luck on: " + endpointUrl + ". The error message we're getting back is: " + ErrorUtils.GetExceptionMessage(exception));

                // Throw the fault up to the caller
                webException = new WebException(ErrorUtils.GetExceptionMessage(exception));
            }
            else
            {
                // Alert the admin that a retry has happened
                ErrorUtils.SendAlert(notifier, authenticatedWho, "Warning", "The system is attempting a retry (" + iteration + ") on: " + endpointUrl + ". The error message we're getting back is: " + ErrorUtils.GetExceptionMessage(exception));
            }

            return webException;
        }

        public static HttpClient CreateHttpClient(IAuthenticatedWho authenticatedWho, String tenantId, String stateId)
        {
            return CreateHttpClient(authenticatedWho, tenantId, stateId, TIMEOUT_SECONDS);
        }

        public static HttpClient CreateHttpClient(IAuthenticatedWho authenticatedWho, String tenantId, String stateId, Int32 timeOut)
        {
            HttpClient httpClient = null;

            httpClient = new HttpClient();

            if (authenticatedWho != null)
            {
                // Serialize and add the user information to the header
                httpClient.DefaultRequestHeaders.Add(HEADER_AUTHORIZATION, Uri.EscapeDataString(AuthenticationUtils.Serialize(authenticatedWho)));
            }

            if (tenantId != null &&
                tenantId.Trim().Length > 0)
            {
                // Add the tenant to the header
                httpClient.DefaultRequestHeaders.Add(HEADER_MANYWHO_TENANT, tenantId);
            }

            if (stateId != null &&
                stateId.Trim().Length > 0)
            {
                // Add the state to the header
                httpClient.DefaultRequestHeaders.Add(HEADER_MANYWHO_STATE, stateId);
            }

            // Set the timeout for the request
            httpClient.Timeout = TimeSpan.FromSeconds(timeOut);

            return httpClient;
        }

        public static void CleanUpHttp(HttpClient httpClient, HttpContent httpContent, HttpResponseMessage httpResponseMessage)
        {
            if (httpClient != null)
            {
                httpClient.Dispose();
                httpClient = null;
            }

            if (httpContent != null)
            {
                httpContent.Dispose();
                httpContent = null;
            }

            if (httpResponseMessage != null)
            {
                httpResponseMessage.Dispose();
                httpResponseMessage = null;
            }
        }

        public static HttpResponseException GetWebException(HttpStatusCode statusCode, String reasonPhrase)
        {
            HttpResponseException httpResponseException = null;
            HttpResponseMessage httpResponseMessage = null;

            // Create the new http response message with the status code
            httpResponseMessage = new HttpResponseMessage(statusCode);

            // Reason phrases cannot have carriage returns
            httpResponseMessage.ReasonPhrase = reasonPhrase.Replace(Environment.NewLine, " ");

            // Add the response message to the exception
            httpResponseException = new HttpResponseException(httpResponseMessage);

            return httpResponseException;
        }

        public static String GetModeFromQuery(Uri uri)
        {
            String mode = null;

            // Check to see if the caller passed in the mode
            if (uri.Query != null)
            {
                if (uri.Query.IndexOf(ManyWhoConstants.MODE_DEBUG_STEPTHROUGH, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    mode = ManyWhoConstants.MODE_DEBUG_STEPTHROUGH;
                }
                else if (uri.Query.IndexOf(ManyWhoConstants.MODE_DEBUG, StringComparison.OrdinalIgnoreCase) > 0)
                {
                    mode = ManyWhoConstants.MODE_DEBUG;
                }
            }
            
            return mode;
        }

        public static String GetEmailFromQuery(Uri uri)
        {
            NameValueCollection nameValueCollection = null;
            String email = null;

            if (uri != null)
            {
                nameValueCollection = uri.ParseQueryString();

                if (nameValueCollection != null)
                {
                    // Get the email from the collection
                    email = nameValueCollection.Get("email");
                }
            }

            return email;
        }
    }
}
