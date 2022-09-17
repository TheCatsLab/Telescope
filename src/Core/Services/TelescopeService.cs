using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Models;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;
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


    public virtual async IAsyncEnumerable<SubscriptionResource> LoadSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        SubscriptionCollection subscriptionCollection = _armClient.GetSubscriptions();

        await foreach (SubscriptionResource subscription in subscriptionCollection.ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            Debug.WriteLine($"-Subscription {subscription.Id} loaded");

            yield return subscription;
        }
    }

    public virtual async Task<IEnumerable<SubscriptionResource>> GetSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        List<SubscriptionResource> subscriptions = new();
        SubscriptionCollection subscriptionCollection = _armClient.GetSubscriptions();

        await foreach(SubscriptionResource subscription in subscriptionCollection.ConfigureAwait(false))
        {
            subscriptions.Add(subscription);

            cancellationToken.ThrowIfCancellationRequested();
        }

        return subscriptions;
    }

    public virtual async IAsyncEnumerable<ResourceGroupResource> LoadResourceGroupsAsync(SubscriptionResource subscription, CancellationToken cancellationToken = default)
    {
        ResourceGroupCollection groupCollection = subscription.GetResourceGroups();

        await foreach (ResourceGroupResource group in groupCollection.ConfigureAwait(false))
        {
            cancellationToken.ThrowIfCancellationRequested();

            Debug.WriteLine($"--Group {group.Id} loaded");

            yield return group;
        }
    }

    public virtual async Task<IEnumerable<ResourceGroupResource>> GetResourceGroupsAsync(SubscriptionResource subscription, CancellationToken cancellationToken = default)
    {
        List<ResourceGroupResource> groups = new();
        ResourceGroupCollection groupCollection = subscription.GetResourceGroups();

        await foreach(ResourceGroupResource group in groupCollection.ConfigureAwait(false))
        {
            groups.Add(group);

            cancellationToken.ThrowIfCancellationRequested();
        }

        return groups;
    }

    public virtual async IAsyncEnumerable<AzureLogicAppInfo> LoadLogicAppsAsync(ResourceGroupResource resourceGroup, CancellationToken cancellationToken = default)
    {
        AsyncPageable<GenericResource> resources = resourceGroup.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Logic/workflows'", cancellationToken: cancellationToken);

        await foreach (var resource in resources)
        {
            cancellationToken.ThrowIfCancellationRequested();

            Debug.WriteLine($"---App {resource.Id} loaded");

            yield return new AzureLogicAppInfo(resource.Id);
        }
    }

    public virtual async Task<IEnumerable<AzureLogicAppInfo>> GetLogicAppsAsync(ResourceGroupResource resourceGroup, CancellationToken cancellationToken = default)
    {
        AsyncPageable<GenericResource> resources = resourceGroup.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Logic/workflows'", cancellationToken: cancellationToken);
        List<AzureLogicAppInfo> apps = new();

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
}
