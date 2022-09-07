namespace Cats.Telescope.VsExtension.Core.Models
{
    internal class AzureLogicAppInfo
    {
        public AzureLogicAppInfo(string subscriptionId, string resourceGroupId, string logicAppId)
        {
            SubscriptionId = subscriptionId;
            ResourceGroupId = resourceGroupId;
            LogicAppId = logicAppId;
        }

        public string SubscriptionId { get; }
        public string ResourceGroupId { get; }
        public string LogicAppId { get; }
    }
}
