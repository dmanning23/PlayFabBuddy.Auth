using FakeItEasy;
using NUnit.Framework;
using PlayFab;
using PlayFab.ClientModels;
using PlayFabBuddyLib;
using PlayFabBuddyLib.Auth;
using Plugin.DeviceInfo.Abstractions;
using Shouldly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PlayFabBuddy.Auth.Tests
{
	public class TestPlayFabAuthService : PlayFabAuthService
	{
		public TestPlayFabAuthService(IPlayFabClient playFabClient) : base(playFabClient)
		{
			OnDisplayAuthentication += TestPlayFabAuthService_OnDisplayAuthentication;
		}

		public virtual void TestPlayFabAuthService_OnDisplayAuthentication()
		{
		}

		public string TestRememberMeId
		{
			get
			{
				return RememberMeId;
			}
			set
			{
				RememberMeId = value;
			}
		}

		protected override Task SilentlyAuthenticate(Platform platform, Action<LoginResult> callback = null)
		{
			CalledSilentlyAuthenticate();
			return base.SilentlyAuthenticate(platform, callback);
		}

		public virtual void CalledSilentlyAuthenticate()
		{
		}

		protected override Task AuthenticateEmailPassword()
		{
			CalledAuthenticateEmailPassword();
			return base.AuthenticateEmailPassword();
		}

		public virtual void CalledAuthenticateEmailPassword()
		{
		}

		protected override Task AddAccountAndPassword()
		{
			CalledAddAccountAndPassword();
			return base.AddAccountAndPassword();
		}

		public virtual void CalledAddAccountAndPassword()
		{
		}

		protected override Task AuthenticateFacebook()
		{
			CalledAuthenticateFacebook();
			return base.AuthenticateFacebook();
		}

		public virtual void CalledAuthenticateFacebook()
		{
		}

		protected override Task AuthenticateGooglePlayGames()
		{
			CalledAuthenticateGooglePlayGames();
			return base.AuthenticateGooglePlayGames();
		}

		public virtual void CalledAuthenticateGooglePlayGames()
		{
		}
	}

	[TestFixture]
	public class Tests
	{
		TestPlayFabAuthService playFabAuth;
		IPlayFabClient playFabClient;

		[SetUp]
		public void Setup()
		{
			playFabClient = A.Fake<IPlayFabClient>();

			//A.CallTo(() => playFabClient.LoginWithCustomIDAsync(A<LoginWithCustomIDRequest>.Ignored, A<object>.Ignored, A<Dictionary<string, string>>.Ignored))
			//	.Returns(new Task<PlayFabResult<LoginResult>>(() => new PlayFabResult<LoginResult>()
			//	{
			//		Result = new LoginResult()
			//		{
			//			PlayFabId = "catpants",
			//			SessionTicket = "wtf"
			//		}
			//	}));

			playFabAuth = A.Fake<TestPlayFabAuthService>(options =>
			{
				options.CallsBaseMethods();
				options.WithArgumentsForConstructor(new object[] { playFabClient });
			});
		}

		[TestCase(true, true)]
		[TestCase(false, false)]
		public void RememberMeTest(bool initialValue, bool expectedResult)
		{
			playFabAuth.RememberMe = initialValue;
			playFabAuth.RememberMe.ShouldBe(expectedResult);
		}

		[TestCase(AuthType.None, AuthType.None)]
		[TestCase(AuthType.Silent, AuthType.Silent)]
		[TestCase(AuthType.UsernameAndPassword, AuthType.UsernameAndPassword)]
		[TestCase(AuthType.EmailAndPassword, AuthType.EmailAndPassword)]
		[TestCase(AuthType.RegisterPlayFabAccount, AuthType.RegisterPlayFabAccount)]
		[TestCase(AuthType.Facebook, AuthType.Facebook)]
		[TestCase(AuthType.Google, AuthType.Google)]
		public void AuthTypeTest(AuthType initialValue, AuthType expectedResult)
		{
			playFabAuth.AuthType = initialValue;
			playFabAuth.AuthType.ShouldBe(expectedResult);
		}

		[Test]
		public void ForceLinkInitialValue()
		{
			playFabAuth.ForceLink.ShouldBeFalse();
		}

		[TestCase("cat", "cat")]
		[TestCase("pants", "pants")]
		public void RememberMeIdTest(string initialValue, string expectedResult)
		{
			playFabAuth.TestRememberMeId = initialValue;
			playFabAuth.TestRememberMeId.ShouldBe(expectedResult);
		}

		[Test]
		public void GenerateRememberMeId()
		{
			playFabAuth.TestRememberMeId = string.Empty;
			playFabAuth.TestRememberMeId.ShouldNotBeNullOrEmpty();
		}

		[Test]
		public void ClearRememberMe()
		{
			playFabAuth.TestRememberMeId = "cat";
			playFabAuth.RememberMe = true;
			playFabAuth.AuthType = AuthType.RegisterPlayFabAccount;

			playFabAuth.ClearRememberMe();

			playFabAuth.TestRememberMeId.ShouldBeNullOrEmpty();
			playFabAuth.RememberMe.ShouldBeFalse();
			playFabAuth.AuthType.ShouldBe(AuthType.RegisterPlayFabAccount);
		}

		[Test]
		public async Task SilentAuthenticate_Called()
		{
			playFabAuth.AuthType = AuthType.Silent;
			await playFabAuth.Authenticate();

			A.CallTo(() => playFabAuth.CalledSilentlyAuthenticate()).MustHaveHappenedOnceExactly();
		}

		[Test]
		public async Task EmailAndPassword_Called()
		{
			playFabAuth.AuthType = AuthType.EmailAndPassword;
			await playFabAuth.Authenticate();

			A.CallTo(() => playFabAuth.CalledAuthenticateEmailPassword()).MustHaveHappenedOnceExactly();
		}

		[Test]
		public async Task RegisterPlayFabAccount_Called()
		{
			playFabAuth.AuthType = AuthType.RegisterPlayFabAccount;
			await playFabAuth.Authenticate();

			A.CallTo(() => playFabAuth.CalledAddAccountAndPassword()).MustHaveHappenedOnceExactly();
		}

		[Test]
		public async Task Facebook_Called()
		{
			playFabAuth.AuthType = AuthType.Facebook;
			await playFabAuth.Authenticate();

			A.CallTo(() => playFabAuth.CalledAuthenticateFacebook()).MustHaveHappenedOnceExactly();
		}

		[Test]
		public async Task Google_Called()
		{
			playFabAuth.AuthType = AuthType.Google;
			await playFabAuth.Authenticate();

			A.CallTo(() => playFabAuth.CalledAuthenticateGooglePlayGames()).MustHaveHappenedOnceExactly();
		}

		[Test]
		public async Task NoAuth_PopupDialog()
		{
			playFabAuth.AuthType = AuthType.None;
			await playFabAuth.Authenticate();

			A.CallTo(() => playFabAuth.TestPlayFabAuthService_OnDisplayAuthentication()).MustHaveHappenedOnceExactly();
		}

		[TestCase(true, "", false)]
		[TestCase(true, "catpants", true)]
		[TestCase(false, "", false)]
		[TestCase(false, "catpants", false)]
		public async Task EmailAuth_CustomID(bool rememberMe, string iD, bool expectedResult)
		{
			playFabAuth.RememberMe = rememberMe;
			playFabAuth.TestRememberMeId = iD;
			playFabAuth.AuthType = AuthType.EmailAndPassword;
			await playFabAuth.Authenticate();

			if (expectedResult)
			{
				A.CallTo(() => playFabClient.LoginWithCustomIDAsync(A<LoginWithCustomIDRequest>.Ignored, A<object>.Ignored, A<Dictionary<string, string>>.Ignored)).MustHaveHappenedOnceExactly();
			}
			else
			{
				A.CallTo(() => playFabClient.LoginWithCustomIDAsync(A<LoginWithCustomIDRequest>.Ignored, A<object>.Ignored, A<Dictionary<string, string>>.Ignored)).MustNotHaveHappened();
			}
		}

		[TestCase(true, "", true)]
		[TestCase(true, "catpants", false)]
		[TestCase(false, "", true)]
		[TestCase(false, "catpants", true)]
		public async Task EmailAuth_CustomID_DisplaysPopup(bool rememberMe, string iD, bool expectedResult)
		{
			playFabAuth.RememberMe = rememberMe;
			playFabAuth.TestRememberMeId = iD;
			playFabAuth.AuthType = AuthType.EmailAndPassword;
			await playFabAuth.Authenticate();

			if (expectedResult)
			{
				A.CallTo(() => playFabAuth.TestPlayFabAuthService_OnDisplayAuthentication()).MustHaveHappenedOnceExactly();
			}
			else
			{
				A.CallTo(() => playFabAuth.TestPlayFabAuthService_OnDisplayAuthentication()).MustNotHaveHappened();
			}
		}

		[TestCase("", "", true)]
		[TestCase("catpants", "", true)]
		[TestCase("", "catpants", true)]
		[TestCase("catpants", "catpants", false)]
		public async Task EmailAuth_MissingData(string email, string password, bool expectedResult)
		{
			playFabAuth.RememberMe = false;
			playFabAuth.Email = email;
			playFabAuth.Password = password;
			playFabAuth.AuthType = AuthType.EmailAndPassword;
			await playFabAuth.Authenticate();

			if (expectedResult)
			{
				A.CallTo(() => playFabAuth.TestPlayFabAuthService_OnDisplayAuthentication()).MustHaveHappenedOnceExactly();
			}
			else
			{
				A.CallTo(() => playFabAuth.TestPlayFabAuthService_OnDisplayAuthentication()).MustNotHaveHappened();
			}
		}

		[TestCase(true, "", "", "", true)]
		[TestCase(true, "id", "", "", false)]
		[TestCase(true, "id", "email", "", false)]
		[TestCase(true, "id", "", "password", false)]
		[TestCase(true, "", "email", "password", false)]
		[TestCase(true, "id", "email", "password", false)]
		[TestCase(false, "", "", "", true)]
		[TestCase(false, "id", "", "", true)]
		[TestCase(false, "id", "email", "", true)]
		[TestCase(false, "id", "", "password", true)]
		[TestCase(false, "", "email", "password", false)]
		[TestCase(false, "id", "email", "password", false)]
		public async Task EmailAuth_DisplaysPopup(bool rememberMe, string iD, string email, string password, bool expectedResult)
		{
			playFabAuth.RememberMe = rememberMe;
			playFabAuth.TestRememberMeId = iD;
			playFabAuth.Email = email;
			playFabAuth.Password = password;
			playFabAuth.AuthType = AuthType.EmailAndPassword;
			await playFabAuth.Authenticate();

			if (expectedResult)
			{
				A.CallTo(() => playFabAuth.TestPlayFabAuthService_OnDisplayAuthentication()).MustHaveHappenedOnceExactly();
			}
			else
			{
				A.CallTo(() => playFabAuth.TestPlayFabAuthService_OnDisplayAuthentication()).MustNotHaveHappened();
			}
		}
	}
}
