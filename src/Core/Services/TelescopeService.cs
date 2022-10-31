using Azure;
using Azure.Identity;
using Azure.ResourceManager;
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

                ResourceNodeType nodeType = kind == "functionapp" ? ResourceNodeType.Function : ResourceNodeType.WebService;

                apps.Add(
                    new WebAppInfo(app.Data.Name, nodeType)
                    {
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
}
