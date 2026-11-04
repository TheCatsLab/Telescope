using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Models;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension.Core.Services;

internal class TelescopeService
{
    private readonly ArmClient _armClient;

    // cancels all the loading operations of the current loading session
    private CancellationTokenSource _loadingCancellationTokenSource = new();

    public TelescopeService()
    {
        _armClient = new ArmClient(new DefaultAzureCredential());
    }

    /// <summary>
    /// Cancels all the loading operations that are currently in progress
    /// </summary>
    public void CancelLoading()
    {
        _loadingCancellationTokenSource.Cancel();
        _loadingCancellationTokenSource.Dispose();
        _loadingCancellationTokenSource = new CancellationTokenSource();
    }

    /// <summary>
    /// Creates a source linked to the session-wide one; the caller owns its lifetime and must
    /// dispose it - the linked registration is a strong reference from the session source,
    /// so a disposed-late linked source would stay rooted by it
    /// </summary>
    private CancellationTokenSource CombineTokens(CancellationToken cancellationToken)
    {
        return CancellationTokenSource.CreateLinkedTokenSource(_loadingCancellationTokenSource.Token, cancellationToken);
    }

    public event EventHandler<Guid> LoadingStarted;
    public event EventHandler<Guid> LoadingCompleted;

    public virtual async Task<IEnumerable<TenantResource>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        List<TenantResource> tenants = new();
        TenantCollection tenantCollection = _armClient.GetTenants();

        Guid actionId = Guid.NewGuid();
        LoadingStarted?.Invoke(this, actionId);

        try
        {
            await foreach (TenantResource tenant in tenantCollection.ConfigureAwait(false))
            {
                tenants.Add(tenant);

                cancellationToken.ThrowIfCancellationRequested();
            }

            return tenants;
        }
        finally
        {
            LoadingCompleted?.Invoke(this, actionId);
        }
    }

    public virtual async Task<IEnumerable<SubscriptionResource>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        List<SubscriptionResource> subscriptions = new();
        SubscriptionCollection subscriptionCollection = _armClient.GetSubscriptions();

        Guid actionId = Guid.NewGuid();
        LoadingStarted?.Invoke(this, actionId);

        try
        {
            await foreach (SubscriptionResource subscription in subscriptionCollection.ConfigureAwait(false))
            {
                subscriptions.Add(subscription);

                cancellationToken.ThrowIfCancellationRequested();
            }

            return subscriptions;
        }
        finally
        {
            LoadingCompleted?.Invoke(this, actionId);
        }
    }

    public virtual async Task<IEnumerable<ResourceGroupResource>> GetResourceGroupsAsync(SubscriptionResource subscription, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        List<ResourceGroupResource> groups = new();
        ResourceGroupCollection groupCollection = subscription.GetResourceGroups();

        Guid actionId = Guid.NewGuid();
        LoadingStarted?.Invoke(this, actionId);

        try
        {
            await foreach (ResourceGroupResource group in groupCollection.ConfigureAwait(false))
            {
                groups.Add(group);

                cancellationToken.ThrowIfCancellationRequested();
            }

            return groups;
        }
        finally
        {
            LoadingCompleted?.Invoke(this, actionId);
        }
    }

    public virtual async Task<IEnumerable<AzureLogicAppInfo>> GetLogicAppsAsync(ResourceGroupResource resourceGroup, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        AsyncPageable<GenericResource> resources = resourceGroup.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Logic/workflows'", cancellationToken: cancellationToken);
        List<AzureLogicAppInfo> apps = new();

        Guid actionId = Guid.NewGuid();
        LoadingStarted?.Invoke(this, actionId);

        try
        {
            await foreach (var resource in resources)
            {
                GenericResource app = await resource.GetAsync();

                apps.Add(
                    new AzureLogicAppInfo(app.Data.Name)
                    {
                        ArmId = app.Data.Id?.ToString(),
                        Data = app.Data.Properties != null ? JValue.Parse(app.Data.Properties.ToString()).ToString(Newtonsoft.Json.Formatting.Indented) : null,
                        Tags = app.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                    });

                cancellationToken.ThrowIfCancellationRequested();
            }

            return apps;
        }
        finally
        {
            LoadingCompleted?.Invoke(this, actionId);
        }
    }

    public virtual async Task<IEnumerable<WebAppInfo>> GetWebAppsAsync(ResourceGroupResource resourceGroup, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        AsyncPageable<GenericResource> resources = resourceGroup.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Web/sites'", cancellationToken: cancellationToken);
        List<WebAppInfo> apps = new();

        Guid actionId = Guid.NewGuid();
        LoadingStarted?.Invoke(this, actionId);

        try
        {
            await foreach (var resource in resources)
            {
                GenericResource app = await resource.GetAsync();

                var jToken = JValue.Parse(app.Data.Properties.ToString());

                var kind = jToken.Value<string>("kind") ?? "app";

                // the kind can contain several comma-separated values, e.g. "functionapp,linux"
                ResourceNodeType nodeType = kind.Contains("functionapp") ? ResourceNodeType.Function : ResourceNodeType.WebService;

                // the slots of the app are loaded lazily when the node is expanded;
                // the closure outlives this loading session, so it passes CancellationToken.None -
                // capturing the combined token of this load would pin its linked CTS for as long
                // as the node holds the closure; GetSlotsAsync combines with the session-wide
                // token itself, so cancelling loadings still works
                ResourceIdentifier siteId = app.Data.Id;

                apps.Add(
                    new WebAppInfo(app.Data.Name, nodeType, () => GetSlotsAsync(siteId, CancellationToken.None))
                    {
                        ArmId = app.Data.Id?.ToString(),
                        Data = app.Data.Properties != null ? JValue.Parse(app.Data.Properties.ToString()).ToString(Newtonsoft.Json.Formatting.Indented) : null,
                        Tags = app.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        EnvironmentVariables = await GetEnvironmentVariablesAsync(resource.Id, cancellationToken).ConfigureAwait(false)
                    });

                cancellationToken.ThrowIfCancellationRequested();
            }

            return apps;
        }
        finally
        {
            LoadingCompleted?.Invoke(this, actionId);
        }
    }

    /// <summary>
    /// Returns the environment variables (app settings) of a web app, or null if they cannot be retrieved
    /// </summary>
    /// <param name="siteId">id of a Microsoft.Web/sites resource</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private Task<Dictionary<string, string>> GetEnvironmentVariablesAsync(ResourceIdentifier siteId, CancellationToken cancellationToken = default)
    {
        WebSiteResource site = _armClient.GetWebSiteResource(siteId);

        return FetchEnvironmentVariablesAsync(() => site.GetApplicationSettingsAsync(cancellationToken));
    }

    /// <summary>
    /// Returns the environment variables (app settings) of a web app deployment slot, or null if they cannot be retrieved
    /// </summary>
    /// <param name="slot">the slot resource</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private Task<Dictionary<string, string>> GetEnvironmentVariablesAsync(WebSiteSlotResource slot, CancellationToken cancellationToken = default)
    {
        return FetchEnvironmentVariablesAsync(() => slot.GetApplicationSettingsSlotAsync(cancellationToken));
    }

    /// <summary>
    /// Fetches the environment variables (app settings) using the passed <paramref name="fetch"/> delegate,
    /// or returns null if they cannot be retrieved
    /// </summary>
    private async Task<Dictionary<string, string>> FetchEnvironmentVariablesAsync(Func<Task<Response<AppServiceConfigurationDictionary>>> fetch)
    {
        try
        {
            Response<AppServiceConfigurationDictionary> appSettings = await fetch().ConfigureAwait(false);

            return appSettings?.Value?.Properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch
        {
            // the environment variables are not critical for the app representation, so just skip them in case of any issue
            return null;
        }
    }

    /// <summary>
    /// Returns the deployment slots of a web app; throws if they cannot be listed(the node shows a retry link)
    /// </summary>
    /// <param name="siteId">id of a Microsoft.Web/sites resource</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public virtual async Task<IEnumerable<ResourceNode>> GetSlotsAsync(ResourceIdentifier siteId, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;

        // the list is allocated lazily - apps without slots(the common case) skip the allocation entirely
        List<DeploymentSlotInfo> slots = null;

        Guid actionId = Guid.NewGuid();
        LoadingStarted?.Invoke(this, actionId);

        try
        {
            WebSiteResource site = _armClient.GetWebSiteResource(siteId);

            WebSiteSlotCollection slotCollection = site.GetWebSiteSlots();

            await foreach (WebSiteSlotResource slot in slotCollection.GetAllAsync(cancellationToken).ConfigureAwait(false))
            {
                // the slot resource name is composite("sitename/slotname"), the display name is its last segment
                string slotName = slot.Data?.Id?.Name ?? slot.Data?.Name?.Split('/').Last() ?? "unknown";

                Dictionary<string, string> environmentVariables = await GetEnvironmentVariablesAsync(slot, cancellationToken).ConfigureAwait(false);

                slots ??= new List<DeploymentSlotInfo>();

                slots.Add(new DeploymentSlotInfo(slotName)
                {
                    ArmId = slot.Data?.Id?.ToString(),
                    Data = await GetSlotConfigJsonAsync(slot, cancellationToken).ConfigureAwait(false)
                           ?? GetEnvironmentVariablesJson(environmentVariables),
                    Tags = slot.Data?.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                    EnvironmentVariables = environmentVariables
                });

                cancellationToken.ThrowIfCancellationRequested();
            }

            return slots ?? Enumerable.Empty<DeploymentSlotInfo>();
        }
        finally
        {
            LoadingCompleted?.Invoke(this, actionId);
        }
    }

    /// <summary>
    /// Returns the environment variables serialized as an indented JSON string;
    /// used as the Configuration JSON fallback for slots whose configuration cannot be retrieved
    /// </summary>
    private static string GetEnvironmentVariablesJson(Dictionary<string, string> environmentVariables)
    {
        return environmentVariables is null
            ? null
            : JToken.FromObject(environmentVariables).ToString(Newtonsoft.Json.Formatting.Indented);
    }

    /// <summary>
    /// Returns the slot configuration serialized as an indented JSON string, or null if it cannot be retrieved
    /// </summary>
    private static async Task<string> GetSlotConfigJsonAsync(WebSiteSlotResource slot, CancellationToken cancellationToken)
    {
        try
        {
            // the SDK exposes only the sync variant - it runs off the UI thread here
            WebSiteSlotConfigResource config = await Task.Run(() => slot.GetWebSiteSlotConfig(), cancellationToken).ConfigureAwait(false);

            return config.HasData && config.Data is not null
                ? JToken.FromObject(config.Data).ToString(Newtonsoft.Json.Formatting.Indented)
                : null;
        }
        catch
        {
            // the configuration is not critical for the slot representation, the JSON tab just stays empty
            return null;
        }
    }
}
