using System.Collections;
using System.Collections.Generic;
using PlayFab;
using PlayFab.ClientModels;
using LoginResult = PlayFab.ClientModels.LoginResult;
using System;
using PerpetualEngine.Storage;

#if FACEBOOK
using Facebook.Unity;
#endif

namespace PlayFabBuddyLib.Auth
{
	/// <summary>
	/// Supported Authentication types
	/// Note: Add types to there to support more AuthTypes
	/// See - https://api.playfab.com/documentation/client#Authentication
	/// </summary>
	public enum Authtypes
	{
		None,
		Silent,
		UsernameAndPassword,
		EmailAndPassword,
		RegisterPlayFabAccount,
		Steam,
		Facebook,
		Google
	}

	public class PlayFabAuthService
	{
		#region Events

		public delegate void DisplayAuthenticationEvent();
		public event DisplayAuthenticationEvent OnDisplayAuthentication;

		public delegate void LoginSuccessEvent(LoginResult success);
		public event LoginSuccessEvent OnLoginSuccess;

		public delegate void PlayFabErrorEvent(PlayFabError error);
		public event PlayFabErrorEvent OnPlayFabError;

		#endregion //Events

		#region Properties

		//These are fields that we set when we are using the service.

		public string Email { get; set; }
		public string Username { get; set; }
		public string Password { get; set; }
		public string AuthTicket { get; set; }
		public GetPlayerCombinedInfoRequestParams InfoRequestParams { get; set; }

		/// <summary>
		/// this is a force link flag for custom ids for demoing
		/// </summary>
		public bool ForceLink = false;

		/// <summary>
		/// Accessbility for PlayFab ID
		/// </summary>
		public string PlayFabId { get; set; }

		/// <summary>
		/// Accessbility for Session Tickets
		/// </summary>
		public string SessionTicket { get; set; }

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
				var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
				storage.Put(_LoginRememberKey, value);
			}
		}

		/// <summary>
		/// Remember the type of authenticate for the user
		/// </summary>
		public Authtypes AuthType
		{
			get
			{
				var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
				return storage.Get(_PlayFabAuthTypeKey, Authtypes.None);
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
		private string RememberMeId
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

		public PlayFabAuthService()
		{
		}

		public void ClearRememberMe()
		{
			var storage = SimpleStorage.EditGroup("PlayFabBuddy.Auth");
			storage.Delete(_LoginRememberKey);
			storage.Delete(_PlayFabRememberMeIdKey);
		}

		/// <summary>
		/// Kick off the authentication process by specific authtype.
		/// </summary>
		/// <param name="authType"></param>
		public void Authenticate(Authtypes authType)
		{
			AuthType = authType;
			Authenticate();
		}

		/// <summary>
		/// Authenticate the user by the Auth Type that was defined.
		/// </summary>
		public void Authenticate()
		{
			var authType = AuthType;
			switch (authType)
			{
				case Authtypes.None:
					{
						if (OnDisplayAuthentication != null)
						{
							OnDisplayAuthentication.Invoke();
						}
					}
					break;
				case Authtypes.Silent:
					{
						SilentlyAuthenticate();
					}
					break;
				case Authtypes.EmailAndPassword:
					{
						AuthenticateEmailPassword();
					}
					break;
				case Authtypes.RegisterPlayFabAccount:
					{
						AddAccountAndPassword();
					}
					break;
				case Authtypes.Facebook:
					{
						AuthenticateFacebook();
					}
					break;
				case Authtypes.Google:
					{
						AuthenticateGooglePlayGames();
					}
					break;
			}
		}

		/// <summary>
		/// Authenticate a user in PlayFab using an Email & Password combo
		/// </summary>
		private void AuthenticateEmailPassword()
		{
			//Check if the users has opted to be remembered.
			if (RememberMe && !string.IsNullOrEmpty(RememberMeId))
			{
				//If the user is being remembered, then log them in with a customid that was 
				//generated by the RememberMeId property
				PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest()
				{
					TitleId = PlayFabSettings.TitleId,
					CustomId = RememberMeId,
					CreateAccount = true,
					InfoRequestParameters = InfoRequestParams
				}, (result) =>
				{
				//Store identity and session
				_playFabId = result.PlayFabId;
					_sessionTicket = result.SessionTicket;

					if (OnLoginSuccess != null)
					{
					//report login result back to subscriber
					OnLoginSuccess.Invoke(result);
					}
				}, (error) =>
				{
					if (OnPlayFabError != null)
					{
					//report error back to subscriber
					OnPlayFabError.Invoke(error);
					}
				});
				return;
			}

			//a good catch: If username & password is empty, then do not continue, and Call back to Authentication UI Display 
			if (!RememberMe && string.IsNullOrEmpty(Email) && string.IsNullOrEmpty(Password))
			{
				OnDisplayAuthentication.Invoke();
				return;
			}

			//We have not opted for remember me in a previous session, so now we have to login the user with email & password.
			PlayFabClientAPI.LoginWithEmailAddress(new LoginWithEmailAddressRequest()
			{
				TitleId = PlayFabSettings.TitleId,
				Email = Email,
				Password = Password,
				InfoRequestParameters = InfoRequestParams
			}, (result) =>
			{
			//store identity and session
			_playFabId = result.PlayFabId;
				_sessionTicket = result.SessionTicket;

			//Note: At this point, they already have an account with PlayFab using a Username (email) & Password
			//If RememberMe is checked, then generate a new Guid for Login with CustomId.
			if (RememberMe)
				{
					RememberMeId = Guid.NewGuid().ToString();
					AuthType = Authtypes.EmailAndPassword;
				//Fire and forget, but link a custom ID to this PlayFab Account.
				PlayFabClientAPI.LinkCustomID(new LinkCustomIDRequest()
					{
						CustomId = RememberMeId,
						ForceLink = ForceLink
					}, null, null);
				}

				if (OnLoginSuccess != null)
				{
				//report login result back to subscriber
				OnLoginSuccess.Invoke(result);
				}
			}, (error) =>
			{
				if (OnPlayFabError != null)
				{
				//Report error back to subscriber
				OnPlayFabError.Invoke(error);
				}
			});
		}

		/// <summary>
		/// Register a user with an Email & Password
		/// Note: We are not using the RegisterPlayFab API
		/// </summary>
		private void AddAccountAndPassword()
		{
			//Any time we attempt to register a player, first silently authenticate the player.
			//This will retain the players True Origination (Android, iOS, Desktop)
			SilentlyAuthenticate((result) =>
			{
				if (result == null)
				{
				//something went wrong with Silent Authentication, Check the debug console.
				OnPlayFabError.Invoke(new PlayFabError()
					{
						Error = PlayFabErrorCode.UnknownError,
						ErrorMessage = "Silent Authentication by Device failed"
					});
				}

			//Note: If silent auth is success, which is should always be and the following 
			//below code fails because of some error returned by the server ( like invalid email or bad password )
			//this is okay, because the next attempt will still use the same silent account that was already created.

			//Now add our username & password.
			PlayFabClientAPI.AddUsernamePassword(new AddUsernamePasswordRequest()
				{
					Username = !string.IsNullOrEmpty(Username) ? Username : result.PlayFabId, //Because it is required & Unique and not supplied by User.
				Email = Email,
					Password = Password,
				}, (addResult) =>
				{
					if (OnLoginSuccess != null)
					{
					//Store identity and session
					_playFabId = result.PlayFabId;
						_sessionTicket = result.SessionTicket;

					//If they opted to be remembered on next login.
					if (RememberMe)
						{
						//Generate a new Guid 
						RememberMeId = Guid.NewGuid().ToString();
						//Fire and forget, but link the custom ID to this PlayFab Account.
						PlayFabClientAPI.LinkCustomID(new LinkCustomIDRequest()
							{
								CustomId = RememberMeId,
								ForceLink = ForceLink
							}, null, null);
						}

					//Override the auth type to ensure next login is using this auth type.
					AuthType = Authtypes.EmailAndPassword;

					//Report login result back to subscriber.
					OnLoginSuccess.Invoke(result);
					}
				}, (error) =>
				{
					if (OnPlayFabError != null)
					{
					//Report error result back to subscriber
					OnPlayFabError.Invoke(error);
					}
				});
			});
		}

		private void AuthenticateFacebook()
		{
#if FACEBOOK
        if (FB.IsInitialized && FB.IsLoggedIn && !string.IsNullOrEmpty(AuthTicket))
        {
            PlayFabClientAPI.LoginWithFacebook(new LoginWithFacebookRequest()
            {
                TitleId = PlayFabSettings.TitleId,
                AccessToken = AuthTicket,
                CreateAccount = true,
                InfoRequestParameters = InfoRequestParams
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
                    OnPlayFabError.Invoke(error);
                }
            });
        }
        else
        {
            if (OnDisplayAuthentication != null)
            {
                OnDisplayAuthentication.Invoke();
            }
        }
#endif
		}

		private void AuthenticateGooglePlayGames()
		{
#if GOOGLEGAMES
        PlayFabClientAPI.LoginWithGoogleAccount(new LoginWithGoogleAccountRequest()
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
                OnPlayFabError.Invoke(error);
            }
        });
#endif
		}

		private void SilentlyAuthenticate(System.Action<LoginResult> callback = null)
		{
#if ANDROID

        //Get the device id from native android
        AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
        AndroidJavaObject currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
        AndroidJavaObject contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver");
        AndroidJavaClass secure = new AndroidJavaClass("android.provider.Settings$Secure");
        string deviceId = secure.CallStatic<string>("getString", contentResolver, "android_id");

        //Login with the android device ID
        PlayFabClientAPI.LoginWithAndroidDeviceID(new LoginWithAndroidDeviceIDRequest() {
            TitleId = PlayFabSettings.TitleId,
            AndroidDevice = SystemInfo.deviceModel,
            OS = SystemInfo.operatingSystem,
            AndroidDeviceId = deviceId,
            CreateAccount = true,
            InfoRequestParameters = InfoRequestParams
        }, (result) => {
            
            //Store Identity and session
            _playFabId = result.PlayFabId;
            _sessionTicket = result.SessionTicket;

            //check if we want to get this callback directly or send to event subscribers.
            if (callback == null && OnLoginSuccess != null)
            {
                //report login result back to the subscriber
                OnLoginSuccess.Invoke(result);
            }else if (callback != null)
            {
                //report login result back to the caller
                callback.Invoke(result);
            }
        }, (error) => {

            //report errro back to the subscriber
            if(callback == null && OnPlayFabError != null){
                OnPlayFabError.Invoke(error);
            }else{
                //make sure the loop completes, callback with null
                callback.Invoke(null);
                //Output what went wrong to the console.
                Debug.LogError(error.GenerateErrorReport());
            }
        });

#elif __IOS__
        PlayFabClientAPI.LoginWithIOSDeviceID(new LoginWithIOSDeviceIDRequest() {
            TitleId = PlayFabSettings.TitleId,
            DeviceModel = SystemInfo.deviceModel, 
            OS = SystemInfo.operatingSystem,
            DeviceId = SystemInfo.deviceUniqueIdentifier,
            CreateAccount = true,
            InfoRequestParameters = InfoRequestParams
        }, (result) => {
            //Store Identity and session
            _playFabId = result.PlayFabId;
            _sessionTicket = result.SessionTicket;

            //check if we want to get this callback directly or send to event subscribers.
            if (callback == null && OnLoginSuccess != null)
            {
                //report login result back to the subscriber
                OnLoginSuccess.Invoke(result);
            }else if (callback != null)
            {
                //report login result back to the caller
                callback.Invoke(result);
            }
        }, (error) => {
            //report errro back to the subscriber
            if(callback == null && OnPlayFabError != null){
                OnPlayFabError.Invoke(error);
            }else{
                //make sure the loop completes, callback with null
                callback.Invoke(null);
                //Output what went wrong to the console.
                Debug.LogError(error.GenerateErrorReport());
            }
        });
#else
			PlayFabClientAPI.LoginWithCustomID(new LoginWithCustomIDRequest()
			{
				TitleId = PlayFabSettings.TitleId,
				CustomId = SystemInfo.deviceUniqueIdentifier,
				CreateAccount = true,
				InfoRequestParameters = InfoRequestParams
			}, (result) =>
			{
			//Store Identity and session
			_playFabId = result.PlayFabId;
				_sessionTicket = result.SessionTicket;

			//check if we want to get this callback directly or send to event subscribers.
			if (callback == null && OnLoginSuccess != null)
				{
				//report login result back to the subscriber
				OnLoginSuccess.Invoke(result);
				}
				else if (callback != null)
				{
				//report login result back to the caller
				callback.Invoke(result);
				}
			}, (error) =>
			{
			//report errro back to the subscriber
			if (callback == null && OnPlayFabError != null)
				{
					OnPlayFabError.Invoke(error);
				}
				else
				{
				//make sure the loop completes, callback with null
				callback.Invoke(null);
				//Output what went wrong to the console.
				Debug.LogError(error.GenerateErrorReport());
				}
			});
#endif
		}

		public void UnlinkSilentAuth()
		{
			SilentlyAuthenticate((result) =>
			{

#if ANDROID
            //Get the device id from native android
            AndroidJavaClass up = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            AndroidJavaObject currentActivity = up.GetStatic<AndroidJavaObject>("currentActivity");
            AndroidJavaObject contentResolver = currentActivity.Call<AndroidJavaObject>("getContentResolver");
            AndroidJavaClass secure = new AndroidJavaClass("android.provider.Settings$Secure");
            string deviceId = secure.CallStatic<string>("getString", contentResolver, "android_id");

            //Fire and forget, unlink this android device.
            PlayFabClientAPI.UnlinkAndroidDeviceID(new UnlinkAndroidDeviceIDRequest() {
                AndroidDeviceId = deviceId
            }, null, null);

#elif __IOS__
            PlayFabClientAPI.UnlinkIOSDeviceID(new UnlinkIOSDeviceIDRequest()
            {
                DeviceId = SystemInfo.deviceUniqueIdentifier
            }, null, null);
#else
			PlayFabClientAPI.UnlinkCustomID(new UnlinkCustomIDRequest()
				{
					CustomId = SystemInfo.deviceUniqueIdentifier
				}, null, null);
#endif

		});
		}
	}

	#endregion //Methods

}