using System.Threading.Tasks;

namespace PlayFabBuddyLib.Auth
{
	public interface IPlayFabDisplayNameService
	{
		event PlayFabDisplayNameService.DisplayNameChangeEvent OnDisplayNameChange;

		/// <summary>
		/// Get the current logged in user's display name
		/// </summary>
		/// <returns></returns>
		Task<string> GetDisplayName();

		/// <summary>
		/// Set the current logged in user's display name
		/// </summary>
		/// <param name="displayName"></param>
		/// <returns>Empty if successul, the error message if something went wrong</returns>
		Task<string> SetDisplayName(string displayName);
	}
}
