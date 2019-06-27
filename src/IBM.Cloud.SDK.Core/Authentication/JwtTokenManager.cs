﻿/**
* Copyright 2019 IBM Corp. All Rights Reserved.
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

using IBM.Cloud.SDK.Core.Http;
using IBM.Cloud.SDK.Core.Util;
using JWT;
using JWT.Serializers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace IBM.Cloud.SDK.Core.Authentication
{
    public class JwtTokenManager
    {
        protected string url;
        protected string tokenName;
        protected string userAccessToken;
        protected bool disableSslVerification;  // for icp4d only
        private TokenData tokenData;
        private long? expireTime;

        /// <summary>
        /// Token Manager Service
        /// 
        /// Retreives and stores JSON web tokens.
        /// </summary>
        /// <param name="options"></param>
        public JwtTokenManager(JwtTokenOptions options)
        {
            tokenData = new TokenData();
            tokenName = "access_token";

            if (!string.IsNullOrEmpty(options.Url))
            {
                this.url = options.Url;
            }

            if (!string.IsNullOrEmpty(options.AccessToken))
            {
                this.userAccessToken = options.AccessToken;
            }
        }

        /// <summary>
        /// This function sends an access token back through a callback. The source of the token
        /// is determined by the following logic:
        /// 1. If user provides their own managed access token, assume it is valid and send it
        /// 2. a) If this class is managing tokens and does not yet have one, make a request for one
        ///    b) If this class is managing tokens and the token has expired, request a new one
        /// 3. If this class is managing tokens and has a valid token stored, send it
        /// </summary>
        /// <returns>The access token</returns>
        public string GetToken()
        {
            if (!string.IsNullOrEmpty(userAccessToken))
            {
                return userAccessToken;
            }
            else if (string.IsNullOrEmpty(userAccessToken) || IsTokenExpired())
            {
                var requestTokenResponse = RequestToken();
                return requestTokenResponse.Result.AccessToken;
            }
            else
            {
                return tokenData.AccessToken;
            }
        }

        /// <summary>
        /// Set a self-managed access token.
        /// The access token should be valid and not yet expired.
        /// 
        /// By using this method, you accept responsibility for managing the
        /// access token yourself.You must set a new access token before this
        /// one expires. Failing to do so will result in authentication errors
        /// after this token expires.
        /// </summary>
        /// <param name="accessToken">A valid, non-expired access token</param>
        public void SetAccessToken(string accessToken)
        {
            userAccessToken = accessToken;
        }

        /// <summary>
        /// Request a JWT using an API key.
        /// </summary>
        /// <returns>Detailed Response containing the TokenData</returns>
        protected virtual DetailedResponse<TokenData> RequestToken()
        {
            throw new Exception("`requestToken` MUST be overridden by a subclass of JwtTokenManagerV1.");
        }

        /// <summary>
        /// Check if currently stored token is "expired"
        /// i.e.past the window to request a new token
        /// </summary>
        /// <returns></returns>
        private bool IsTokenExpired()
        {
            if (expireTime == null)
            {
                return true;
            }

            long currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            return expireTime < currentTime;
        }

        /// <summary>
        /// Save the JWT service response and the calculated expiration time to the object's state.
        /// </summary>
        /// <param name="tokenResponse">Response object from JWT service request</param>
        private void SaveTokenInfo(TokenData tokenResponse)
        {
            var accessToken = tokenResponse.AccessToken;

            if (string.IsNullOrEmpty(userAccessToken))
            {
                throw new Exception("Access token not present in response");
            }

            expireTime = CalculateTimeForNewToken(accessToken);
            tokenData = tokenResponse;
        }

        /// <summary>
        /// Decode the access token and calculate the time to request a new token.
        /// A time buffer prevents the edge case of the token expiring before the request could be made.
        /// The buffer will be a fraction of the total time to live - we are using 80%
        /// </summary>
        /// <param name="accessToken">JSON Web Token received from the service</param>
        /// <returns></returns>
        private long CalculateTimeForNewToken(string accessToken)
        {
            // the time of expiration is found by decoding the JWT access token
            // exp is the time of expire and iat is the time of token retrieval
            long timeForNewToken = 0;

            try
            {
                IJsonSerializer serializer = new JsonNetSerializer();
                IDateTimeProvider provider = new UtcDateTimeProvider();
                IJwtValidator validator = new JwtValidator(serializer, provider);
                IBase64UrlEncoder urlEncoder = new JwtBase64UrlEncoder();
                IJwtDecoder decoder = new JwtDecoder(serializer, validator, urlEncoder);

                var decodedResponse = decoder.Decode(accessToken);

                if (!string.IsNullOrEmpty(decodedResponse))
                {
                    var o = JObject.Parse(decodedResponse);
                    long exp = (long)o["exp"];
                    long iat = (long)o["iat"];

                    double fractonOfTtl = 0.8d;
                    long timeToLive = exp - iat;
                    timeForNewToken = Convert.ToInt64(exp - (timeToLive * (1.0d - fractonOfTtl)));
                }
                else
                {
                    throw new Exception("Access token recieved is not a valid JWT");
                }
            }
            catch (TokenExpiredException)
            {
                Console.WriteLine("Token has expired");
            }
            catch (SignatureVerificationException)
            {
                Console.WriteLine("Token has invalid signature");
            }

            return timeForNewToken;
        }
    }

    public class JwtTokenOptions
    {
        private string url;
        public string Url
        {
            get { return url; }
            set
            {
                if (!Utility.HasBadFirstOrLastCharacter(value))
                {
                    url = value;
                }
                else
                {
                    throw new ArgumentException("The url shouldn't start or end with curly brackets or quotes. Be sure to remove any {} and \" characters surrounding your url");
                }
            }
        }

        public string AccessToken { get; set; }
    }

    public class TokenData
    {
        public string AccessToken { get; set; }
        [JsonProperty("accessToken", NullValueHandling = NullValueHandling.Ignore)]
        private string icp4dAccessToken { set { AccessToken = value; } }
        [JsonProperty("access_token", NullValueHandling = NullValueHandling.Ignore)]
        private string iamAccessToken { set { AccessToken = value; } }
        [JsonProperty("username", NullValueHandling = NullValueHandling.Ignore)]
        public string Username { get; set; }
        [JsonProperty("role", NullValueHandling = NullValueHandling.Ignore)]
        public string role { get; set; }
        [JsonProperty("permissions", NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Permissions { get; set; }
        [JsonProperty("sub", NullValueHandling = NullValueHandling.Ignore)]
        public string Sub { get; set; }
        [JsonProperty("iss", NullValueHandling = NullValueHandling.Ignore)]
        public string Iss { get; set; }
        [JsonProperty("aud", NullValueHandling = NullValueHandling.Ignore)]
        public string Aud { get; set; }
        [JsonProperty("uid", NullValueHandling = NullValueHandling.Ignore)]
        public string Uid { get; set; }
        [JsonProperty("_messageCode_", NullValueHandling = NullValueHandling.Ignore)]
        public string MessageCode { get; set; }
        [JsonProperty("message", NullValueHandling = NullValueHandling.Ignore)]
        public string Message { get; set; }
        [JsonProperty("refresh_token", NullValueHandling = NullValueHandling.Ignore)]
        public string RefreshToken { get; set; }
        [JsonProperty("token_type", NullValueHandling = NullValueHandling.Ignore)]
        public string TokenType { get; set; }
        [JsonProperty("expires_in", NullValueHandling = NullValueHandling.Ignore)]
        public long ExpiresIn { get; set; }
        [JsonProperty("expiration", NullValueHandling = NullValueHandling.Ignore)]
        public long Expiration { get; set; }
    }
}
