﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace wallabag.Api
{
    public partial class WallabagClient : IWallabagClient
    {
        private HttpClient _httpClient;

        /// <summary>
        /// Initializes a new instance of WallabagClient.
        /// </summary>
        /// <param name="Uri">The Uri of the wallabag instance of the user.</param>
        /// <param name="ClientId">The OAuth client id of the app.</param>
        /// <param name="ClientSecret">The OAuth client secret of the app.</param>
        /// <param name="Timeout">Number in milliseconds after the request will be cancelled.</param>
        public WallabagClient(
            Uri Uri,
            string ClientId,
            string ClientSecret,
            int Timeout = 0)
        {
            this.InstanceUri = Uri;
            this.ClientId = ClientId;
            this.ClientSecret = ClientSecret;

            if (!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(RefreshToken))
            {
                this.AccessToken = AccessToken;
                this.RefreshToken = RefreshToken;
            }

            this._httpClient = new HttpClient();
            if (Timeout > 0)
                _httpClient.Timeout = TimeSpan.FromMilliseconds(Timeout);
        }

        public void Dispose() => _httpClient.Dispose();

        /// <summary>
        /// Returns the version number of the current wallabag instance.
        /// </summary>
        public async Task<string> GetVersionNumberAsync()
        {
            var jsonString = await ExecuteHttpRequestAsync(HttpRequestMethod.Get, "/version");
            return await Task.Factory.StartNew(() => JsonConvert.DeserializeObject<string>(jsonString));
        }

        protected async Task<string> ExecuteHttpRequestAsync(HttpRequestMethod httpRequestMethod, string RelativeUriString, Dictionary<string, object> parameters = default(Dictionary<string, object>))
        {
            var args = new PreRequestExecutionEventArgs();
            args.RequestMethod = httpRequestMethod;
            args.RequestUriSubString = RelativeUriString;
            args.Parameters = parameters;
            PreRequestExecution?.Invoke(this, args);

            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());

            if (string.IsNullOrEmpty(AccessToken))
                throw new Exception("Access token not available. Please create one using the RequestTokenAsync() method first.");

            var uriString = $"{InstanceUri}api{RelativeUriString}.json";

            if (httpRequestMethod == HttpRequestMethod.Get && parameters?.Count > 0)
            {
                uriString += "?";

                foreach (var item in parameters)
                    uriString += $"{item.Key}={item.Value.ToString()}&";

                // Remove the last ampersand (&).
                uriString = uriString.Remove(uriString.Length - 1);
            }

            Uri requestUri = new Uri(uriString);


            string httpMethodString = "GET";
            switch (httpRequestMethod)
            {
                case HttpRequestMethod.Delete: httpMethodString = "DELETE"; break;
                case HttpRequestMethod.Patch: httpMethodString = "PATCH"; break;
                case HttpRequestMethod.Post: httpMethodString = "POST"; break;
                case HttpRequestMethod.Put: httpMethodString = "PUT"; break;
            }

            var method = new HttpMethod(httpMethodString);
            var request = new HttpRequestMessage(method, requestUri);

            if (parameters != null && httpRequestMethod != HttpRequestMethod.Get)
                request.Content = new StringContent(JsonConvert.SerializeObject(parameters), System.Text.Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.SendAsync(request);
                AfterRequestExecution?.Invoke(this, response);

                if (response.IsSuccessStatusCode)
                    return await response.Content.ReadAsStringAsync();
                else
                    return null;
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine("[FAILURE] [wallabag-api] An error occured during the request: " + e.Message);
                return null;
            }
        }

        protected Task<T> ParseJsonFromStringAsync<T>(string s)
        {
            if (!string.IsNullOrEmpty(s))
                return Task.Factory.StartNew(() => JsonConvert.DeserializeObject<T>(s));
            else
                return Task.FromResult(default(T));
        }

        /// <summary>
        /// The type of the HTTP request.
        /// </summary>
        public enum HttpRequestMethod { Delete, Get, Patch, Post, Put }

        /// <summary>
        /// Event that is fired before a HTTP request to the server is started.
        /// </summary>
        public event EventHandler<PreRequestExecutionEventArgs> PreRequestExecution;

        /// <summary>
        /// Event that is fired after the HTTP request is complete.
        /// </summary>
        public event EventHandler<HttpResponseMessage> AfterRequestExecution;
    }

    /// <summary>
    /// The arguments of the <see cref="WallabagClient.PreRequestExecution" /> event.
    /// </summary>
    public class PreRequestExecutionEventArgs
    {
        /// <summary>
        /// The substring that will attached to the <see cref="WallabagClient.InstanceUri"/> to perform a certain HTTP request.
        /// </summary>
        public string RequestUriSubString { get; set; }

        /// <summary>
        /// The type of the HTTP request.
        /// </summary>
        public WallabagClient.HttpRequestMethod RequestMethod { get; set; }

        /// <summary>
        /// Any parameters that are going to be submitted along with the request, e.g. the URL of a new item.
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; }
    }

}
