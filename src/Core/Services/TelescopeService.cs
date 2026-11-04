using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Models;
using Cats.Telescope.VsExtension.Core.Utils;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension.Core.Services;

internal class TelescopeService : IDisposable
{
    private readonly ArmClient _armClient;
    private readonly object _sessionSync = new();
    private bool _isDisposed;

    // bounds the ARM fan-out: the tree loads all subscriptions and their resource groups
    // concurrently, so a large tenant would otherwise issue hundreds of simultaneous
    // requests (memory spikes, socket pressure, ARM 429s); the helpers called inside a
    // gated method inherit its slot - they must never be gated themselves, that would
    // deadlock under saturation
    private readonly SemaphoreSlim _armGate = new(8, 16);

    // the app settings fetched for the search scope are cached for the session keyed by
    // the app id - Refresh recreates the nodes and would otherwise re-request every
    // app's settings on the next search; the LRU keeps the retention bounded, and failed
    // fetches (null) are not cached, so they are retried
    private readonly LruCache<string, Dictionary<string, string>> _settingsCache = new(300);

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
        CancellationTokenSource previous;

        lock (_sessionSync)
        {
            if (_isDisposed)
                return;

            previous = _loadingCancellationTokenSource;
            _loadingCancellationTokenSource = new CancellationTokenSource();
        }

        // cancel outside the lock: the linked sources' callbacks run synchronously
        // and must not be able to re-enter the session lock
        previous.Cancel();
        previous.Dispose();
    }

    /// <summary>
    /// Releases the session-wide cancellation source; the service must not be used afterwards
    /// </summary>
    public void Dispose()
    {
        CancellationTokenSource current;

        lock (_sessionSync)
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            current = _loadingCancellationTokenSource;
        }

        current.Cancel();
        current.Dispose();
    }

    /// <summary>
    /// Creates a source linked to the session-wide one; the caller owns its lifetime and must
    /// dispose it - the linked registration is a strong reference from the session source,
    /// so a disposed-late linked source would stay rooted by it
    /// </summary>
    private CancellationTokenSource CombineTokens(CancellationToken cancellationToken)
    {
        // the session source is replaced and disposed on cancel, so linking to it is
        // only safe while holding the same lock
        lock (_sessionSync)
        {
            if (_isDisposed)
                return CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            return CancellationTokenSource.CreateLinkedTokenSource(_loadingCancellationTokenSource.Token, cancellationToken);
        }
    }

    /// <summary>
    /// Token of the current loading session; deferred loaders (invoked when a node is
    /// selected) must not capture the token of the session that created the node
    /// </summary>
    private CancellationToken SessionToken
    {
        get
        {
            lock (_sessionSync)
            {
                return _isDisposed ? CancellationToken.None : _loadingCancellationTokenSource.Token;
            }
        }
    }

    public event EventHandler<Guid> LoadingStarted;
    public event EventHandler<Guid> LoadingCompleted;

    /// <summary>
    /// Raises <see cref="LoadingStarted"/> without letting a faulty subscriber break the
    /// loading; the pairing <see cref="LoadingCompleted"/> is raised from a finally block
    /// </summary>
    private void RaiseLoadingStarted(Guid actionId)
    {
        try
        {
            LoadingStarted?.Invoke(this, actionId);
        }
        catch
        {
            // the loading itself must go on; an unbalanced id in a subscriber is its own issue
        }
    }

    /// <summary>
    /// Raises <see cref="LoadingCompleted"/> without letting a faulty subscriber mask the
    /// original loading exception
    /// </summary>
    private void RaiseLoadingCompleted(Guid actionId)
    {
        try
        {
            LoadingCompleted?.Invoke(this, actionId);
        }
        catch
        {
        }
    }

    public virtual async Task<IEnumerable<TenantResource>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        List<TenantResource> tenants = new();
        TenantCollection tenantCollection = _armClient.GetTenants();

        Guid actionId = Guid.NewGuid();

        try
        {
            RaiseLoadingStarted(actionId);

            await _armGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                _armGate.Release();
            }
        }
        finally
        {
            RaiseLoadingCompleted(actionId);
        }
    }

    public virtual async Task<IEnumerable<SubscriptionResource>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        List<SubscriptionResource> subscriptions = new();
        SubscriptionCollection subscriptionCollection = _armClient.GetSubscriptions();

        Guid actionId = Guid.NewGuid();

        try
        {
            RaiseLoadingStarted(actionId);

            await _armGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                _armGate.Release();
            }
        }
        finally
        {
            RaiseLoadingCompleted(actionId);
        }
    }

    public virtual async Task<IEnumerable<ResourceGroupResource>> GetResourceGroupsAsync(SubscriptionResource subscription, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        List<ResourceGroupResource> groups = new();
        ResourceGroupCollection groupCollection = subscription.GetResourceGroups();

        Guid actionId = Guid.NewGuid();

        try
        {
            RaiseLoadingStarted(actionId);

            await _armGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                _armGate.Release();
            }
        }
        finally
        {
            RaiseLoadingCompleted(actionId);
        }
    }

    public virtual async Task<IEnumerable<AzureLogicAppInfo>> GetLogicAppsAsync(ResourceGroupResource resourceGroup, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        AsyncPageable<GenericResource> resources = resourceGroup.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Logic/workflows'", cancellationToken: cancellationToken);
        List<AzureLogicAppInfo> apps = new();

        Guid actionId = Guid.NewGuid();

        try
        {
            RaiseLoadingStarted(actionId);

            await _armGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await foreach (var resource in resources)
                {
                    GenericResource app = await resource.GetAsync();

                    // keep the raw JSON in the deferred loader - a parsed JToken graph costs
                    // several times the string and would sit in every not-yet-inspected node
                    string rawData = app.Data.Properties?.ToString();

                    apps.Add(
                        new AzureLogicAppInfo(app.Data.Name)
                        {
                            ArmId = app.Data.Id?.ToString(),
                            // the formatted JSON is only needed when the node is inspected
                            DataLoader = rawData is null
                                ? null
                                : () => Task.FromResult(FormatJson(rawData)),
                            Tags = app.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
                        });

                    cancellationToken.ThrowIfCancellationRequested();
                }

                return apps;
            }
            finally
            {
                _armGate.Release();
            }
        }
        finally
        {
            RaiseLoadingCompleted(actionId);
        }
    }

    public virtual async Task<IEnumerable<WebAppInfo>> GetWebAppsAsync(ResourceGroupResource resourceGroup, CancellationToken cancellationToken = default)
    {
        using CancellationTokenSource sessionLink = CombineTokens(cancellationToken);
        cancellationToken = sessionLink.Token;
        AsyncPageable<GenericResource> resources = resourceGroup.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Web/sites'", cancellationToken: cancellationToken);
        List<WebAppInfo> apps = new();

        Guid actionId = Guid.NewGuid();

        try
        {
            RaiseLoadingStarted(actionId);

            await _armGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await foreach (var resource in resources)
                {
                    GenericResource app = await resource.GetAsync();

                    // keep the raw JSON in the deferred loader - a parsed JToken graph costs
                    // several times the string and would sit in every not-yet-inspected node
                    string rawData = app.Data.Properties?.ToString();

                    // the kind is read from the properties once, right here; the parse result
                    // is discarded with the loop iteration and only the raw string is retained
                    var kind = rawData is null ? "app" : JValue.Parse(rawData).Value<string>("kind") ?? "app";

                    // the kind can contain several comma-separated values, e.g. "functionapp,linux"
                    ResourceNodeType nodeType = kind.Contains("functionapp") ? ResourceNodeType.Function : ResourceNodeType.WebService;

                    // the slots of the app are loaded lazily when the node is expanded;
                    // the closure outlives this loading session, so it passes CancellationToken.None -
                    // capturing the combined token of this load would pin its linked CTS for as long
                    // as the node holds the closure; GetSlotsAsync combines with the session-wide
                    // token itself, so cancelling loadings still works
                    ResourceIdentifier siteId = app.Data.Id;
                    ResourceIdentifier appId = resource.Id;

                    apps.Add(
                        new WebAppInfo(app.Data.Name, nodeType, () => GetSlotsAsync(siteId, CancellationToken.None))
                        {
                            ArmId = app.Data.Id?.ToString(),
                            // configuration and app settings are materialized on demand:
                            // fetching the settings for every app in every resource group
                            // costs one extra ARM request per app during the tree load
                            DataLoader = rawData is null
                                ? null
                                : () => Task.FromResult(FormatJson(rawData)),
                            Tags = app.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                            EnvironmentVariablesLoader = () => GetEnvironmentVariablesAsync(appId, SessionToken)
                        });

                    cancellationToken.ThrowIfCancellationRequested();
                }

                return apps;
            }
            finally
            {
                _armGate.Release();
            }
        }
        finally
        {
            RaiseLoadingCompleted(actionId);
        }
    }

    /// <summary>
    /// Returns the environment variables (app settings) of a web app, or null if they cannot be retrieved
    /// </summary>
    /// <param name="siteId">id of a Microsoft.Web/sites resource</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private async Task<Dictionary<string, string>> GetEnvironmentVariablesAsync(ResourceIdentifier siteId, CancellationToken cancellationToken = default)
    {
        // the session cache serves the search scope without re-requesting settings that
        // were already fetched - including after a Refresh recreated the nodes
        string cacheKey = siteId.ToString();

        if (_settingsCache.TryGet(cacheKey, out Dictionary<string, string> cached))
            return cached;

        // this overload serves the deferred loader of a selected node - it runs outside
        // any other gated method, so it takes its own slot; the slot-based overload below
        // is only called from gated GetSlotsAsync and must not gate again
        await _armGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WebSiteResource site = _armClient.GetWebSiteResource(siteId);

            Dictionary<string, string> settings = await FetchEnvironmentVariablesAsync(() => site.GetApplicationSettingsAsync(cancellationToken)).ConfigureAwait(false);

            // null means the fetch failed - not caching it lets the next pass retry
            if (settings != null)
                _settingsCache.Set(cacheKey, settings);

            return settings;
        }
        finally
        {
            _armGate.Release();
        }
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

        try
        {
            RaiseLoadingStarted(actionId);

            await _armGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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
                _armGate.Release();
            }
        }
        finally
        {
            RaiseLoadingCompleted(actionId);
        }
    }

    /// <summary>
    /// Formats a raw JSON string as indented; used by the deferred data loaders,
    /// which keep only the raw string until the node is inspected
    /// </summary>
    private static string FormatJson(string raw)
    {
        return JValue.Parse(raw).ToString(Newtonsoft.Json.Formatting.Indented);
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
