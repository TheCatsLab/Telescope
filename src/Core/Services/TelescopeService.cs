using Azure;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension.Core.Services;

internal interface ITelescopeService
{
    Task<IEnumerable<AzureLogicAppInfo>> LoadLogicAppsAsync(CancellationToken? cancellationToken = null);
}


internal class TelescopeService
{
    private readonly ArmClient _armClient;

    public TelescopeService()
    {
        _armClient = new ArmClient(new DefaultAzureCredential());
    }


    public async Task<IEnumerable<SubscriptionResource>> LoadSubscriptionsAsync(CancellationToken cancellationToken = default)
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

    public async Task<IEnumerable<ResourceGroupResource>> LoadResourceGroupsAsync(SubscriptionResource subscription, CancellationToken cancellationToken = default)
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

    //enum AzureResources
    //{
    //    Logic,
    //    //ServiceBus,
    //    //Web

    //}
    //bool IsResourceAcceptable(string resourceId)
    //{
    //    foreach (AzureResources res in (AzureResources[])Enum.GetValues(typeof(AzureResources)))
    //    {
    //        if (resourceId.Contains("Microsoft." + res.ToString())) return true;
    //    }
    //    return false;
    //}

    public async Task<IEnumerable<AzureLogicAppInfo>> LoadLogicAppsAsync(ResourceGroupResource resourceGroup, CancellationToken cancellationToken = default)
    {
        AsyncPageable<GenericResource> resources = resourceGroup.GetGenericResourcesAsync(filter: "resourceType eq 'Microsoft.Logic/workflows'", cancellationToken: cancellationToken);
        List<AzureLogicAppInfo> apps = new();

        await foreach (var resource in resources)
        {
            apps.Add(new AzureLogicAppInfo("", resourceGroup.Id, resource.Id));

            cancellationToken.ThrowIfCancellationRequested();
        }

        return apps;
    }
}
