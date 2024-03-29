﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml;
using System.Net;
using System.Web;
using System.Web.Http;
using System.Net.Http;
using System.Threading.Tasks;
using ManyWho.Flow.SDK;
using ManyWho.Flow.SDK.Utils;
using ManyWho.Flow.SDK.Security;
using ManyWho.Flow.SDK.Run;
using ManyWho.Flow.SDK.Run.Elements.Type;
using ManyWho.Flow.SDK.Run.Elements.Config;
using ManyWho.Service.Salesforce.Utils;
using ManyWho.Service.Salesforce.Models.Rest;
using ManyWho.Service.Salesforce.Salesforce;

/*!

Copyright 2013 Manywho, Inc.

Licensed under the Manywho License, Version 1.0 (the "License"); you may not use this
file except in compliance with the License.

You may obtain a copy of the License at: http://manywho.com/sharedsource

Unless required by applicable law or agreed to in writing, software distributed under
the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
KIND, either express or implied. See the License for the specific language governing
permissions and limitations under the License.

*/

namespace ManyWho.Service.Salesforce.Singletons
{
    public class SalesforceAuthenticationSingleton
    {
        private static SalesforceAuthenticationSingleton salesforceAuthenticationSingleton;

        // Useful constants for sobject properties
        public const String SALESFORCE_SOBJECT_USER_ID = "sf:Id";
        public const String SALESFORCE_SOBJECT_MANAGER_ID = "sf:ManagerId";
        public const String SALESFORCE_SOBJECT_USERNAME = "sf:Username";
        public const String SALESFORCE_SOBJECT_EMAIL = "sf:Email";
        public const String SALESFORCE_SOBJECT_FIRST_NAME = "sf:FirstName";
        public const String SALESFORCE_SOBJECT_LAST_NAME = "sf:LastName";
        public const String SALESFORCE_SOBJECT_ROLE_ID = "sf:UserRoleId";
        public const String SALESFORCE_SOBJECT_ROLE_NAME = "sf:UserRole";
        public const String SALESFORCE_SOBJECT_PROFILE_ID = "sf:ProfileId";
        public const String SALESFORCE_SOBJECT_PROFILE_NAME = "sf:Profile";

        private const String GROUP_TYPE_QUEUE = "QUEUE";

        private SalesforceAuthenticationSingleton()
        {

        }

        public static SalesforceAuthenticationSingleton GetInstance()
        {
            if (salesforceAuthenticationSingleton == null)
            {
                salesforceAuthenticationSingleton = new SalesforceAuthenticationSingleton();
            }

            return salesforceAuthenticationSingleton;
        }

        public Int32 GetAuthorizationContextCount(INotifier notifier, IAuthenticatedWho authenticatedWho, List<EngineValueAPI> configurationValues, AuthorizationAPI authorization)
        {
            SforceService sforceService = null;
            String groupSelection = null;
            Int32 authorizationContextCount = 0;

            if (authenticatedWho == null)
            {
                throw new ArgumentNullException("AuthenticatedWho", "The AuthenticatedWho object cannot be null.");
            }

            if (authorization == null)
            {
                throw new ArgumentNullException("Authorization", "The Authorization object cannot be null.");
            }

            if (authorization.users != null &&
                authorization.users.Count > 0)
            {
                throw new ArgumentNullException("Authorization.Users", "The Service does not currently support any authorization context other than Group for Voting.");
            }

            if (authorization.groups != null &&
                authorization.groups.Count > 1)
            {
                throw new ArgumentNullException("Authorization.Groups", "The Service does not currently support more than one Group in the authorization context for Voting.");
            }

            // Login to the service
            sforceService = SalesforceDataSingleton.GetInstance().Login(authenticatedWho, configurationValues, true, false);

            if (sforceService == null)
            {
                throw new ArgumentNullException("SalesforceService", "Unable to log into Salesforce.");
            }

            // Check to see how the groups should be sourced
            groupSelection = ValueUtils.GetContentValue(SalesforceServiceSingleton.SERVICE_VALUE_GROUP_SELECTION, configurationValues, false);

            if (authorization.groups != null &&
                authorization.groups.Count > 0)
            {
                // We use the utils response object as it makes it a little easier to manage conditions that fail
                AuthenticationUtilsResponse authenticationUtilsResponse = null;

                if (authorization.groups[0].attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_MEMBERS, StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_QUEUE, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string groupId = GroupId(sforceService, authorization.groups[0].authenticationId, GROUP_TYPE_QUEUE);

                        // Check to see if the user is a member of the specified queue
                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, authorization.groups[0].authenticationId, groupId, authenticatedWho.UserId, true);
                    }
                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_ALL_GROUPS, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string groupId = GroupId(sforceService, authorization.groups[0].authenticationId);
                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, authorization.groups[0].authenticationId, groupId, authenticatedWho.UserId, true);
                    }
                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_PROFILE, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Check to see if the user is a member of the specified profile
                        authenticationUtilsResponse = this.ProfileMember(sforceService, authorization.groups[0].authenticationId, authenticatedWho.UserId, true);
                    }
                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_ROLE, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Check to see if the user is a member of the specified role
                        authenticationUtilsResponse = this.RoleMember(sforceService, authorization.groups[0].authenticationId, authenticatedWho.UserId, true);
                    }
                    else
                    {
                        // Check to see if the user is a member of the specified group
                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, authorization.groups[0].authenticationId, authenticatedWho.UserId, true);
                    }
                }
                else if (authorization.groups[0].attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_OWNERS, StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_QUEUE, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string groupId = GroupId(sforceService, authorization.groups[0].authenticationId, GROUP_TYPE_QUEUE);
                        // Check to see if the user is a member of the specified profile - we don't support owner
                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, authorization.groups[0].authenticationId, groupId, authenticatedWho.UserId, true);
                    }
                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_ALL_GROUPS, StringComparison.InvariantCultureIgnoreCase))
                    {
                        string groupId = GroupId(sforceService, authorization.groups[0].authenticationId);
                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, authorization.groups[0].authenticationId, groupId, authenticatedWho.UserId, true);
                    }
                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_PROFILE, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Check to see if the user is a member of the specified profile - we don't support owner
                        authenticationUtilsResponse = this.ProfileMember(sforceService, authorization.groups[0].authenticationId, authenticatedWho.UserId, true);
                    }
                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_ROLE, StringComparison.InvariantCultureIgnoreCase))
                    {
                        // Check to see if the user is a member of the specified role - we don't support owner
                        authenticationUtilsResponse = this.RoleMember(sforceService, authorization.groups[0].authenticationId, authenticatedWho.UserId, true);
                    }
                    else
                    {
                        // Check to see if the user is an owner of the specified group
                        authenticationUtilsResponse = this.GroupOwner(sforceService, authorization.groups[0].authenticationId, authenticatedWho.UserId, true);
                    }
                }
                else
                {
                    // We don't support the attribute that's being provided
                    String errorMessage = "The Group attribute is not supported: " + authorization.groups[0].attribute;

                    ErrorUtils.SendAlert(notifier, authenticatedWho, ErrorUtils.ALERT_TYPE_FAULT, errorMessage);

                    throw new ArgumentNullException("BadRequest", errorMessage);
                }

                // Get the count out from the result
                authorizationContextCount = authenticationUtilsResponse.Count;
            }
            else
            {
                // Get the count of all users in the org
                authorizationContextCount = this.OrgUserCount(sforceService).Count;
            }

            return authorizationContextCount;
        }

        public List<ObjectAPI> GetUserInAuthorizationContext(INotifier notifier, IAuthenticatedWho authenticatedWho, List<EngineValueAPI> configurationValues, ObjectDataRequestAPI objectDataRequest)
        {
            SforceService sforceService = null;
            List<ObjectAPI> objectAPIs = null;
            ObjectAPI objectAPI = null;
            Boolean loginUsingOAuth2 = false;
            String groupSelection = null;
            String authenticationUrl = null;
            String chatterBaseUrl = null;
            String alertEmail = null;
            String consumerSecret = null;
            String consumerKey = null;

            // Get the configuration values out
            authenticationUrl = ValueUtils.GetContentValue(SalesforceServiceSingleton.SERVICE_VALUE_AUTHENTICATION_URL, configurationValues, false);
            chatterBaseUrl = ValueUtils.GetContentValue(SalesforceServiceSingleton.SERVICE_VALUE_CHATTER_BASE_URL, configurationValues, false);
            alertEmail = ValueUtils.GetContentValue(SalesforceServiceSingleton.SERVICE_VALUE_ADMIN_EMAIL, configurationValues, false);
            consumerSecret = ValueUtils.GetContentValue(SalesforceServiceSingleton.SERVICE_VALUE_CONSUMER_SECRET, configurationValues, false);
            consumerKey = ValueUtils.GetContentValue(SalesforceServiceSingleton.SERVICE_VALUE_CONSUMER_KEY, configurationValues, false);

            // Check to see if the admin wants users to login using oauth2
            if (String.IsNullOrWhiteSpace(consumerSecret) == false &&
                String.IsNullOrWhiteSpace(consumerKey) == false)
            {
                // We have the consumer information, we should login using oauth
                loginUsingOAuth2 = true;
            }

            // Login to the service
            sforceService = SalesforceDataSingleton.GetInstance().Login(authenticatedWho, configurationValues, true, false);

            // Check to see how the groups should be sourced
            groupSelection = ValueUtils.GetContentValue(SalesforceServiceSingleton.SERVICE_VALUE_GROUP_SELECTION, configurationValues, false);

            // We can get a null salesforce service if the user is using active user authentication and the user has not yet logged in successfully via their token
            if (sforceService != null)
            {
                // We start by checking if the request is based on public users. Despite this seeming a little odd, it does give the plugin the opportunity
                // to assign information to the public user that may be helpful for other operations - e.g. anoymous collaboration.
                if (objectDataRequest.authorization.globalAuthenticationType.Equals(ManyWhoConstants.GROUP_AUTHORIZATION_GLOBAL_AUTHENTICATION_TYPE_PUBIC, StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    // Create the standard user object
                    objectAPI = CreateUserObject(sforceService);

                    // Apply some default settings
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_USER_ID, ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID));
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_USERNAME, ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID));
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_EMAIL, null));
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_FIRST_NAME, ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID));
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_LAST_NAME, ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID));

                    // Tell ManyWho the user is authorized to proceed
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_STATUS, ManyWhoConstants.AUTHORIZATION_STATUS_AUTHORIZED));
                }
                else if (objectDataRequest.authorization.globalAuthenticationType.Equals(ManyWhoConstants.GROUP_AUTHORIZATION_GLOBAL_AUTHENTICATION_TYPE_ALL_USERS, StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    // Only bother doing the lookup if we have an actual user id that's valid for this type of operation (e.g. not public)
                    if (authenticatedWho.UserId != null &&
                        authenticatedWho.UserId.Equals(ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID, StringComparison.InvariantCultureIgnoreCase) == false)
                    {
                        // Check to see if the user is in fact a user in the org. We do this by checking the authenticated who object as this is the user actually
                        // requesting access - we pass in the group selection as we may populate that optimistically if there is only one option possible for the user
                        // in the provided group selection context
                        objectAPI = this.User(sforceService, authenticatedWho.UserId, groupSelection).UserObject;
                    }
                }
                else if (objectDataRequest.authorization.globalAuthenticationType.Equals(ManyWhoConstants.GROUP_AUTHORIZATION_GLOBAL_AUTHENTICATION_TYPE_SPECIFIED, StringComparison.InvariantCultureIgnoreCase) == true)
                {
                    Boolean doMoreWork = true;

                    // Specified permissions is a bit more complicated as we need to do a little more analysis depending on the configuration. We assume
                    // the user is authenticated if any of the specified criteria evaluate to true. First we check to see if the author of the flow has
                    // specified permissions based on specific user references (which is not recommended - but is supported).
                    if (objectDataRequest.authorization.users != null &&
                        objectDataRequest.authorization.users.Count > 0)
                    {
                        // Go through each of the specified users and attempt to match the currently authenticated user with the criteria
                        foreach (UserAPI user in objectDataRequest.authorization.users)
                        {
                            // First - check to see if the author explicitly decided this user should have access. This is the default setting if the
                            // attribute is null.
                            if (user.attribute == null ||
                                user.attribute.Trim().Length == 0 ||
                                user.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_USER, StringComparison.InvariantCultureIgnoreCase) == true)
                            {
                                // This is a hard-coded user permission - so we simply check if this user matches the current user
                                if (user.authenticationId.Equals(authenticatedWho.UserId, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    // Get the user object from salesforce (which may not exist)
                                    objectAPI = this.User(sforceService, authenticatedWho.UserId, null).UserObject;

                                    // This is our user - no need to do anything else as the lookup will now determine if they have access
                                    doMoreWork = false;
                                    break;
                                }
                            }
                            else
                            {
                                // We use the utils response object as it makes it a little easier to manage conditions that fail
                                AuthenticationUtilsResponse authenticationUtilsResponse = null;
                                String userAuthenticationId = null;

                                if (user.runningUser == true)
                                {
                                    userAuthenticationId = objectDataRequest.authorization.runningAuthenticationId;
                                }
                                else
                                {
                                    userAuthenticationId = user.authenticationId;
                                }

                                // We are looking at a particular attribute of the user and therefore need to query the system based on that attribute
                                if (user.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_COLLEAGUES, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    // Check to see if the current user is a colleague of the specified user
                                    authenticationUtilsResponse = this.Colleague(sforceService, userAuthenticationId, authenticatedWho.UserId);
                                }
                                else if (user.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_DELEGATES, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    // Check to see if the current user is a delegate of the specified user
                                    authenticationUtilsResponse = this.Delegate(sforceService, userAuthenticationId, authenticatedWho.UserId);
                                }
                                else if (user.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_DIRECTS, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    // Check to see if the current user is a direct of the specified user
                                    authenticationUtilsResponse = this.Direct(sforceService, userAuthenticationId, authenticatedWho.UserId);
                                }
                                else if (user.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_FOLLOWERS, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    // Check to see if the current user is a follower of the specified user
                                    authenticationUtilsResponse = this.Follower(sforceService, notifier, authenticatedWho, alertEmail, chatterBaseUrl, userAuthenticationId);
                                }
                                else if (user.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_FOLLOWING, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    // Check to see if the current user is being followed by the specified user
                                    authenticationUtilsResponse = this.Following(sforceService, notifier, authenticatedWho, alertEmail, chatterBaseUrl, userAuthenticationId);
                                }
                                else if (user.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_MANAGERS, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    // Check to see if the current user is a direct of the specified user
                                    authenticationUtilsResponse = this.Manager(sforceService, userAuthenticationId, authenticatedWho.UserId);
                                }
                                else
                                {
                                    // We don't support the attribute that's being provided
                                    String errorMessage = "The user attribute is not supported: " + user.attribute;

                                    ErrorUtils.SendAlert(notifier, authenticatedWho, ErrorUtils.ALERT_TYPE_FAULT, errorMessage);

                                    throw new ArgumentNullException("BadRequest", errorMessage);
                                }

                                // If the user is in this context, then we don't need to do anything else
                                if (authenticationUtilsResponse.IsInContext == true)
                                {
                                    // Grab the user object
                                    objectAPI = authenticationUtilsResponse.UserObject;

                                    // Break out of the user validation
                                    doMoreWork = false;
                                    break;
                                }
                            }
                        }
                    }

                    // No need to do this next bit if we already know we're authorized
                    if (doMoreWork == true)
                    {
                        // If the user has not been matched by the user configuration, we need to move into the groups to see if they're included
                        // in any of the specified groups - if any group configuration has been provided
                        if (objectDataRequest.authorization.groups != null &&
                            objectDataRequest.authorization.groups.Count > 0)
                        {
                            // Go through each group in turn
                            foreach (GroupAPI group in objectDataRequest.authorization.groups)
                            {
                                // We use the utils response object as it makes it a little easier to manage conditions that fail
                                AuthenticationUtilsResponse authenticationUtilsResponse = null;

                                if (group.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_MEMBERS, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_QUEUE, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        string groupId = GroupId(sforceService, group.authenticationId, GROUP_TYPE_QUEUE);
                                        // Check to see if the user is a member of the specified queue
                                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, group.authenticationId, groupId, authenticatedWho.UserId, false);
                                    }
                                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_ALL_GROUPS, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        string groupId = GroupId(sforceService, group.authenticationId);
                                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, group.authenticationId, groupId, authenticatedWho.UserId, false);
                                    }
                                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_PROFILE, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // Check to see if the user is a member of the specified profile
                                        authenticationUtilsResponse = this.ProfileMember(sforceService, group.authenticationId, authenticatedWho.UserId, false);
                                    }
                                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_ROLE, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // Check to see if the user is a member of the specified role
                                        authenticationUtilsResponse = this.RoleMember(sforceService, group.authenticationId, authenticatedWho.UserId, false);
                                    }
                                    else
                                    {
                                        // Check to see if the user is a member of the specified group
                                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, group.authenticationId, authenticatedWho.UserId, false);
                                    }
                                }
                                else if (group.attribute.Equals(SalesforceServiceSingleton.SERVICE_VALUE_OWNERS, StringComparison.InvariantCultureIgnoreCase) == true)
                                {
                                    if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_QUEUE, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        string groupId = GroupId(sforceService, group.authenticationId, GROUP_TYPE_QUEUE);
                                        // Check to see if the user is a member of the specified queue
                                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, group.authenticationId, groupId, authenticatedWho.UserId, false);
                                    }
                                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_ALL_GROUPS, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        string groupId = GroupId(sforceService, group.authenticationId);
                                        authenticationUtilsResponse = this.GroupTypeMember(sforceService, group.authenticationId, groupId, authenticatedWho.UserId, false);
                                    }
                                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_PROFILE, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // Check to see if the user is a member of the specified profile
                                        authenticationUtilsResponse = this.ProfileMember(sforceService, group.authenticationId, authenticatedWho.UserId, false);
                                    }
                                    else if (string.IsNullOrWhiteSpace(groupSelection) == false &&
                                        groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_ROLE, StringComparison.InvariantCultureIgnoreCase))
                                    {
                                        // Check to see if the user is a member of the specified profile
                                        authenticationUtilsResponse = this.RoleMember(sforceService, group.authenticationId, authenticatedWho.UserId, false);
                                    }
                                    else
                                    {
                                        // Check to see if the user is an owner of the specified group
                                        authenticationUtilsResponse = this.GroupOwner(sforceService, group.authenticationId, authenticatedWho.UserId, false);
                                    }
                                }
                                else
                                {
                                    // We don't support the attribute that's being provided
                                    String errorMessage = "The group attribute is not supported: " + group.attribute;

                                    ErrorUtils.SendAlert(notifier, authenticatedWho, ErrorUtils.ALERT_TYPE_FAULT, errorMessage);

                                    throw new ArgumentNullException("BadRequest", errorMessage);
                                }

                                // If the user is in this context, then we don't need to do anything else
                                if (authenticationUtilsResponse.IsInContext == true)
                                {
                                    // Grab the user object
                                    objectAPI = authenticationUtilsResponse.UserObject;

                                    // Break out of the user validation
                                    doMoreWork = false;
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            // If we're here and the user object is null, then they did not manage to authenticate
            if (objectAPI == null)
            {
                // Check to see if this user is a directory user at all - if so we want to return their details
                if (sforceService != null &&
                    authenticatedWho.UserId != null &&
                    authenticatedWho.UserId.Equals(ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID, StringComparison.InvariantCultureIgnoreCase) == false)
                {
                    // Check to see if the user is in fact a user in the org. We do this by checking the authenticated who object as this is the user actually
                    // requesting access
                    objectAPI = this.User(sforceService, authenticatedWho.UserId, null).UserObject;

                    if (objectAPI == null)
                    {
                        throw new ArgumentNullException("User", string.Format("A user could not be found for the provided identifier. You may be logged into the incorrect Salesforce Org for which the Flow has been configured. The user identifier provided is: '{0}'", authenticatedWho.UserId));
                    }

                    // Set the status of this user to not authorized, but we do want to return their details
                    foreach (PropertyAPI property in objectAPI.properties)
                    {
                        if (property.developerName.Equals(ManyWhoConstants.MANYWHO_USER_PROPERTY_STATUS, StringComparison.OrdinalIgnoreCase) == true)
                        {
                            property.contentValue = ManyWhoConstants.AUTHORIZATION_STATUS_NOT_AUTHORIZED;
                            break;
                        }
                    }
                }

                // If the object is still null, then this is not a user of the directory
                if (objectAPI == null)
                {
                    // Create the standard user object
                    objectAPI = CreateUserObject(sforceService);

                    // Apply some default settings
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_USER_ID, ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID));
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_USERNAME, ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID));
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_EMAIL, null));
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_FIRST_NAME, ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID));
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_LAST_NAME, ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID));

                    // Tell ManyWho the user is not authorized to proceed
                    objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_STATUS, ManyWhoConstants.AUTHORIZATION_STATUS_NOT_AUTHORIZED));
                }
            }

            // Finally, decide on the authentication mode
            if (loginUsingOAuth2 == true)
            {
                String loginUrl = "";

                if (String.IsNullOrWhiteSpace(authenticationUrl) == true)
                {
                    loginUrl = "https://login.salesforce.com";
                }
                else
                {
                    loginUrl = authenticationUrl;
                }

                loginUrl = String.Format(loginUrl + "/services/oauth2/authorize?response_type=code&client_id={0}", consumerKey);

                objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_AUTHENTICATION_TYPE, ManyWhoConstants.AUTHENTICATION_TYPE_OAUTH2));
                objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_LOGIN_URL, loginUrl));
            }
            else
            {
                objectAPI.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_AUTHENTICATION_TYPE, ManyWhoConstants.AUTHENTICATION_TYPE_USERNAME_PASSWORD));
            }

            // Create the list of objects to return and add our object
            objectAPIs = new List<ObjectAPI>();
            objectAPIs.Add(objectAPI);

            // Return the user in an object list
            return objectAPIs;
        }

        /// <summary>
        /// For user and group loads, the user has the option to provide the list of users contained in the system. We then have the option to populate all of the
        /// latest user information so ManyWho isn't storing data that's likely to change in the user management system.
        /// </summary>
        public ListFilterAPI CreateFilterFromProvidedObjectData(List<ObjectAPI> objectData, ListFilterAPI inboundListFilterAPI, String identifierColumnName)
        {
            ListFilterAPI listFilterAPI = null;

            // Check to see if the caller has passed in objects - if they have - we'll filter the response by that list - using the attribute id as the filter
            if (objectData != null &&
                objectData.Count > 0 &&
                inboundListFilterAPI != null &&
                inboundListFilterAPI.filterByProvidedObjects == true)
            {
                listFilterAPI = new ListFilterAPI();
                listFilterAPI.comparisonType = ManyWhoConstants.LIST_FILTER_CONFIG_COMPARISON_TYPE_OR;
                listFilterAPI.where = new List<ListFilterWhereAPI>();

                foreach (ObjectAPI objectDataEntry in objectData)
                {
                    ListFilterWhereAPI listFilterWhere = null;

                    listFilterWhere = new ListFilterWhereAPI();
                    listFilterWhere.columnName = identifierColumnName;
                    listFilterWhere.criteriaType = ManyWhoConstants.CONTENT_VALUE_IMPLEMENTATION_CRITERIA_TYPE_EQUAL;

                    // We now need to find the id property from the incoming object
                    if (objectDataEntry.properties != null &&
                        objectDataEntry.properties.Count > 0)
                    {
                        // Go through each of the properties in the object to find the identifier
                        foreach (PropertyAPI objectDataEntryProperty in objectDataEntry.properties)
                        {
                            if (objectDataEntryProperty.developerName.Equals(ManyWhoConstants.AUTHENTICATION_OBJECT_AUTHENTICATION_ID, StringComparison.InvariantCultureIgnoreCase) == true)
                            {
                                listFilterWhere.value = objectDataEntryProperty.contentValue;
                                break;
                            }
                        }
                    }
                    else
                    {
                        throw new ArgumentNullException("BadRequest", "The incoming user object does not contain any properties.");
                    }

                    if (listFilterWhere.value == null ||
                        listFilterWhere.value.Trim().Length == 0)
                    {
                        throw new ArgumentNullException("BadRequest", "An attribute id could not be found for the user, which means the plugin will not be able to find the correct user.");
                    }

                    // Add this filter to the list
                    listFilterAPI.where.Add(listFilterWhere);
                }
            }

            return listFilterAPI;
        }

        /// <summary>
        /// Based on the reference group identifier, grab the complete list of user emails - though restrict the list to 100 for now so we
        /// don't accidentally send out a huge amount of spam to users.
        /// </summary>
        public List<String> GetGroupMemberEmails(INotifier notifier, SforceService sforceService, ServiceRequestAPI serviceRequestAPI, String referenceGroupId)
        {
            List<String> groupMemberEmails = null;
            QueryResult queryResult = null;
            String[] userIds = null;
            String soql = null;
            String where = String.Empty;

            if (notifier == null)
            {
                throw new ArgumentNullException("Notifier", "The Notifier object cannot be null.");
            }

            if (sforceService == null)
            {
                throw new ArgumentNullException("SforceService", "The SforceService object cannot be null.");
            }

            if (serviceRequestAPI == null)
            {
                throw new ArgumentNullException("ServiceRequestAPI", "The ServiceRequestAPI object cannot be null.");
            }

            if (String.IsNullOrWhiteSpace(referenceGroupId) == true)
            {
                throw new ArgumentNullException("ReferenceGroupId", "The ReferenceGroupId cannot be null or blank.");
            }

            // Select from the group members to see if this user exists in the set
            soql = "SELECT MemberId FROM CollaborationGroupMember WHERE CollaborationGroupId = '" + referenceGroupId + "'";

            // Query salesforce to see if anything comes back
            queryResult = sforceService.query(soql);

            // Check to see if the query returned any results
            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                userIds = new String[queryResult.records.Length];

                for (int i = 0; i < queryResult.records.Length; i++)
                {
                    // Get the identifier out of the record, we'll need this to get our user list
                    userIds[i] = queryResult.records[i].Any[0].InnerText;
                }

                groupMemberEmails = this.GetEmailsForUserIds(notifier, sforceService, userIds);
            }

            return groupMemberEmails;
        }

        public List<String> GetEmailsForUserIds(INotifier notifier, SforceService sforceService, String[] userIds)
        {
            List<String> userEmails = null;
            QueryResult queryResult = null;
            String where = String.Empty;

            for (int i = 0; i < userIds.Length; i++)
            {
                // Get the identifier out of the record, we'll need this to get our user list
                where += "Id = '" + userIds[i] + "' OR ";

                if (i >= 25)
                {
                    notifier.AddLogEntry("Too many users need notification (max 25) - sending to the first 25.");
                    break;
                }
            }

            // Trim the where clause back
            where = where.Substring(0, (where.Length - " OR ".Length));

            // Query salesforce again with this new where clause
            queryResult = sforceService.query("SELECT Email FROM User WHERE " + where);

            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                userEmails = new List<String>();

                // Now that we have the user, we need to get the properties from the object so we can map them to a manywho user
                for (int j = 0; j < queryResult.records.Length; j++)
                {
                    userEmails.Add(queryResult.records[j].Any[0].InnerText);
                }
            }

            return userEmails;
        }

        /// <summary>
        /// Check to see if this user exists in the system for the provided user id.
        /// </summary>
        private AuthenticationUtilsResponse User(SforceService sforceService, string thisUserId, string groupSelection)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            string where = null;

            // Get the user based on this identifier
            where = "Id = '" + thisUserId + "'";

            // Execute the query and grab the user response as this is also our user
            authenticationUtilsResponse = new AuthenticationUtilsResponse();
            authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, groupSelection).UserObject;

            // If we have a user object then we can assume the user is in context
            if (authenticationUtilsResponse.UserObject != null)
            {
                authenticationUtilsResponse.IsInContext = true;
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if this user is a colleague of the reference user id.
        /// </summary>
        private AuthenticationUtilsResponse Colleague(SforceService sforceService, String referenceUserId, String thisUserId)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            String where = null;
            String managerId = null;

            // Get the manager for the reference user id
            where = "Id = '" + referenceUserId + "'";

            // Grab the manager id from our helper
            managerId = this.ExecuteUserQuery(sforceService, where, null).ManagerId;

            //  Check to see if we found a manager id for this user
            if (managerId == null ||
                managerId.Trim().Length == 0)
            {
                // We didn't so the user is not in the colleague context
                authenticationUtilsResponse = new AuthenticationUtilsResponse();
                authenticationUtilsResponse.IsInContext = false;
            }
            else
            {
                // We have a manager, so we now need to see if this user has the same manager - and hence is a colleague
                where = "Id = '" + thisUserId + "' AND ManagerId = '" + managerId + "'";

                // Execute the query and grab the user response as this is also our user
                authenticationUtilsResponse = new AuthenticationUtilsResponse();
                authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;

                // If we have a user object then we can assume the user is in context
                if (authenticationUtilsResponse.UserObject != null)
                {
                    authenticationUtilsResponse.IsInContext = true;
                }
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if this user is a direct of the reference user id.
        /// </summary>
        private AuthenticationUtilsResponse Direct(SforceService sforceService, String referenceUserId, String thisUserId)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            String where = null;

            // Select from the users table to see if we have a user for this id and with a manager of the reference id
            where = "Id = '" + thisUserId + "' AND ManagerId = '" + referenceUserId + "'";

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // We assign the user object as this query will return the correct user also - so we can keep that without requerying salesforce
            authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;

            // If we have a user object, then we can assume the user has authenticated
            if (authenticationUtilsResponse.UserObject != null)
            {
                authenticationUtilsResponse.IsInContext = true;
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if this user is being followed of the reference user id.
        /// </summary>
        private AuthenticationUtilsResponse Following(SforceService sforceService, INotifier notifier, IAuthenticatedWho authenticatedWho, String alertEmail, String chatterBaseUrl, String referenceUserId)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            HttpClient httpClient = null;
            HttpResponseMessage httpResponseMessage = null;
            ChatterFollowingResponse followingUsersresponse = null;
            String endpointUrl = null;

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // We enclose the request in a for loop to handle http errors
            for (int i = 0; i < SalesforceHttpUtils.MAXIMUM_RETRIES; i++)
            {
                try
                {
                    // Create a new client object
                    httpClient = SalesforceHttpUtils.CreateHttpClient(SalesforceHttpUtils.GetAuthenticationDetails(authenticatedWho.Token).Token);

                    // Create the endpoint url
                    endpointUrl = chatterBaseUrl + SalesforceServiceSingleton.CHATTER_URI_PART_API_VERSION + String.Format(SalesforceServiceSingleton.CHATTER_URI_PART_STREAM_FOLLOWERS, referenceUserId);

                    // TODO: Need to add paging support to this as it currently only sends back the first page of results
                    httpResponseMessage = httpClient.GetAsync(endpointUrl).Result;

                    // Check the status of the response and respond appropriately
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        followingUsersresponse = httpResponseMessage.Content.ReadAsAsync<ChatterFollowingResponse>().Result;

                        // Check to see if this user has any following
                        if (followingUsersresponse.Following != null &&
                            followingUsersresponse.Following.Count > 0)
                        {
                            // Go through the followers and see if any of them match the current user
                            foreach (ChatterFollowing chatterFollowing in followingUsersresponse.Following)
                            {
                                ChatterUserInfo chatterUserInfo = null;

                                // The following "thing" is in the subject
                                if (chatterFollowing.Subject != null)
                                {
                                    chatterUserInfo = chatterFollowing.Subject;

                                    // Check to see if this is our user
                                    if (chatterUserInfo.Id.Equals(authenticatedWho.UserId, StringComparison.InvariantCultureIgnoreCase) == true)
                                    {
                                        // This user is a follower, we need to get their full user object details
                                        authenticationUtilsResponse.UserObject = this.User(sforceService, authenticatedWho.UserId, null).UserObject;
                                        authenticationUtilsResponse.IsInContext = true;

                                        // We have our user, break out of the following list
                                        break;
                                    }
                                }
                            }
                        }

                        // We successfully executed the request, we can break out of the retry loop
                        break;
                    }
                    else
                    {
                        // Make sure we handle the lack of success properly
                        BaseHttpUtils.HandleUnsuccessfulHttpResponseMessage(notifier, authenticatedWho, i, httpResponseMessage, endpointUrl);
                    }
                }
                catch (Exception exception)
                {
                    // Make sure we handle the exception properly
                    BaseHttpUtils.HandleHttpException(notifier, authenticatedWho, i, exception, endpointUrl);
                }
                finally
                {
                    // Clean up the objects from the request
                    BaseHttpUtils.CleanUpHttp(httpClient, null, httpResponseMessage);
                }
            }

            // Finally, return the authentication response to the caller
            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if this user is a follower of the reference user id.
        /// </summary>
        private AuthenticationUtilsResponse Follower(SforceService sforceService, INotifier notifier, IAuthenticatedWho authenticatedWho, String alertEmail, String chatterBaseUrl, String referenceUserId)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            HttpClient httpClient = null;
            HttpResponseMessage httpResponseMessage = null;
            ChatterFollowingResponse followingUsersresponse = null;
            String endpointUrl = null;

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // We enclose the request in a for loop to handle http errors
            for (int i = 0; i < SalesforceHttpUtils.MAXIMUM_RETRIES; i++)
            {
                try
                {
                    // Create a new client object
                    httpClient = SalesforceHttpUtils.CreateHttpClient(SalesforceHttpUtils.GetAuthenticationDetails(authenticatedWho.Token).Token);

                    // Create the endpoint url
                    endpointUrl = chatterBaseUrl + SalesforceServiceSingleton.CHATTER_URI_PART_API_VERSION + String.Format(SalesforceServiceSingleton.CHATTER_URI_PART_STREAM_FOLLOWERS, referenceUserId);

                    // TODO: Need to add paging support to this as it currently only sends back the first page of results
                    httpResponseMessage = httpClient.GetAsync(endpointUrl).Result;

                    // Check the status of the response and respond appropriately
                    if (httpResponseMessage.IsSuccessStatusCode)
                    {
                        followingUsersresponse = httpResponseMessage.Content.ReadAsAsync<ChatterFollowingResponse>().Result;

                        // Check to see if this user has any following
                        if (followingUsersresponse.Following != null &&
                            followingUsersresponse.Following.Count > 0)
                        {
                            // Go through the followers and see if any of them match the current user
                            foreach (ChatterFollowing chatterFollowing in followingUsersresponse.Following)
                            {
                                ChatterUserInfo chatterUserInfo = null;

                                // The following "thing" is in the subject
                                if (chatterFollowing.Subject != null)
                                {
                                    chatterUserInfo = chatterFollowing.Subject;

                                    // Check to see if this is our user
                                    if (chatterUserInfo.Id.Equals(authenticatedWho.UserId, StringComparison.InvariantCultureIgnoreCase) == true)
                                    {
                                        // This user is a follower, we need to get their full user object details
                                        authenticationUtilsResponse.UserObject = this.User(sforceService, authenticatedWho.UserId, null).UserObject;
                                        authenticationUtilsResponse.IsInContext = true;

                                        // We have our user, break out of the following list
                                        break;
                                    }
                                }
                            }
                        }

                        // We successfully executed the request, we can break out of the retry loop
                        break;
                    }
                    else
                    {
                        // Make sure we handle the lack of success properly
                        BaseHttpUtils.HandleUnsuccessfulHttpResponseMessage(notifier, authenticatedWho, i, httpResponseMessage, endpointUrl);
                    }
                }
                catch (Exception exception)
                {
                    // Make sure we handle the exception properly
                    BaseHttpUtils.HandleHttpException(notifier, authenticatedWho, i, exception, endpointUrl);
                }
                finally
                {
                    // Clean up the objects from the request
                    BaseHttpUtils.CleanUpHttp(httpClient, null, httpResponseMessage);
                }
            }

            // Finally, return the authentication response to the caller
            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if this user is a delegate of the reference user id.
        /// </summary>
        private AuthenticationUtilsResponse Delegate(SforceService sforceService, String referenceUserId, String thisUserId)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            String where = null;

            // Select from the users table to see if we have a user for the reference id and delegate approval authority for this user id
            where = "Id = '" + referenceUserId + "' AND DelegateApproverId = '" + thisUserId + "'";

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // If the query returns results, we know the user is valid
            if (this.ExecuteUserQuery(sforceService, where, null) != null)
            {
                // Ths user is in the specified context
                authenticationUtilsResponse.IsInContext = true;

                // Now we query the system again, but this time with the query for the actual user
                where = "Id = '" + thisUserId + "'";

                // Grab the correct user object
                authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if this user is a manager of the reference user id.
        /// </summary>
        private AuthenticationUtilsResponse Manager(SforceService sforceService, String referenceUserId, String thisUserId)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            String where = null;

            // Select from the users table to see if we have a user for this id and with a manager of the reference id
            where = "Id = '" + referenceUserId  + "' AND ManagerId = '" + thisUserId + "'";

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // Check to see if the query returns and results - if it doesn then we know this user is a manager of the reference user
            if (this.ExecuteUserQuery(sforceService, where, null).UserObject != null)
            {
                // Set the flag to indicate that this user is in the context
                authenticationUtilsResponse.IsInContext = true;

                // Now we query the system again, but this time with the query for the actual user
                where = "Id = '" + thisUserId + "'";

                // Grab the correct user object
                authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if this user is a member of the reference group id.
        /// </summary>
        private AuthenticationUtilsResponse GroupTypeMember(SforceService sforceService, String referenceGroupId, String thisUserId, Boolean isUserCount)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            QueryResult queryResult = null;
            Boolean executeQuery = true;
            String soql = null;
            String where = null;

            // Check to see what type of group member lookup we're doing
            if (isUserCount == true)
            {
                // If we're counting the users, we don't want to filter by a specific user, we just want the count
                soql = "SELECT Count(CollaborationGroupId) FROM CollaborationGroupMember WHERE CollaborationGroupId = '" + referenceGroupId + "'";
            }
            else
            {
                // Check to make sure we're not dealing with a public user as there's no point executing the query
                if (String.IsNullOrWhiteSpace(thisUserId) == false &&
                    thisUserId.Equals(ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID, StringComparison.OrdinalIgnoreCase) == true)
                {
                    executeQuery = false;
                }

                // Select from the group members to see if this user exists in the set
                soql = "SELECT CollaborationGroupId FROM CollaborationGroupMember WHERE CollaborationGroupId = '" + referenceGroupId + "' AND MemberId = '" + thisUserId + "'";
            }

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // Check to make sure we should bother executing the query
            if (executeQuery == true)
            {
                // Query salesforce to see if anything comes back
                queryResult = sforceService.query(soql);
            }

            // Check to see if the query returned any results
            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                if (isUserCount == true)
                {
                    if (queryResult.records[0].Any != null &&
                        queryResult.records[0].Any.Length > 0)
                    {
                        // Just get the count out of the result
                        authenticationUtilsResponse.Count = Int32.Parse(queryResult.records[0].Any[0].InnerText);
                    }
                }
                else
                {
                    // If we have a result, then the user is in context
                    authenticationUtilsResponse.IsInContext = true;

                    // Now we query the system again, but this time with the query for the actual user
                    where = "Id = '" + thisUserId + "'";

                    // Grab the user object - sending null for the group selection as we'll do it in the logic in this method
                    authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;

                    // Add the additional group information if this group validates the user
                    if (authenticationUtilsResponse.UserObject != null)
                    {
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_ID, contentValue = referenceGroupId, contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                        // At the moment we return an empty name for the chatter group
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_NAME, contentValue = "", contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                    }
                }
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if this user is the owner of the reference group id.
        /// </summary>
        private AuthenticationUtilsResponse GroupOwner(SforceService sforceService, String referenceGroupId, String thisUserId, Boolean isUserCount)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            QueryResult queryResult = null;
            String soql = null;
            String where = null;

            // Check to see what type of group member lookup we're doing
            if (isUserCount == true)
            {
                // If we're counting the users, we don't want to filter by a specific user, we just want the count
                soql = "SELECT Count(OwnerId) FROM CollaborationGroup WHERE Id = '" + referenceGroupId + "'";
            }
            else
            {
                // Select from the collaboration groups for the reference group id with this user as owner
                soql = "SELECT OwnerId FROM CollaborationGroup WHERE Id = '" + referenceGroupId + "' AND OwnerId = '" + thisUserId + "'";
            }

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // Query salesforce to see if anything comes back
            queryResult = sforceService.query(soql);

            // Check to see if the query returned any results
            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                if (isUserCount == true)
                {
                    if (queryResult.records[0].Any != null &&
                        queryResult.records[0].Any.Length > 0)
                    {
                        // Just get the count out of the result
                        authenticationUtilsResponse.Count = Int32.Parse(queryResult.records[0].Any[0].InnerText);
                    }
                }
                else
                {
                    // If we have a result, then the user is in context
                    authenticationUtilsResponse.IsInContext = true;

                    // Now we query the system again, but this time with the query for the actual user
                    where = "Id = '" + thisUserId + "'";

                    // Grab the user object - sending null for the group selection as we'll do it in the logic in this method
                    authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;

                    // Add the additional group information if this group validates the user
                    if (authenticationUtilsResponse.UserObject != null)
                    {
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_ID, contentValue = referenceGroupId, contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                        // At the moment we return an empty name for the chatter group
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_NAME, contentValue = "", contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                    }
                }
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if the user is a member of the provided profile. We do this based on profile name.
        /// </summary>
        private AuthenticationUtilsResponse ProfileMember(SforceService sforceService, String referenceProfileName, String thisUserId, Boolean isUserCount)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            QueryResult queryResult = null;
            Boolean executeQuery = true;
            String referenceProfileId = null;
            String soql = null;
            String where = null;

            // First we need to get the unique identifier for the profile as we're provided the name
            soql = "SELECT Id FROM Profile WHERE Name = '" + referenceProfileName + "'";

            queryResult = sforceService.query(soql);

            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                if (queryResult.records[0].Any != null &&
                    queryResult.records[0].Any.Length > 0)
                {
                    // Get the unique identifier out
                    referenceProfileId = queryResult.records[0].Any[0].InnerText;
                }
            }

            // Check to make sure we could find the reference profile name in the system
            if (string.IsNullOrWhiteSpace(referenceProfileId) == false)
            {
                // Check to see what type of group member lookup we're doing
                if (isUserCount == true)
                {
                    // If we're counting the users, we don't want to filter by a specific user, we just want the count
                    soql = "SELECT Count(ProfileId) FROM User WHERE ProfileId = '" + referenceProfileId + "'";
                }
                else
                {
                    // Check to make sure we're not dealing with a public user as there's no point executing the query
                    if (String.IsNullOrWhiteSpace(thisUserId) == false &&
                        thisUserId.Equals(ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        executeQuery = false;
                    }

                    // Select from the users to see if this user has the matching profile
                    soql = "SELECT Id FROM User WHERE ProfileId = '" + referenceProfileId + "' AND Id = '" + thisUserId + "'";
                }
            }

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // Check to make sure we should bother executing the query
            if (executeQuery == true)
            {
                // Query salesforce to see if anything comes back
                queryResult = sforceService.query(soql);
            }

            // Check to see if the query returned any results
            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                if (isUserCount == true)
                {
                    if (queryResult.records[0].Any != null &&
                        queryResult.records[0].Any.Length > 0)
                    {
                        // Just get the count out of the result
                        authenticationUtilsResponse.Count = Int32.Parse(queryResult.records[0].Any[0].InnerText);
                    }
                }
                else
                {
                    // If we have a result, then the user is in context
                    authenticationUtilsResponse.IsInContext = true;

                    // Now we query the system again, but this time with the query for the actual user
                    where = "Id = '" + thisUserId + "'";

                    // Grab the user object - sending null for the group selection as we'll do it in the logic in this method
                    authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;

                    // Add the additional group information if this group validates the user
                    if (authenticationUtilsResponse.UserObject != null)
                    {
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_ID, contentValue = referenceProfileId, contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_NAME, contentValue = referenceProfileName, contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                    }
                }
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// It gets the group id, using the developerName of the group and the type.
        /// It returns null if it is not found
        /// </summary>
        private string GroupId(SforceService sforceService, String referenceGroupDeveloperName, string groupType)
        {
            QueryResult queryResult = null;
            String referenceGroupId = null;

            // First we need to get the unique identifier for the queue as we're provided the developer name
            string soql = "SELECT Id FROM Group WHERE DeveloperName = '" + referenceGroupDeveloperName + "' AND Type = '" + groupType + "'";

            queryResult = sforceService.query(soql);

            if (queryResult?.records?.Length > 0)
            {
                if (queryResult.records[0].Any != null &&
                    queryResult.records[0].Any.Length > 0)
                {
                    // Get the unique identifier out
                    referenceGroupId = queryResult.records[0].Any[0].InnerText;
                }
            }

            return referenceGroupId;
        }

        /// <summary>
        /// It returns the group Id if the group exists and null in other case
        /// </summary>
        private string GroupId(SforceService sforceService, String groupId)
        {
            QueryResult queryResult = null;
            String referenceGroupId = null;

            // First we need to get the unique identifier for the queue as we're provided the developer name
            String soql = "SELECT Id FROM Group WHERE Id = '" + groupId + "'";

            queryResult = sforceService.query(soql);

            if (queryResult?.records?.Length > 0)
            {
                if (queryResult.records[0].Any != null &&
                    queryResult.records[0].Any.Length > 0)
                {
                    // Get the unique identifier out
                    referenceGroupId = queryResult.records[0].Any[0].InnerText;
                }
            }

            return referenceGroupId;
        }

        /// <summary>
        /// Check to see if the user is a member of the provided queue. We do this based on queue developer name.
        /// </summary>
        private AuthenticationUtilsResponse GroupTypeMember(SforceService sforceService, String referenceGroupIdentifier, string groupId, String thisUserId, Boolean isUserCount)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            QueryResult queryResult = null;
            Boolean executeQuery = true;
            String soql = null;
            String where = null;

            // Check to make sure we could find the reference profile name in the system
            if (string.IsNullOrWhiteSpace(groupId) == false)
            {
                // Check to see what type of group member lookup we're doing
                if (isUserCount == true)
                {
                    // If we're counting the users, we don't want to filter by a specific user, we just want the count
                    soql = "SELECT Count(GroupId) FROM GroupMember WHERE GroupId = '" + groupId + "'";
                }
                else
                {
                    // Check to make sure we're not dealing with a public user as there's no point executing the query
                    if (String.IsNullOrWhiteSpace(thisUserId) == false &&
                        thisUserId.Equals(ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        executeQuery = false;
                    }

                    // Select from the users to see if this user has the matching queue
                    soql = "SELECT GroupId FROM GroupMember WHERE GroupId = '" + groupId + "' AND UserOrGroupId = '" + thisUserId + "'";
                }
            }

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // Check to make sure we should bother executing the query
            if (executeQuery == true)
            {
                // Query salesforce to see if anything comes back
                queryResult = sforceService.query(soql);
            }

            // Check to see if the query returned any results
            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                if (isUserCount == true)
                {
                    if (queryResult.records[0].Any != null &&
                        queryResult.records[0].Any.Length > 0)
                    {
                        // Just get the count out of the result
                        authenticationUtilsResponse.Count = Int32.Parse(queryResult.records[0].Any[0].InnerText);
                    }
                }
                else
                {
                    // If we have a result, then the user is in context
                    authenticationUtilsResponse.IsInContext = true;

                    // Now we query the system again, but this time with the query for the actual user
                    where = "Id = '" + thisUserId + "'";

                    // Grab the user object - sending null for the group selection as we'll do it in the logic in this method
                    authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;

                    // Add the additional group information if this group validates the user
                    if (authenticationUtilsResponse.UserObject != null)
                    {
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_ID, contentValue = groupId, contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_NAME, contentValue = referenceGroupIdentifier, contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                    }
                }
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Check to see if the user is a member of the provided role. We do this based on role name.
        /// </summary>
        private AuthenticationUtilsResponse RoleMember(SforceService sforceService, String referenceRoleName, String thisUserId, Boolean isUserCount)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            QueryResult queryResult = null;
            Boolean executeQuery = true;
            String referenceRoleId = null;
            String soql = null;
            String where = null;

            // First we need to get the unique identifier for the role as we're provided the name
            soql = "SELECT Id FROM UserRole WHERE DeveloperName = '" + referenceRoleName + "'";

            queryResult = sforceService.query(soql);

            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                if (queryResult.records[0].Any != null &&
                    queryResult.records[0].Any.Length > 0)
                {
                    // Get the unique identifier out
                    referenceRoleId = queryResult.records[0].Any[0].InnerText;
                }
            }

            // Check to make sure we could find the reference role name in the system
            if (string.IsNullOrWhiteSpace(referenceRoleId) == false)
            {
                // Check to see what type of group member lookup we're doing
                if (isUserCount == true)
                {
                    // If we're counting the users, we don't want to filter by a specific user, we just want the count
                    soql = "SELECT Count(UserRoleId) FROM User WHERE UserRoleId = '" + referenceRoleId + "'";
                }
                else
                {
                    // Check to make sure we're not dealing with a public user as there's no point executing the query
                    if (String.IsNullOrWhiteSpace(thisUserId) == false &&
                        thisUserId.Equals(ManyWhoConstants.AUTHENTICATED_USER_PUBLIC_USER_ID, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        executeQuery = false;
                    }

                    // Select from the users to see if this user has the matching role
                    soql = "SELECT Id FROM User WHERE UserRoleId = '" + referenceRoleId + "' AND Id = '" + thisUserId + "'";
                }
            }

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // Check to make sure we should bother executing the query
            if (executeQuery == true)
            {
                // Query salesforce to see if anything comes back
                queryResult = sforceService.query(soql);
            }

            // Check to see if the query returned any results
            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                if (isUserCount == true)
                {
                    if (queryResult.records[0].Any != null &&
                        queryResult.records[0].Any.Length > 0)
                    {
                        // Just get the count out of the result
                        authenticationUtilsResponse.Count = Int32.Parse(queryResult.records[0].Any[0].InnerText);
                    }
                }
                else
                {
                    // If we have a result, then the user is in context
                    authenticationUtilsResponse.IsInContext = true;

                    // Now we query the system again, but this time with the query for the actual user
                    where = "Id = '" + thisUserId + "'";

                    // Grab the user object - sending null for the group selection as we'll do it in the logic in this method
                    authenticationUtilsResponse.UserObject = this.ExecuteUserQuery(sforceService, where, null).UserObject;

                    // Add the additional group information if this group validates the user
                    if (authenticationUtilsResponse.UserObject != null)
                    {
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_ID, contentValue = referenceRoleId, contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                        authenticationUtilsResponse.UserObject.properties.Add(new PropertyAPI() { developerName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_NAME, contentValue = referenceRoleName, contentType = ManyWhoConstants.CONTENT_TYPE_STRING });
                    }
                }
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Get the count of all users in the org.
        /// </summary>
        private AuthenticationUtilsResponse OrgUserCount(SforceService sforceService)
        {
            AuthenticationUtilsResponse authenticationUtilsResponse = null;
            QueryResult queryResult = null;
            String soql = null;

            // Get the count of all users in the org
            soql = "SELECT Count(Id) FROM User";

            // Create a new authentication utils response object to house the results
            authenticationUtilsResponse = new AuthenticationUtilsResponse();

            // Query salesforce to see if anything comes back
            queryResult = sforceService.query(soql);

            // Check to see if the query returned any results
            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0 &&
                queryResult.records[0].Any != null &&
                queryResult.records[0].Any.Length > 0)
            {
                // Just get the count out of the result
                authenticationUtilsResponse.Count = Int32.Parse(queryResult.records[0].Any[0].InnerText);
            }

            return authenticationUtilsResponse;
        }

        /// <summary>
        /// Utility method for executing user queries against salesforce.com
        /// </summary>
        private QueryResponseHelper ExecuteUserQuery(SforceService sforceService, string where, string groupSelection)
        {
            QueryResponseHelper queryResponseHelper = null;
            ObjectAPI userObject = null;
            QueryResult queryResult = null;
            sObject queryObject = null;
            string soql = null;

            // By default we execute the standard user query
            soql = "SELECT Id, Username, Email, FirstName, LastName, ManagerId, UserRoleId, UserRole.DeveloperName, ProfileId, Profile.Name FROM User WHERE ";

            // If the user is using profile group selection, we get that column also as we can populate it regardless of group context
            if (!string.IsNullOrWhiteSpace(groupSelection) &&
                groupSelection.Equals(SalesforceServiceSingleton.GROUP_SELECTION_PROFILE, StringComparison.InvariantCultureIgnoreCase))
            {
                soql = "SELECT Id, Username, Email, FirstName, LastName, ManagerId, UserRoleId, UserRole.DeveloperName, ProfileId, Profile.Name FROM User WHERE ";
            }

            // Create a new instance of the query response helper
            queryResponseHelper = new QueryResponseHelper();

            // Query salesforce to see if anything comes back
            queryResult = sforceService.query(soql + where);
            
            // Check to see if the query returned any results
            if (queryResult != null &&
                queryResult.records != null &&
                queryResult.records.Length > 0)
            {
                // Check to make sure that only one result was returned
                if (queryResult.records.Length > 1)
                {
                    throw new ArgumentException("The user query returned more than one result. The WHERE clause is: " + where);
                }

                // Create a new user object as we have one
                userObject = this.CreateUserObject(sforceService);

                // Grab the sobject from the array - this is our user
                queryObject = queryResult.records[0];

                // Now that we have the user, we need to get the properties from the object so we can map them to a manywho user
                for (int y = 0; y < queryObject.Any.Length; y++)
                {
                    PropertyAPI userProperty = null;
                    XmlElement element = queryObject.Any[y];

                    // We opportunistically grab the manager id also as it's useful for subsequent querying
                    if (element.Name.Equals(SALESFORCE_SOBJECT_MANAGER_ID, StringComparison.InvariantCultureIgnoreCase) == true)
                    {
                        queryResponseHelper.ManagerId = element.InnerText;
                    }
                    else
                    {
                        if ((y == 7 || y == 9) &&
                            element.LastChild != null)
                        {
                            // This is the relationship field for the user role name
                            userProperty = this.CreateProperty(this.RemapName(element.Name), element.LastChild.InnerText);
                        }
                        else
                        {
                            // Remap the salesforce property to a manywho property and create the object
                            userProperty = this.CreateProperty(this.RemapName(element.Name), element.InnerText);
                        }

                        // If this is the ID property, we assign that as the external id so manywho can track the value properly
                        if (userProperty.developerName.Equals(ManyWhoConstants.MANYWHO_USER_PROPERTY_USER_ID, StringComparison.InvariantCultureIgnoreCase) == true)
                        {
                            userObject.externalId = userProperty.contentValue;
                        }

                        // Add the property to the new user object
                        userObject.properties.Add(userProperty);
                    }
                }

                // Set the status to OK for the user - this user has authenticated OK
                userObject.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_STATUS, ManyWhoConstants.AUTHORIZATION_STATUS_AUTHORIZED));
            }

            // Assign the user object to the helper
            queryResponseHelper.UserObject = userObject;

            // Return the user object - which will be null if the user could not be found
            return queryResponseHelper;
        }

        /// <summary>
        /// Utility method for creating the standard properties for the user object.
        /// </summary>
        private ObjectAPI CreateUserObject(SforceService sforceService)
        {
            ObjectAPI userObject = null;

            userObject = new ObjectAPI();
            userObject.developerName = ManyWhoConstants.MANYWHO_USER_DEVELOPER_NAME;
            userObject.properties = new List<PropertyAPI>();
            userObject.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_COUNTRY, null));
            userObject.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_LANGUAGE, null));
            userObject.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_LOCATION, null));

            // This can be null for active user authentication, so we send back dummy information instead
            if (sforceService != null)
            {
                userObject.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_DIRECTORY_ID, sforceService.getUserInfo().organizationId));
                userObject.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_DIRECTORY_NAME, sforceService.getUserInfo().organizationName));
            }
            else
            {
                userObject.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_DIRECTORY_ID, "UNKNOWN"));
                userObject.properties.Add(CreateProperty(ManyWhoConstants.MANYWHO_USER_PROPERTY_DIRECTORY_NAME, "UNKNOWN"));
            }

            return userObject;
        }

        /// <summary>
        /// Utility method for creating new properties.
        /// </summary>
        private PropertyAPI CreateProperty(String developerName, String contentValue)
        {
            PropertyAPI propertyAPI = null;

            propertyAPI = new PropertyAPI();
            propertyAPI.developerName = developerName;
            propertyAPI.contentValue = contentValue;

            return propertyAPI;
        }

        /// <summary>
        /// Utility method for mapping salesforce field names to ManyWho property developer names.
        /// </summary>
        private String RemapName(String salesforceName)
        {
            String manywhoName = null;

            if (salesforceName.Equals(SALESFORCE_SOBJECT_USER_ID, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_USER_ID;
            }
            else if (salesforceName.Equals(SALESFORCE_SOBJECT_USERNAME, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_USERNAME;
            }
            else if (salesforceName.Equals(SALESFORCE_SOBJECT_EMAIL, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_EMAIL;
            }
            else if (salesforceName.Equals(SALESFORCE_SOBJECT_FIRST_NAME, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_FIRST_NAME;
            }
            else if (salesforceName.Equals(SALESFORCE_SOBJECT_LAST_NAME, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_LAST_NAME;
            }
            else if (salesforceName.Equals(SALESFORCE_SOBJECT_ROLE_ID, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_ROLE_ID;
            }
            else if (salesforceName.Equals(SALESFORCE_SOBJECT_ROLE_NAME, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_ROLE_NAME;
            }
            else if (salesforceName.Equals(SALESFORCE_SOBJECT_PROFILE_ID, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_ID;
            }
            else if (salesforceName.Equals(SALESFORCE_SOBJECT_PROFILE_NAME, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                manywhoName = ManyWhoConstants.MANYWHO_USER_PROPERTY_PRIMARY_GROUP_NAME;
            }
            else
            {
                throw new ArgumentException("The provided name could not be mapped: " + salesforceName);
            }

            return manywhoName;
        }
    }

    class AuthenticationUtilsResponse
    {
        public Boolean IsInContext
        {
            get;
            set;
        }

        public ObjectAPI UserObject
        {
            get;
            set;
        }

        public Int32 Count
        {
            get;
            set;
        }
    }

    class QueryResponseHelper
    {
        public String ManagerId
        {
            get;
            set;
        }

        public ObjectAPI UserObject
        {
            get;
            set;
        }
    }
}
