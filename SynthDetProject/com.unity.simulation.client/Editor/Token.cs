#if !UNITY_EDITOR

using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;

using UnityEngine;

namespace Unity.Simulation.Client
{
    /// <summary>
    /// Encapsulates the access token used to authenticate with the USim service.
    /// </summary>
    public class Token
    {
        // Public Members

        /// <summary>
        /// Returns the access token.
        /// </summary>
        public string accessToken
        {
            get { return _AccessToken; }
        }

        /// <summary>
        /// Returns true if the access token has expired, false otherwise.
        /// </summary>
        public bool isExpired
        {
            get
            {
                return _TimeStamp.AddSeconds(_ExpiresIn).CompareTo(DateTime.Now) <= 0;
            }
        }

        /// <summary>
        /// Loads a token from peristent storage.
        /// </summary>
        /// <param name="tokenFile"> Path to the token file to laod. Can be null. Defaults to ~/.usim/token.json </param>
        /// <param name="refreshIfExpired"> When true, will automatically refresh the token if it has expired. Defaults to true. </param>
        public static Token Load(string tokenFile = null, bool refreshIfExpired = true)
        {
            if (string.IsNullOrEmpty(tokenFile))
                tokenFile = Config.tokenFile;

            if (!File.Exists(tokenFile))
                throw new System.Exception("Missing token file " + tokenFile);

            var json = File.ReadAllText(tokenFile);
            var data = JsonUtility.FromJson<TokenData>(json);

            var token = new Token(Config.apiEndpoint, data);

            if (refreshIfExpired && token.isExpired)
            {
                token.Refresh();
                token.Save(tokenFile);
            }
            return token;
        }

        /// <summary>
        /// Saves this token to persisten storage.
        /// </summary>
        /// <param name="tokenFile"> Path to the token file to laod. Can be null. Defaults to ~/.usim/token.json </param>
        public void Save(string tokenFile = null)
        {
          if (string.IsNullOrEmpty(tokenFile))
              tokenFile = Config.tokenFile;

            TokenData data;
            data.access_token  = _AccessToken;
            data.expires_in    = _ExpiresIn;
            data.refresh_token = _RefreshToken;
            data.timestamp     = _TimeStamp.ToString();
            data.token_type    = _TokenType;
            File.WriteAllText(tokenFile, JsonUtility.ToJson(data));
        }

        /// <summary>
        /// Refresh the current token.
        /// </summary>
        /// <param name="timeoutSeconds"> Timeout for attempting to refresh the token. </param>
        public void Refresh(int timeoutSeconds = 30)
        {
            if (string.IsNullOrEmpty(_RefreshToken))
                throw new Exception("Refresh token not found");

            var url = _APIEndpoint + "/v1/auth/refresh?refresh_token=" + _RefreshToken;

            using (var message = new HttpRequestMessage(HttpMethod.Get, url))
            {
                message.Headers.Accept   .Add(new MediaTypeWithQualityHeaderValue("application/json" ));
                message.Headers.UserAgent.Add(new ProductInfoHeaderValue         ("usim-cli", "0.0.0"));

                var request = Transaction.client.SendAsync(message);
                request.Wait(TimeSpan.FromSeconds(timeoutSeconds));

                if (!request.Result.IsSuccessStatusCode)
                {
                    throw new Exception("Refresh: failed " + request.Result.ReasonPhrase);
                }

                var payload = request.Result.Content.ReadAsStringAsync();
                payload.Wait();

                var data = JsonUtility.FromJson<TokenData>(payload.Result);

                _AccessToken  = data.access_token;
                _RefreshToken = data.refresh_token;
                _TokenType    = data.token_type;
                _ExpiresIn    = data.expires_in;
                _TimeStamp    = DateTime.Now;

                Debug.Log("Token Refreshed: " + accessToken);
            }
        }

        // Non Public Members

        string   _AccessToken;
        string   _TokenType;
        string   _RefreshToken;
        string   _APIEndpoint;
        int      _ExpiresIn;
        DateTime _TimeStamp;

        internal Token(string apiEndpoint, TokenData data)
        {
            _APIEndpoint  = apiEndpoint;
            _AccessToken  = data.access_token;
            _RefreshToken = data.refresh_token;
            _TokenType    = data.token_type;
            _ExpiresIn    = data.expires_in;
            _TimeStamp    = DateTime.Parse(data.timestamp);
        }

        internal Token(string apiEndpoint, System.Collections.Specialized.NameValueCollection kvp)
        {
            _APIEndpoint  = apiEndpoint;
            _AccessToken  = kvp["access_token"];
            _RefreshToken = kvp["refresh_token"];
            _TokenType    = kvp["token_type"];
            _ExpiresIn    = int.Parse(kvp["expires_in"]);
            _TimeStamp    = DateTime.Now;
        }
    }
}
#endif//!UNITY_EDITOR