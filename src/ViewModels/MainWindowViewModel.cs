using Azure.ResourceManager.Resources;
using Cats.Telescope.VsExtension.Core.Models;
using Cats.Telescope.VsExtension.Core.Services;
using Microsoft.VisualStudio.Shell;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private ObservableCollection<LogicAppViewModel> _logicAppCollection;

        private TelescopeService _telescopeService;

        #endregion

        #region Contructors

        public MainWindowViewModel()
        {
            _telescopeService = new();
            LogicAppCollection = new ObservableCollection<LogicAppViewModel>();
        }

        #endregion

        #region Commands

        #endregion

        #region Properties

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


        public ObservableCollection<LogicAppViewModel> LogicAppCollection
        {
            get => _logicAppCollection;
            set
            {
                _logicAppCollection = value;
                RaisePropertyChanged();
            }
        }

        #endregion


        #region Public Methods

        public async Task OnLoadedAsync(object parameter)
        {
            try
            {
                if (LogicAppCollection.Any())
                    LogicAppCollection.Clear();

                var subscriptions = await Do<IEnumerable<SubscriptionResource>>(() =>
                {
                    return _telescopeService.LoadSubscriptionsAsync();
                }, "Loading subscriptions...");

                var groups = await Do<IEnumerable<ResourceGroupResource>> (async () =>
                {
                    List<ResourceGroupResource> groupList = new();

                    foreach (var subscription in subscriptions)
                    {
                        var loadedGroups = await _telescopeService.LoadResourceGroupsAsync(subscription);

                        if (loadedGroups.Any())
                            groupList.AddRange(loadedGroups);
                    }

                    return groupList;
                }, "Loading groups...");


                var logicApps = await Do<IEnumerable<AzureLogicAppInfo>>(async () =>
                {
                    List<AzureLogicAppInfo> apps = new();

                    foreach (var group in groups)
                    {
                        var loadedApps = await _telescopeService.LoadLogicAppsAsync(group);

                        if (loadedApps.Any())
                            apps.AddRange(loadedApps);
                    }

                    return apps;
                }, "Extracting logic apps...");

                if (logicApps.Any())
                {
                    foreach(var app in logicApps)
                    {
                        await ThreadHelper.JoinableTaskFactory.RunAsync(async delegate {
                            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                            LogicAppCollection.Add(new LogicAppViewModel { Name = app.LogicAppId });
                        });
                    }
                }
            }
            catch (Exception ex)
            {

            }
        }

        #endregion


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
