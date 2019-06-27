﻿/**
* Copyright 2017 IBM Corp. All Rights Reserved.
*
* Licensed under the Apache License, Version 2.0 (the "License");
* you may not use this file except in compliance with the License.
* You may obtain a copy of the License at
*
*      http://www.apache.org/licenses/LICENSE-2.0
*
* Unless required by applicable law or agreed to in writing, software
* distributed under the License is distributed on an "AS IS" BASIS,
* WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
* See the License for the specific language governing permissions and
* limitations under the License.
*
*/

using System;
using System.Collections.Generic;
using System.Net.Http;
using IBM.Cloud.SDK.Core.Authentication;
using IBM.Cloud.SDK.Core.Authentication.Noauth;
using IBM.Cloud.SDK.Core.Http;
using IBM.Cloud.SDK.Core.Util;

namespace IBM.Cloud.SDK.Core.Service
{
    public abstract class IBMService : IIBMService
    {
        //const string PATH_AUTHORIZATION_V1_TOKEN = "/authorization/api/v1/token";
        const string icpPrefix = "icp-";
        const string apikeyAsUsername = "apikey";
        public string SERVICE_NAME;
        private string username;
        private string password;
        public string ApiKey { get; set; }
        public IClient Client { get; set; }
        private bool skipAuthentication;
        private string defaultEndpoint;

        public string ServiceName { get; set; }
        public string Url { get { return Endpoint; } }
        protected Dictionary<string, string> customRequestHeaders = new Dictionary<string, string>();

        protected string Endpoint
        {
            get
            {
                if (Client.BaseClient == null ||
                    Client.BaseClient.BaseAddress == null)
                    return string.Empty;

                return Client.BaseClient.BaseAddress.AbsoluteUri;
            }
            set
            {
                if (Client.BaseClient == null)
                {
                    Client.BaseClient = new HttpClient();
                }
                Client.BaseClient.BaseAddress = new Uri(value);
            }
        }
        public string UserName
        {
            get { return username; }
            set
            {
                if (!Utility.HasBadFirstOrLastCharacter(value))
                {
                    username = value;
                }
                else
                {
                    throw new ArgumentException("The username shouldn't start or end with curly brackets or quotes. Be sure to remove any {} and \" characters surrounding your username.");
                }
            }
        }
        public string Password
        {
            get { return password; }
            set
            {
                if (!Utility.HasBadFirstOrLastCharacter(value))
                {
                    password = value;
                }
                else
                {
                    throw new ArgumentException("The password shouldn't start or end with curly brackets or quotes. Be sure to remove any {} and \" characters surrounding your password.");
                }
            }
        }
        
        private Authenticator authenticator;

        protected bool _userSetEndpoint = false;

        protected IBMService(string serviceName)
        {
            Client = new IBMHttpClient();
            ServiceName = serviceName;

            var credentialsPaths = Utility.GetCredentialsPaths();
            if (credentialsPaths.Count > 0)
            {
                foreach (string path in credentialsPaths)
                {
                    if (Utility.LoadEnvFile(path))
                    {
                        break;
                    }
                }

                string apiKey = Environment.GetEnvironmentVariable(ServiceName.ToUpper() + "_IAM_APIKEY");
                // check for old IAM API key name as well
                if (string.IsNullOrEmpty(apiKey))
                {
                    apiKey = Environment.GetEnvironmentVariable(ServiceName.ToUpper() + "_APIKEY");
                }
                if (!string.IsNullOrEmpty(apiKey))
                    ApiKey = apiKey;
                string un = Environment.GetEnvironmentVariable(ServiceName.ToUpper() + "_USERNAME");
                if (!string.IsNullOrEmpty(un))
                    UserName = un;
                string pw = Environment.GetEnvironmentVariable(ServiceName.ToUpper() + "_PASSWORD");
                if (!string.IsNullOrEmpty(pw))
                    Password = pw;
                string ServiceUrl = Environment.GetEnvironmentVariable(ServiceName.ToUpper() + "_URL");

                if (string.IsNullOrEmpty(ApiKey) && (string.IsNullOrEmpty(UserName) || string.IsNullOrEmpty(Password)))
                {
                    throw new NullReferenceException(string.Format("Either {0}_APIKEY or {0}_USERNAME and {0}_PASSWORD did not exist. Please add credentials with this key in ibm-credentials.env.", ServiceName.ToUpper()));
                }

                if (!string.IsNullOrEmpty(ApiKey))
                {
                    TokenOptions tokenOptions = new TokenOptions()
                    {
                        IamApiKey = ApiKey
                    };

                    if (!string.IsNullOrEmpty(ServiceUrl))
                        tokenOptions.ServiceUrl = ServiceUrl;

                    if (!string.IsNullOrEmpty(tokenOptions.ServiceUrl))
                    {
                        Endpoint = tokenOptions.ServiceUrl;
                    }
                    else
                    {
                        tokenOptions.ServiceUrl = Url;
                    }

                    SetCredential(tokenOptions);
                }

                if (!string.IsNullOrEmpty(UserName) && !string.IsNullOrEmpty(Password))
                {
                    if (!string.IsNullOrEmpty(ServiceUrl))
                        Endpoint = ServiceUrl;

                    SetCredential(UserName, Password);
                }
            }
        }

        protected IBMService(string serviceName, string url)
        {
            ServiceName = serviceName;
            Client = new IBMHttpClient(url, UserName, Password);

            if (!string.IsNullOrEmpty(Endpoint))
                Endpoint = url;
            //TODO: verificar como iremos obter de um arquivo json por injeção de dependencia
            //ApiKey = CredentialUtils.GetApiKey(serviceName);
            //Endpoint = CredentialUtils.GetApiUrl(serviceName);
        }

        protected IBMService(string serviceName, string url, IClient httpClient)
        {
            ServiceName = serviceName;
            Client = httpClient;

            if (!string.IsNullOrEmpty(Endpoint))
                Endpoint = url;

            //TODO: verificar como iremos obter de um arquivo json por injeção de dependencia
            //ApiKey = CredentialUtils.GetApiKey(serviceName);
            //Endpoint = CredentialUtils.GetApiUrl(serviceName);
        }

        protected IBMService(string serviceName, IAuthenticatorConfig authenticatorConfig)
        {

        }

        /// <summary>
        /// Sets the username and password credentials.
        /// </summary>
        /// <param name="userName">The username</param>
        /// <param name="password">The password</param>
        public void SetCredential(string userName, string password)
        {
            if (userName == apikeyAsUsername && !password.StartsWith(icpPrefix))
            {
                TokenOptions tokenOptions = new TokenOptions()
                {
                    IamApiKey = password
                };

                SetCredential(tokenOptions);
            }
            else
            {
                UserName = userName;
                Password = password;
            }
        }

        /// <summary>
        /// Sets the tokenOptions for the service. 
        /// Also sets the endpoint if the user has not set the endpoint.
        /// </summary>
        /// <param name="options"></param>
        public void SetCredential(TokenOptions options)
        {
            if (!string.IsNullOrEmpty(options.ServiceUrl))
            {
                if (!_userSetEndpoint)
                {
                    Endpoint = options.ServiceUrl;
                }
            }
            else
            {
                options.ServiceUrl = Endpoint;
            }

            if (!string.IsNullOrEmpty(options.IamApiKey))
            {
                if (options.IamApiKey.StartsWith(icpPrefix))
                {
                    SetCredential(apikeyAsUsername, options.IamApiKey);
                }
                else
                {
                    _tokenManager = new TokenManager(options);
                }
            }
            else if (!string.IsNullOrEmpty(options.IamAccessToken))
            {
                _tokenManager = new TokenManager(options);
            }
            else
            {
                throw new ArgumentNullException("An iamApikey or iamAccessToken is required.");
            }
        }

        /// <summary>
        /// Initializes a new Authenticator instance based on the input AuthenticatorConfig instance and sets it as
        /// the current authenticator on the BaseService instance.
        /// </summary>
        /// <param name="authConfig">the AuthenticatorConfig instance containing the authentication configuration</param>
        protected void SetAuthenticator(IAuthenticatorConfig authConfig)
        {
            try
            {
                authenticator = AuthenticatorFactory.GetAuthenticator(authConfig);
                if (authenticator is NoauthAuthenticator)
                {
                    skipAuthentication = true;
                }
            }
            catch (Exception e)
            {
                throw new Exception(string.Format("Error: {0}", e));
            }

        }

        public void SetCredential(IamTokenOptions options)
        {
            if (!string.IsNullOrEmpty(options.Url))
            {
                if (!_userSetEndpoint)
                {
                    Endpoint = options.Url;
                }
            }
            else
            {
                options.Url = Endpoint;
            }

            if (!string.IsNullOrEmpty(options.IamApiKey))
            {
                if (options.IamApiKey.StartsWith(icpPrefix))
                {
                    SetCredential(apikeyAsUsername, options.IamApiKey);
                }
                else
                {
                    jwtTokenManager = new IamTokenManager(options);
                }
            }
            else if (!string.IsNullOrEmpty(options.IamAccessToken))
            {
                jwtTokenManager = new IamTokenManager(options);
            }
            else
            {
                throw new ArgumentNullException("An iamApikey or iamAccessToken is required.");
            }
        }
        public void SetCredential(Icp4dTokenOptions options)
        {
            if (!string.IsNullOrEmpty(options.Url))
            {
                if (!_userSetEndpoint)
                {
                    Endpoint = options.Url;
                }
            }
            else
            {
                options.Url = Endpoint;
            }

            if (!string.IsNullOrEmpty(options.Username) && !string.IsNullOrEmpty(options.Password))
            {
                jwtTokenManager = new Icp4dTokenManager(options);
            }
            else if (!string.IsNullOrEmpty(options.AccessToken))
            {
                jwtTokenManager = new Icp4dTokenManager(options);
            }
            else
            {
                throw new ArgumentNullException("A Username and Password or AccessToken is required.");
            }
        }

        public void SetEndpoint(string url)
        {
            _userSetEndpoint = true;
            Endpoint = url;
        }

        public void DisableSslVerification(bool insecure)
        {
            Client.DisableSslVerification(insecure);
        }

        public void WithHeader(string name, string value)
        {
            if (!customRequestHeaders.ContainsKey(name))
            {
                customRequestHeaders.Add(name, value);
            }
            else
            {
                customRequestHeaders[name] = value;
            }
        }

        public void WithHeaders(Dictionary<string, string> headers)
        {
            foreach (KeyValuePair<string, string> kvp in headers)
            {
                if (!customRequestHeaders.ContainsKey(kvp.Key))
                {
                    customRequestHeaders.Add(kvp.Key, kvp.Value);
                }
                else
                {
                    customRequestHeaders[kvp.Key] = kvp.Value;
                }
            }
        }

        protected void ClearCustomRequestHeaders()
        {
            customRequestHeaders = new Dictionary<string, string>();
        }

        /// <summary>
        /// Returns a Dictionary of custom request headers.
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetCustomRequestHeaders()
        {
            return customRequestHeaders;
        }
    }
}
