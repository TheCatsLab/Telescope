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

    public TelescopeService()
    {
        _armClient = new ArmClient(new DefaultAzureCredential());
    }

    public event EventHandler<Guid> LoadingStarted;
    public event EventHandler<Guid> LoadingCompleted;

    public virtual async Task<IEnumerable<TenantResource>> GetTenantsAsync(CancellationToken cancellationToken = default)
    {
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

                apps.Add(
                    new WebAppInfo(app.Data.Name, nodeType)
                    {
                        ArmId = app.Data.Id?.ToString(),
                        Data = app.Data.Properties != null ? JValue.Parse(app.Data.Properties.ToString()).ToString(Newtonsoft.Json.Formatting.Indented) : null,
                        Tags = app.Data.Tags?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        EnvironmentVariables = await GetEnvironmentVariablesAsync(resource.Id, cancellationToken)
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
    private async Task<Dictionary<string, string>> GetEnvironmentVariablesAsync(ResourceIdentifier siteId, CancellationToken cancellationToken = default)
    {
        try
        {
            WebSiteResource site = _armClient.GetWebSiteResource(siteId);

            Response<AppServiceConfigurationDictionary> appSettings = await site.GetApplicationSettingsAsync(cancellationToken).ConfigureAwait(false);

            return appSettings?.Value?.Properties?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        catch
        {
            // the environment variables are not critical for the app representation, so just skip them in case of any issue
            return null;
        }
    }
}
