using PerpetualEngine.Storage;
using PlayFab;
using PlayFab.ClientModels;
using Plugin.DeviceInfo;
using Plugin.DeviceInfo.Abstractions;
using System;
using System.Threading.Tasks;
using FacebookLoginLib;
using LoginResult = PlayFab.ClientModels.LoginResult;
using PlayFab.Internal;

namespace PlayFabBuddyLib.Auth
{
	/// <summary>
	/// Supported Authentication types
	/// Note: Add types to there to support more AuthTypes
	/// See - https://api.playfab.com/documentation/client#Authentication
	/// </summary>
	public enum AuthType
	{
		None,
		Silent,
		UsernameAndPassword,
		EmailAndPassword,
		RegisterPlayFabAccount,
		Facebook,
		Google
	}

	public class PlayFabAuthService : IPlayFabAuthService
	{
		#region Events

		public delegate void DisplayAuthenticationEvent();
		public delegate void LoggingInEvent();
		public delegate void LoginSuccessEvent(LoginResult success);
		public delegate void PlayFabErrorEvent(PlayFabError error);

		public event DisplayAuthenticationEvent OnDisplayAuthentication;

		public event LoggingInEvent OnLoggingIn;

		public event LoginSuccessEvent OnLoginSuccess;

		public event PlayFabErrorEvent OnPlayFabError;

		#endregion //Events

		#region Properties

		/// <summary>
		/// The PlayFab client object that will do all the comunicating with PlayFab
		/// </summary>
		public IPlayFabClient PlayFabClient { get; set; }

		public IFacebookService Facebook { get; set; }

		public string Email { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public string AuthTicket { get; set; }

		public GetPlayerCombinedInfoRequestParams InfoRequestParams { get; set; }

		/// <summary>
		/// this is a force link flag for custom ids for demoing
		/// </summary>
		public bool ForceLink { get; set; } = true;

		/// <summary>
		/// Accessbility for PlayFab ID
		/// </summary>
		public string PlayFabId { get; protected set; }

		/// <summary>
		/// Accessbility for Session Tickets
		/// </summary>
		public string SessionTicket { get; protected set; }

		public bool IsLoggedIn => !string.IsNullOrEmpty(PlayFabId) && !string.IsNullOrEmpty(SessionTicket);

		#region Auth Storage

		private const string _LoginRememberKey = "PlayFabLoginRemember";
		private const string _PlayFabRememberMeIdKey = "PlayFabIdPassGuid";
		private const string _PlayFabAuthTypeKey = "PlayFabAuthType";

		/// <summary>
		/// Remember the user next time they log in
		/// This is used for Auto-Login purpose.
		/// </summary>
		public bool RememberMe
		{
			get
			{
				var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
				return storage.Get(_LoginRememberKey, false);
			}
			set
			{
				if (!value)
				{
					Logout();
				}
				else
				{
					var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
					storage.Put(_LoginRememberKey, value);
				}
			}
		}

		/// <summary>
		/// Remember the type of authenticate for the user
		/// </summary>
		public AuthType AuthType
		{
			get
			{
				var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
				return storage.Get(_PlayFabAuthTypeKey, AuthType.None);
			}
			set
			{
				var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
				storage.Put(_PlayFabAuthTypeKey, value);
			}
		}

		/// <summary>
		/// Generated Remember Me ID
		/// Pass Null for a value to have one auto-generated.
		/// </summary>
		protected string RememberMeId
		{
			get
			{
				var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
				return storage.Get(_PlayFabRememberMeIdKey);
			}
			set
			{
				var guid = string.IsNullOrEmpty(value) ? Guid.NewGuid().ToString() : value;
				var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
				storage.Put(_PlayFabRememberMeIdKey, guid);
			}
		}

		#endregion //Auth Storage

		#endregion //Properties

		#region Methods

		public PlayFabAuthService(IPlayFabClient playFabClient, IFacebookService facebook = null)
		{
			PlayFabClient = playFabClient;
			Facebook = facebook;
		}

		public void Logout()
		{
			ClearRememberMe();
			PlayFabId = string.Empty;
			SessionTicket = string.Empty;
		}

		public void ClearRememberMe()
		{
			var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
			storage.Delete(_LoginRememberKey);
			storage.Delete(_PlayFabAuthTypeKey);
			storage.Delete(_PlayFabRememberMeIdKey);
		}

		/// <summary>
		/// Kick off the authentication process by specific authtype.
		/// </summary>
		/// <param name="authType"></param>
		public Task Authenticate(AuthType authType)
		{
			AuthType = authType;
			return Authenticate();
		}

		/// <summary>
		/// Authenticate the user by the Auth Type that was defined.
		/// </summary>
		public async Task Authenticate()
		{
			switch (AuthType)
			{
				case AuthType.Silent:
					{
						OnLoggingIn?.Invoke();
						var platform = CrossDeviceInfo.Current.Platform;
						await SilentlyAuthenticate(platform).ConfigureAwait(false);
					}
					break;
				case AuthType.EmailAndPassword:
					{
						OnLoggingIn?.Invoke();
						await AuthenticateEmailPassword().ConfigureAwait(false);
					}
					break;
				case AuthType.RegisterPlayFabAccount:
					{
						OnLoggingIn?.Invoke();
						await AddAccountAndPassword().ConfigureAwait(false);
					}
					break;
				case AuthType.Facebook:
					{
						await AuthenticateFacebook().ConfigureAwait(false);
					}
					break;
				case AuthType.Google:
					{
						OnLoggingIn?.Invoke();
						await AuthenticateGooglePlayGames().ConfigureAwait(false);
					}
					break;
				default:
					{
						if (RememberMe)
						{
							OnLoggingIn?.Invoke();
							AuthType = AuthType.EmailAndPassword;
							await AuthenticateEmailPassword().ConfigureAwait(false);
						}
						else
						{
							OnDisplayAuthentication?.Invoke();
						}
					}
					break;
			}
		}

		/// <summary>
		/// Authenticate a user in PlayFab using an Email & Password combo
		/// </summary>
		protected virtual async Task AuthenticateEmailPassword()
		{
			//Check if the users has opted to be remembered.
			if (RememberMe && !string.IsNullOrEmpty(RememberMeId))
			{
				//If the user is being remembered, then log them in with a customid that was 
				//generated by the RememberMeId property
				var customIdResult = await PlayFabClient.LoginWithCustomIDAsync(new LoginWithCustomIDRequest()
				{
					TitleId = PlayFabSettings.staticSettings.TitleId,
					CustomId = RememberMeId,
					CreateAccount = true,
					InfoRequestParameters = this.InfoRequestParams
				}).ConfigureAwait(false);

				LoginResult(customIdResult);

				return;
			}
			else if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
			{
				//a good catch: If username & password is empty, then do not continue, and Call back to Authentication UI Display 
				OnDisplayAuthentication.Invoke();
				return;
			}
			else
			{
				//We have not opted for remember me in a previous session, so now we have to login the user with email & password.
				var emailResult = await PlayFabClient.LoginWithEmailAddressAsync(new LoginWithEmailAddressRequest()
				{
					TitleId = PlayFabSettings.staticSettings.TitleId,
					Email = this.Email,
					Password = this.Password,
					InfoRequestParameters = this.InfoRequestParams
				}).ConfigureAwait(false);

				//Note: At this point, they already have an account with PlayFab using a Username (email) & Password
				//If RememberMe is checked, then generate a new Guid for Login with CustomId.
				if (RememberMe)
				{
					RememberMeId = Guid.NewGuid().ToString();
					AuthType = AuthType.EmailAndPassword;

					//Fire and forget, but link a custom ID to this PlayFab Account.
					await PlayFabClient.LinkCustomIDAsync(new LinkCustomIDRequest()
					{
						CustomId = RememberMeId,
						ForceLink = ForceLink
					}).ConfigureAwait(false);
				}

				LoginResult(emailResult);
			}
		}

		private void LoginResult(PlayFabResult<LoginResult> result)
		{
			if (null != result.Error)
			{
				//report error back to subscriber
				Logout();
				OnPlayFabError?.Invoke(result.Error);
			}
			else
			{
				//Store identity and session
				PlayFabId = result.Result.PlayFabId;
				SessionTicket = result.Result.SessionTicket;

				//report login result back to subscriber
				OnLoginSuccess?.Invoke(result.Result);
			}
		}

		private void LoginResult<T>(PlayFabResult<T> result) where T : PlayFabResultCommon
		{
			if (null != result.Error)
			{
				//report error back to subscriber
				Logout();
				OnPlayFabError?.Invoke(result.Error);
			}
			else
			{
				//report login result back to subscriber
				OnLoginSuccess?.Invoke(new LoginResult
				{
				});
			}
		}

		/// <summary>
		/// Register a user with an Email & Password
		/// Note: We are not using the RegisterPlayFab API
		/// </summary>
		protected virtual async Task AddAccountAndPassword()
		{
			//Any time we attempt to register a player, first silently authenticate the player.
			//This will retain the players True Origination (Android, iOS, Desktop)
			await SilentlyAuthenticate(CrossDeviceInfo.Current.Platform, async (result) =>
			{
				if (result == null)
				{
					//something went wrong with Silent Authentication, Check the debug console.
					Logout();
					OnPlayFabError?.Invoke(new PlayFabError()
					{
						Error = PlayFabErrorCode.UnknownError,
						ErrorMessage = "Silent Authentication by Device failed"
					});
				}

				//Note: If silent auth is success, which is should always be and the following 
				//below code fails because of some error returned by the server ( like invalid email or bad password )
				//this is okay, because the next attempt will still use the same silent account that was already created.

				//Now add our username & password.
				var addUsernameResult = await PlayFabClient.AddUsernamePasswordAsync(new AddUsernamePasswordRequest()
				{
					Username = !string.IsNullOrEmpty(this.Username) ? Username : result.PlayFabId, //Because it is required & Unique and not supplied by User.
					Email = this.Email,
					Password = this.Password,
				}).ConfigureAwait(false);

				if (null != addUsernameResult.Error)
				{
					//report error back to subscriber
					Logout();
					OnPlayFabError?.Invoke(addUsernameResult.Error);
				}
				else
				{
					//Store identity and session
					PlayFabId = result.PlayFabId;
					SessionTicket = result.SessionTicket;

					//If they opted to be remembered on next login.
					if (RememberMe)
					{
						//Generate a new Guid 
						RememberMeId = Guid.NewGuid().ToString();

						//Fire and forget, but link the custom ID to this PlayFab Account.
						await PlayFabClient.LinkCustomIDAsync(new LinkCustomIDRequest()
						{
							CustomId = RememberMeId,
							ForceLink = ForceLink
						}).ConfigureAwait(false);
					}

					//Override the auth type to ensure next login is using this auth type.
					AuthType = AuthType.EmailAndPassword;

					//Report login result back to subscriber.
					OnLoginSuccess?.Invoke(result);
				}
			}).ConfigureAwait(false);
		}

		protected virtual async Task AuthenticateFacebook()
		{
			//If there is no Facebook client, we can't do this
			if (null == Facebook)
			{
				Logout();
				OnPlayFabError?.Invoke(new PlayFabError()
				{
					ErrorMessage = "No FacebookClient was detected",
				});
			}

			//Check if the user needs to log into Facebook
			if (!Facebook.LoggedIn || string.IsNullOrEmpty(AuthTicket))
			{
				//sign up for the logged in event
				Facebook.OnLoginSuccess -= OnFacebookLoggedIn;
				Facebook.OnLoginSuccess += OnFacebookLoggedIn;
				Facebook.OnLoginError -= Facebook_OnLoginError;
				Facebook.OnLoginError += Facebook_OnLoginError;

				//try to log in
				Facebook.Login();
			}
			else
			{
				//just run that event
				await AuthenticateFacebookUser(Facebook.User).ConfigureAwait(false);
			}
		}

		public async Task LinkFacebook()
		{
			//If there is no Facebook client, we can't do this
			if (null == Facebook)
			{
				Logout();
				OnPlayFabError?.Invoke(new PlayFabError()
				{
					ErrorMessage = "No FacebookClient was detected",
				});
			}

			//Check if the user needs to log into Facebook
			if (!Facebook.LoggedIn || string.IsNullOrEmpty(AuthTicket))
			{
				//sign up for the logged in event
				Facebook.OnLoginSuccess -= OnFacebookLoggedIn;
				Facebook.OnLoginSuccess += OnFacebookLoggedIn;
				Facebook.OnLoginError -= Facebook_OnLoginError;
				Facebook.OnLoginError += Facebook_OnLoginError;

				//try to log in
				Facebook.Login();
			}
			else
			{
				OnLoggingIn?.Invoke();

				//grab the auth ticket from that user
				AuthTicket = Facebook.User.Token;

				var result = await PlayFabClient.LinkFacebookAccountAsync(new LinkFacebookAccountRequest()
				{
					AccessToken = AuthTicket,
				}).ConfigureAwait(false);

				LoginResult(result);
			}
		}

		private void Facebook_OnLoginError(Exception ex)
		{
			Logout();
		}

		private async void OnFacebookLoggedIn(FacebookUser loggedInUser)
		{
			await AuthenticateFacebookUser(loggedInUser).ConfigureAwait(false);
		}

		private async Task AuthenticateFacebookUser(FacebookUser loggedInUser)
		{
			OnLoggingIn?.Invoke();

			//grab the auth ticket from that user
			AuthTicket = loggedInUser.Token;

			var result = await PlayFabClient.LoginWithFacebookAsync(new LoginWithFacebookRequest()
			{
				TitleId = PlayFabSettings.staticSettings.TitleId,
				AccessToken = AuthTicket,
				CreateAccount = true,
				InfoRequestParameters = InfoRequestParams
			}).ConfigureAwait(false);

			LoginResult(result);
		}

		protected virtual Task AuthenticateGooglePlayGames()
		{
#if GOOGLEGAMES
        PlayFabClient.LoginWithGoogleAccount(new LoginWithGoogleAccountRequest()
        {
            TitleId = PlayFabSettings.TitleId,
            ServerAuthCode = AuthTicket,
            InfoRequestParameters = InfoRequestParams,
            CreateAccount = true
        }, (result) =>
        {
            //Store Identity and session
            _playFabId = result.PlayFabId;
            _sessionTicket = result.SessionTicket;

            //check if we want to get this callback directly or send to event subscribers.
            if (OnLoginSuccess != null)
            {
                //report login result back to the subscriber
                OnLoginSuccess.Invoke(result);
            }
        }, (error) =>
        {

            //report errro back to the subscriber
            if (OnPlayFabError != null)
            {
				Logout();
                OnPlayFabError.Invoke(error);
            }
        });
#else
			return Task.CompletedTask;
#endif
		}

		protected virtual async Task SilentlyAuthenticate(Platform platform, System.Action<LoginResult> callback = null)
		{
			PlayFabResult<LoginResult> result = null;

			switch (platform)
			{
				case Platform.Android:
					{
						//Login with the android device ID
						result = await PlayFabClient.LoginWithAndroidDeviceIDAsync(new LoginWithAndroidDeviceIDRequest()
						{
							TitleId = PlayFabSettings.staticSettings.TitleId,
							AndroidDevice = CrossDeviceInfo.Current.DeviceName,
							OS = CrossDeviceInfo.Current.Platform.ToString(),
							AndroidDeviceId = CrossDeviceInfo.Current.Id,
							CreateAccount = true,
							InfoRequestParameters = this.InfoRequestParams

						}).ConfigureAwait(false);
					}
					break;
				case Platform.iOS:
					{
						result = await PlayFabClient.LoginWithIOSDeviceIDAsync(new LoginWithIOSDeviceIDRequest()
						{
							TitleId = PlayFabSettings.staticSettings.TitleId,
							DeviceModel = CrossDeviceInfo.Current.Model,
							OS = CrossDeviceInfo.Current.Platform.ToString(),
							DeviceId = CrossDeviceInfo.Current.Id,
							CreateAccount = true,
							InfoRequestParameters = InfoRequestParams
						}).ConfigureAwait(false);
					}
					break;
				default:
					{
						result = await PlayFabClient.LoginWithCustomIDAsync(new LoginWithCustomIDRequest()
						{
							TitleId = PlayFabSettings.staticSettings.TitleId,
							CustomId = CrossDeviceInfo.Current.Id,
							CreateAccount = true,
							InfoRequestParameters = InfoRequestParams
						}).ConfigureAwait(false);
					}
					break;
			}

			if (null != result.Error)
			{
				//report errro back to the subscriber
				if (callback == null)
				{
					Logout();
					OnPlayFabError?.Invoke(result.Error);
				}
				else
				{
					//make sure the loop completes, callback with null
					callback.Invoke(null);
				}
			}
			else
			{
				//Store identity and session
				PlayFabId = result.Result.PlayFabId;
				SessionTicket = result.Result.SessionTicket;

				//check if we want to get this callback directly or send to event subscribers.
				if (callback == null)
				{
					//report login result back to the subscriber
					OnLoginSuccess?.Invoke(result.Result);
				}
				else
				{
					//report login result back to the caller
					callback.Invoke(result.Result);
				}
			}
		}

		public async Task UnlinkSilentAuth()
		{
			var platform = CrossDeviceInfo.Current.Platform;
			await SilentlyAuthenticate(platform, async (result) =>
			{
				switch (platform)
				{
					case Platform.Android:
						{
							//Fire and forget, unlink this android device.
							await PlayFabClient.UnlinkAndroidDeviceIDAsync(new UnlinkAndroidDeviceIDRequest()
							{
								AndroidDeviceId = CrossDeviceInfo.Current.Id,
							}).ConfigureAwait(false);
						}
						break;
					case Platform.iOS:
						{
							await PlayFabClient.UnlinkIOSDeviceIDAsync(new UnlinkIOSDeviceIDRequest()
							{
								DeviceId = CrossDeviceInfo.Current.Id,
							}).ConfigureAwait(false);
						}
						break;
					default:
						{
							await PlayFabClient.UnlinkCustomIDAsync(new UnlinkCustomIDRequest()
							{
								CustomId = CrossDeviceInfo.Current.Id,
							}).ConfigureAwait(false);
						}
						break;
				}
			}).ConfigureAwait(false);
		}

		#endregion //Methods
	}
}
