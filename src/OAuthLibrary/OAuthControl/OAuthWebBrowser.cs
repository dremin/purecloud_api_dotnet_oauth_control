﻿using System;
using System.Linq;
using System.Net;
using System.Web;
using System.Windows.Forms;
using RestSharp;
using RestSharp.Authenticators;

namespace ININ.PureCloud.OAuthControl
{
    public class OAuthWebBrowser : WebBrowser
    {
        #region Private Members

        private string _accessToken;

        #endregion



        #region Public Members

        /// <summary>
        /// Use a static property from PureCloudEnvironment to set the environment
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// The redirect URI for the OAuth Client
        /// </summary>
        public string RedirectUri { get; set; }

        /// <summary>
        /// [True] if the redirect URI does not resolve. Setting this to true will hide the control when the redirect URI is encountered.1
        /// </summary>
        public bool RedirectUriIsFake { get; set; }

        /// <summary>
        /// The OAuth Client ID
        /// </summary>
        public string ClientId { get; set; }

        /// <summary>
        /// The OAuth Client Secret. Only used with an authorization code grant.
        /// </summary>
        public string ClientSecret { get; set; }

        /// <summary>
        /// The Access Token returned after authenticating.
        /// </summary>
        public string AccessToken
        {
            get { return _accessToken; }
            private set
            {
                if (value == _accessToken) return;

                _accessToken = value;
                RaiseAuthenticated(AccessToken);
            }
        }

        /// <summary>
        /// The number of seconds in which the token expires. This will be 0 if the value is unknown.
        /// </summary>
        public int TokenExpiresInSeconds { get; private set; }

        public delegate void ExceptionEncounteredDelegate(string source, Exception ex);

        public delegate void AuthenticatedDelegate(string accessToken);

        /// <summary>
        /// Raised when an exception occurs during the authentication process
        /// </summary>
        public event ExceptionEncounteredDelegate ExceptionEncountered;

        /// <summary>
        /// Raised when an Access Token is successfully retrieved
        /// </summary>
        public event AuthenticatedDelegate Authenticated;

        #endregion



        public OAuthWebBrowser()
        {
            RedirectUriIsFake = false;
            Environment = PureCloudEnvironment.MyPureCloud;
            
            this.Navigated += OnNavigated;
        }

        #region Private Methods

        private void RaiseExceptionEncountered(string source, Exception ex)
        {
            ExceptionEncountered?.Invoke(source, ex);
        }

        private void RaiseAuthenticated(string accessToken)
        {
            Authenticated?.Invoke(accessToken);
        }

        private void OnNavigated(object sender, WebBrowserNavigatedEventArgs args)
        {
            try
            {
                // Ignore the navigated event after we have an access token
                if (!string.IsNullOrEmpty(AccessToken)) return;

                // Check for errors
                if (args.Url.Fragment.StartsWith("#/error?"))
                {
                    // Strip leading part of path and parse
                    var errorFragment = HttpUtility.ParseQueryString(args.Url.Fragment.Substring(8));

                    // Check for errorKey parameter
                    if (errorFragment.AllKeys.Contains("errorKey"))
                    {
                        RaiseExceptionEncountered("OAuthWebBrowser.Navigated", new Exception(errorFragment["errorKey"]));
                        return;
                    }
                }

                // Process our redirect URL
                if (args.Url.ToString().ToLowerInvariant().StartsWith(RedirectUri.ToLowerInvariant()))
                {
                    var queryString = HttpUtility.ParseQueryString(args.Url.Query);

                    var fragment = HttpUtility.ParseQueryString(args.Url.Fragment.TrimStart('#'));
                    // Get the token from the redirect URI (implicit grant)
                    if (fragment.AllKeys.Contains("expires_in"))
                    {
                        var i = 0;
                        if (int.TryParse(fragment["expires_in"], out i))
                            TokenExpiresInSeconds = i;
                    }
                    if (fragment.AllKeys.Contains("access_token"))
                    {
                        AccessToken = fragment["access_token"];
                        if (RedirectUriIsFake) Visible = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                RaiseExceptionEncountered("OAuthWebBrowser.OnNavigated", ex);
            }
        }

        #endregion



        #region Public Methods

        /// <summary>
        /// Initiates the Implicit Grant OAuth flow
        /// </summary>
        public void BeginImplicitGrant()
        {
            // Clear existing token
            AccessToken = "";

            // Navigate to the login URL
            this.Navigate($"https:\\\\login.{Environment}.com/authorize?client_id={ClientId}&response_type=token&redirect_uri={RedirectUri}");
        }

        #endregion
    }
}