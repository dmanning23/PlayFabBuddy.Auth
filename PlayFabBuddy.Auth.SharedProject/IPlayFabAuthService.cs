using System;
using System.Threading.Tasks;
using PlayFab;
using PlayFab.ClientModels;

namespace PlayFabBuddyLib.Auth
{
	public interface IPlayFabAuthService
	{
		event PlayFabAuthService.DisplayAuthenticationEvent OnDisplayAuthentication;
		event PlayFabAuthService.LoggingInEvent OnLoggingIn;
		event PlayFabAuthService.LoginSuccessEvent OnLoginSuccess;
		event PlayFabAuthService.PlayFabErrorEvent OnPlayFabError;
		event EventHandler OnLogout;

		string Email { get; set; }
		string Username { get; set; }
		string Password { get; set; }
		string AuthTicket { get; set; }

		bool ForceLink { get; set; }

		string PlayFabId { get; }
		string SessionTicket { get; }

		bool IsLoggedIn { get; }

		void Logout();

		bool RememberMe { get; set; }
		AuthType AuthType { get; set; }

		Task Authenticate();
		Task Authenticate(AuthType authType);
		void ClearRememberMe();
		Task UnlinkSilentAuth();

		Task LinkFacebook();
	}
}