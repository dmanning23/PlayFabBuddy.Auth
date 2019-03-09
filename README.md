# PlayFabBuddy.Auth
Authentication wrapper around the PlayFab Xamarin client SDK

Porting the PlayFab authentication video examples to Xamarin and MonoGame:

https://www.youtube.com/watch?v=mqPiIlpQFBc

For an example of how to use this package, check out the https://github.com/dmanning23/PlayFabAuthExample project.

##How to use:

Step 1: 
Install the nuget package
https://www.nuget.org/packages/PlayFabBuddy.Auth/

Step 2:
Add the auth wrapper to your IoC container:
```
var client = new PlayFabClient("YOUR PLAYFAB APP ID!!!");
Services.AddService<IPlayFabClient>(client);
var auth = new PlayFabAuthService(client);
Services.AddService<IPlayFabAuthService>(auth);
```

Step 3:
Sign up for all the events that the auth wrapper uses to communicate:
```
//The auth wrapper wants to pop up some kind of login screen (that you have created in whatever gui library you are using)
auth.OnDisplayAuthentication += Auth_OnDisplayAuthentication;

//The auth wrapper is in the process of talking to the PlayFab backend to log in
auth.OnLoggingIn += Auth_OnLoggingIn;

//The auth wrapper has logged into PlayFab successfully
auth.OnLoginSuccess += Auth_OnLoginSuccess;

//The auth wrapper hit the fan while trying to log in
auth.OnPlayFabError += Auth_OnPlayFabError;
```

Step 4:
Try to authenticate
```
auth.Authenticate();
```
