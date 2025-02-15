using Microsoft.Identity.Web;

using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;

using AK.OAuthSamples.OpenIdDict.Server.Authorisation;

namespace AK.OAuthSamples.OpenIdDict.Server.Configuration;

internal static partial class ServiceCollectionExtensions
{
	/// <summary>
	///		Register the auth engine
	/// </summary>
	/// <remarks>
	///		OAuth 2.0 uses two endpoints: `/authorize` and `/oauth/token` (https://auth0.com/docs/authenticate/protocols/oauth).
	///		Their meaning for the Authorization Code Flow:
	///			`/authorize` – issues an authorization code (redirects to Azure AD) 
	///			`/token` – exchanges the authorization code for an access token (how does it get used for refreshing the token?)
	/// </remarks>
	public static IServiceCollection AddAndConfigureAuthorisation(this IServiceCollection services, AppSettings settings)
	{
		// Register the OpenIddict services
		services.AddOpenIddict()
				// Register the OpenIddict server components.
				.AddServer(options =>
				{
					// Enable the authorization and token endpoints
					options 
							.SetTokenEndpointUris("/connect/token")
							.SetAuthorizationEndpointUris("/connect/authorize")
							.AddEventHandler<OpenIddictServerEvents.ValidateAuthorizationRequestContext>(builder => builder.UseInlineHandler(OpenIdDictEvents.ValidateAuthorizationRequestFunc(settings.Auth)))
							.AddEventHandler<OpenIddictServerEvents.ValidateTokenRequestContext>(builder => builder.UseInlineHandler(OpenIdDictEvents.ValidateTokenRequestFunc(settings.Auth)))
							.AddEventHandler<OpenIddictServerEvents.HandleAuthorizationRequestContext>(builder => builder.UseInlineHandler(OpenIdDictEvents.HandleAuthorizationRequest(settings.Auth)))
					// Enable the Authorization Code Flow with PKCE and Refresh Token Flow
							.AllowAuthorizationCodeFlow()
							.RequireProofKeyForCodeExchange()
							.AllowRefreshTokenFlow()

					// Register the signing and encryption credentials
							// Note 1: Ephemeral keys are used for development only and need to be replaced with a generated certificate. See https://github.com/openiddict/openiddict-core/issues/976 for more advice
							// Note 2: the methods below require the application pool to be configured to load a user profile, otherwise it'll cause an exception being thrown at runtime
							// Alternatively, these 2 methods can be used: .AddEphemeralEncryptionKey().AddEphemeralSigningKey()
							.AddDevelopmentEncryptionCertificate()
							.AddDevelopmentSigningCertificate()
							.DisableAccessTokenEncryption();

					// Register scopes.
					// Note 1: 'openid' scope is there by default and 'offline_access' scope is added above by calling 'AllowRefreshTokenFlow()'
					// Note 2: due to using 'degraded' mode, the scopes need to be re-added on creating a new principal
					options.RegisterScopes(settings.Auth.Scope);

					// Need Degraded Mode to use bare-bones of OpenIdDict
					options.EnableDegradedMode()
					// Register the ASP.NET Core host and configure the ASP.NET Core-specific options.	
							.UseAspNetCore();
				})
				// Register the OpenIddict validation components.
				.AddValidation(options =>
				{
					// Import the configuration from the local OpenIddict server instance.
					options.UseLocalServer();
					// Register the ASP.NET Core host.
					options.UseAspNetCore();
				});
		
		services.AddAuthorization()
				.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
				// Configuration for the linked Azure AD tenant
				.AddMicrosoftIdentityWebApp(options =>
				{
					options.Instance = settings.AzureAd.Instance;
					options.TenantId = settings.AzureAd.Tenant;
					options.ClientId = settings.AzureAd.ClientId;
					// Note: Scopes can be ignored if you need from MS a token_id only
				});
		return services;
	}
}