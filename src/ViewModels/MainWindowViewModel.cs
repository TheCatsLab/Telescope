using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Enums;
using Cats.Telescope.VsExtension.Core.Models;
using Cats.Telescope.VsExtension.Core.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Cats.Telescope.VsExtension.ViewModels
{
    internal class MainWindowViewModel : ViewModelBase
    {
        #region Constants

        private const string DefaultBusyText = "Loading...";

        #endregion

        #region Fields

        private bool _isBusy;
        private string _busyText;
        private ObservableCollection<ResourceNode> _resourceNodes;
        private ResourceNode _selectedNode;

        private TelescopeService _telescopeService;

        #endregion

        #region Contructors

        public MainWindowViewModel()
        {
            _telescopeService = new();
            ResourceNodes = new();

        }

        #endregion

        #region Commands


        #endregion

        #region Properties

        public ResourceNode SelectedNode
        {
            get => _selectedNode;
            set
            {
                _selectedNode = value;
                RaisePropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                RaisePropertyChanged();
            }
        }

        public string BusyText
        {
            get => _busyText ?? DefaultBusyText;
            set
            {
                _busyText = value;
                RaisePropertyChanged();
            }
        }


        public ObservableCollection<ResourceNode> ResourceNodes
        {
            get => _resourceNodes;
            set
            {
                _resourceNodes = value;
                RaisePropertyChanged();
            }
        }

        #endregion

        #region Public Methods


        public async Task OnLoadedAsync(object parameter)
        {
            try
            {
                if (ResourceNodes.Any())
                    ResourceNodes.Clear();


                Stopwatch sw = Stopwatch.StartNew();

                //var subscriptions = await Do<IEnumerable<SubscriptionResource>>(() =>
                //{
                //    return _telescopeService.GetSubscriptionsAsync();
                //}, "Loading subscriptions...");

                //var groups = await Do<IEnumerable<ResourceGroupResource>> (async () =>
                //{
                //    List<ResourceGroupResource> groupList = new();

                //    foreach (var subscription in subscriptions)
                //    {
                //        var loadedGroups = await _telescopeService.GetResourceGroupsAsync(subscription);

                //        if (loadedGroups.Any())
                //            groupList.AddRange(loadedGroups);
                //    }

                //    return groupList;
                //}, "Loading groups...");


                //var logicApps = await Do<IEnumerable<AzureLogicAppInfo>>(async () =>
                //{
                //    List<AzureLogicAppInfo> apps = new();

                //    foreach (var group in groups)
                //    {
                //        var loadedApps = await _telescopeService.GetLogicAppsAsync(group);

                //        if (loadedApps.Any())
                //            apps.AddRange(loadedApps);
                //    }

                //    return apps;
                //}, "Extracting logic apps...");


                //List<>

                //await foreach (var t in _telescopeService.LoadSubscriptionsAsync())
                //{

                //}

                var subscriptions = await Do<IEnumerable<SubscriptionResource>>(() =>
                {
                    return _telescopeService.GetSubscriptionsAsync();
                }, "Loading subscriptions...");


                if (subscriptions.Any())
                {
                    foreach(var subscription in subscriptions)
                    {
                        ResourceNodes.Add(new ResourceNode(subscription.Data.DisplayName, ResourceNodeType.Subscription, () => ExpandSubscriptionAsync(subscription)));
                    }
                }

                var time = sw.Elapsed.TotalMilliseconds;

                //if (logicApps.Any())
                //{
                //    foreach(var app in logicApps)
                //    {
                //        await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate {
                //            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                //            LogicAppCollection.Add(new LogicAppViewModel { Name = app.LogicAppId });
                //        });
                //    }
                //}
            }
            catch (Exception ex)
            {

            }
        }

        #endregion


        private async Task<IEnumerable<ResourceNode>> ExpandSubscriptionAsync(SubscriptionResource subscription)
        {
            IEnumerable<ResourceGroupResource> groups = await _telescopeService.GetResourceGroupsAsync(subscription);

            return groups.Select(g => new ResourceNode(g.Data.Name, ResourceNodeType.ResourceGroup, () => ExpandGroupsAsync(g), true));
        }

        private async Task<IEnumerable<ResourceNode>> ExpandGroupsAsync(ResourceGroupResource resourceGroup)
        {
            IEnumerable<AzureLogicAppInfo> logicApps = await _telescopeService.GetLogicAppsAsync(resourceGroup);

            if (logicApps.Any())
            {

            }

            return logicApps;
        }

        #region Private Methods

        private void SetBusy(string busyText = null)
        {
            IsBusy = true;
            BusyText = busyText;
        }

        private void Free()
        {
            IsBusy = false;
            BusyText = null;
        }

        private async Task Do(Func<Task> action, string busyText = null)
        {
            SetBusy(busyText);

            try
            {
                await action();
            }
            catch(Exception ex)
            {
                // TODO: handle
            }
            finally
            {
                Free();
            }
        }

        private async Task<T> Do<T>(Func<Task<T>> action, string busyText = null)
        {
            SetBusy(busyText);

            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                // TODO: handle
                throw;
            }
            finally
            {
                Free();
            }
        }

        #endregion
    }
}
