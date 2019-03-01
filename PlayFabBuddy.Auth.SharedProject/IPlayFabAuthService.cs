using System.Threading.Tasks;
using PlayFab.ClientModels;

namespace PlayFabBuddyLib.Auth
{
	public interface IPlayFabAuthService
	{
		event PlayFabAuthService.DisplayAuthenticationEvent OnDisplayAuthentication;
		event PlayFabAuthService.LoginSuccessEvent OnLoginSuccess;
		event PlayFabAuthService.PlayFabErrorEvent OnPlayFabError;

		string Email { get; set; }
		string Username { get; set; }
		string Password { get; set; }
		string AuthTicket { get; set; }

		GetPlayerCombinedInfoRequestParams InfoRequestParams { get; set; }

		bool ForceLink { get; set; }

		string PlayFabId { get; }
		string SessionTicket { get; }

		bool RememberMe { get; set; }
		AuthType AuthType { get; set; }

		Task Authenticate();
		Task Authenticate(AuthType authType);
		void ClearRememberMe();
		Task UnlinkSilentAuth();
	}
}